using System.Runtime.InteropServices;
using Sigwalk.Core.Interop;
using Sigwalk.Core.Models;

namespace Sigwalk.Core.Services.Signature;

internal sealed record ExtractedSignatureInfo(
    CertificateInfo? Certificate,
    List<ChainElementInfo> Chain,
    string? SignatureAlgorithmOid,
    string? DigestAlgorithmOid,
    TimestampInfo? Timestamp,
    int SignerError,
    int CertificateError);

internal sealed record WinTrustCallResult(
    int Status,
    uint SecondarySignatureCount,
    ExtractedSignatureInfo? Signature,
    bool HasStateData);

/// <summary>
/// WinVerifyTrust 调用封装。每次调用都使用独立的 state data，
/// 并在释放前复制所有托管数据。
/// </summary>
internal sealed unsafe class WinTrustInvoker
{
    public WinTrustCallResult Invoke(
        string filePath,
        RevocationMode revocationMode,
        int? signatureIndex,
        bool countSecondary,
        uint knownSecondaryCount)
    {
        var action = WinTrustConstants.WINTRUST_ACTION_GENERIC_VERIFY_V2;
        var provFlags = GetProvFlags(revocationMode);
        var useSettings = countSecondary || signatureIndex.HasValue;

        uint secondaryCount = 0;
        ExtractedSignatureInfo? extracted = null;
        var statePresent = false;
        var status = int.MinValue;

        fixed (char* pathPointer = filePath)
        {
            var fileInfo = new WINTRUST_FILE_INFO
            {
                cbStruct = (uint)sizeof(WINTRUST_FILE_INFO),
                pcwszFilePath = pathPointer,
                hFile = IntPtr.Zero,
                pgKnownSubject = null,
            };

            var settings = new WINTRUST_SIGNATURE_SETTINGS
            {
                cbStruct = (uint)sizeof(WINTRUST_SIGNATURE_SETTINGS),
            };

            if (countSecondary)
            {
                settings.dwFlags = WinTrustConstants.WSS_GET_SECONDARY_SIG_COUNT;
            }
            else if (signatureIndex.HasValue)
            {
                settings.dwFlags = WinTrustConstants.WSS_VERIFY_SPECIFIC;
                settings.dwIndex = (uint)signatureIndex.Value;
                settings.cSecondarySigs = knownSecondaryCount;
            }

            var data = new WINTRUST_DATA
            {
                cbStruct = (uint)sizeof(WINTRUST_DATA),
                dwUIChoice = WinTrustConstants.WTD_UI_NONE,
                fdwRevocationChecks = WinTrustConstants.WTD_REVOKE_NONE,
                dwUnionChoice = WinTrustConstants.WTD_CHOICE_FILE,
                pChoice = &fileInfo,
                dwStateAction = WinTrustConstants.WTD_STATEACTION_VERIFY,
                dwProvFlags = provFlags,
                dwUIContext = 0,
                pSignatureSettings = useSettings ? &settings : null,
            };

            try
            {
                status = WinTrustNative.WinVerifyTrust(IntPtr.Zero, ref action, ref data);
                secondaryCount = settings.cSecondarySigs;
                statePresent = data.hWVTStateData != IntPtr.Zero;

                if (statePresent && signatureIndex.HasValue)
                {
                    try
                    {
                        extracted = ExtractSignature(data.hWVTStateData, revocationMode);
                    }
                    catch
                    {
                        // 证书/时间戳读取失败不改变 WinVerifyTrust 的权威结论。
                        extracted = null;
                    }
                }
            }
            finally
            {
                if (data.hWVTStateData != IntPtr.Zero)
                {
                    data.dwStateAction = WinTrustConstants.WTD_STATEACTION_CLOSE;
                    try
                    {
                        WinTrustNative.WinVerifyTrust(IntPtr.Zero, ref action, ref data);
                    }
                    catch
                    {
                    }
                }
            }
        }

        return new WinTrustCallResult(status, secondaryCount, extracted, statePresent);
    }

    private static uint GetProvFlags(RevocationMode mode) => mode switch
    {
        RevocationMode.None =>
            WinTrustConstants.WTD_REVOCATION_CHECK_NONE |
            WinTrustConstants.WTD_CACHE_ONLY_URL_RETRIEVAL |
            WinTrustConstants.WTD_DISABLE_MD2_MD4,
        RevocationMode.CacheOnly =>
            WinTrustConstants.WTD_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT |
            WinTrustConstants.WTD_CACHE_ONLY_URL_RETRIEVAL |
            WinTrustConstants.WTD_DISABLE_MD2_MD4,
        RevocationMode.Online =>
            WinTrustConstants.WTD_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT |
            WinTrustConstants.WTD_DISABLE_MD2_MD4,
        _ => WinTrustConstants.WTD_REVOCATION_CHECK_NONE | WinTrustConstants.WTD_DISABLE_MD2_MD4,
    };

    private static ExtractedSignatureInfo? ExtractSignature(IntPtr stateData, RevocationMode mode)
    {
        var provData = WinTrustNative.WTHelperProvDataFromStateData(stateData);
        if (provData == IntPtr.Zero)
        {
            return null;
        }

        var pSigner = WinTrustNative.WTHelperGetProvSignerFromChain(provData, 0, false, 0);
        if (pSigner == IntPtr.Zero)
        {
            return null;
        }

        var signer = Marshal.PtrToStructure<CRYPT_PROVIDER_SGNR>(pSigner);

        CertificateInfo? certificate = null;
        var chain = new List<ChainElementInfo>();
        var certificateError = 0;

        var pProviderCert = WinTrustNative.WTHelperGetProvCertFromChain(pSigner, 0);
        if (pProviderCert != IntPtr.Zero)
        {
            var providerCert = Marshal.PtrToStructure<CRYPT_PROVIDER_CERT>(pProviderCert);
            certificateError = unchecked((int)providerCert.dwError);

            using var x509 = CertificateContextHelper.ToCertificate(providerCert.pCert);
            if (x509 is not null)
            {
                var snapshot = CertificateInfoFactory.Create(x509, mode);
                certificate = snapshot.Info;
                chain = snapshot.Chain;
            }
        }

        string? signatureOid = null;
        string? digestOid = null;
        if (signer.psSigner != IntPtr.Zero)
        {
            try
            {
                var signerInfo = Marshal.PtrToStructure<CMSG_SIGNER_INFO>(signer.psSigner);
                signatureOid = Marshal.PtrToStringAnsi(signerInfo.HashEncryptionAlgorithm.pszObjId);
                digestOid = Marshal.PtrToStringAnsi(signerInfo.HashAlgorithm.pszObjId);
            }
            catch
            {
            }
        }

        return new ExtractedSignatureInfo(
            certificate,
            chain,
            signatureOid,
            digestOid,
            ExtractTimestamp(provData, signer),
            unchecked((int)signer.dwError),
            certificateError);
    }

    private static TimestampInfo? ExtractTimestamp(IntPtr provData, CRYPT_PROVIDER_SGNR signer)
    {
        TimestampInfo? firstTimestamp = null;
        var counterCount = Math.Min(signer.csCounterSigners, 16u);
        var signerSize = Marshal.SizeOf<CRYPT_PROVIDER_SGNR>();

        for (uint index = 0; index < counterCount; index++)
        {
            var pCounterSigner = WinTrustNative.WTHelperGetProvSignerFromChain(provData, 0, true, index);
            if (pCounterSigner == IntPtr.Zero && signer.pasCounterSigners != IntPtr.Zero)
            {
                pCounterSigner = IntPtr.Add(signer.pasCounterSigners, (int)index * signerSize);
            }

            if (pCounterSigner == IntPtr.Zero)
            {
                continue;
            }

            var counterSigner = Marshal.PtrToStructure<CRYPT_PROVIDER_SGNR>(pCounterSigner);
            var time = FileTimeHelper.ToDateTime(counterSigner.sftVerifyAsOf) ??
                       FileTimeHelper.ToDateTime(signer.sftVerifyAsOf);

            CertificateInfo? signerCertificate = null;
            var pProviderCert = WinTrustNative.WTHelperGetProvCertFromChain(pCounterSigner, 0);
            if (pProviderCert != IntPtr.Zero)
            {
                var providerCert = Marshal.PtrToStructure<CRYPT_PROVIDER_CERT>(pProviderCert);
                using var x509 = CertificateContextHelper.ToCertificate(providerCert.pCert);
                if (x509 is not null)
                {
                    signerCertificate = CertificateInfoFactory.CreateSimple(x509);
                }
            }

            var isRfc3161 = counterSigner.dwSignerType == WinTrustConstants.SGNR_TYPE_TIMESTAMP;
            var info = new TimestampInfo
            {
                Timestamp = time,
                Signer = signerCertificate,
                Type = isRfc3161 ? "RFC3161" : "旧式时间戳",
            };

            if (isRfc3161)
            {
                return info;
            }

            firstTimestamp ??= info;
        }

        return firstTimestamp;
    }
}
