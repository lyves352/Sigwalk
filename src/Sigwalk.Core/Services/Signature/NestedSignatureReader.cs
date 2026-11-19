using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using Sigwalk.Core.Interop;
using Sigwalk.Core.Logging;
using Sigwalk.Core.Models;
using Sigwalk.Core.Utilities;

namespace Sigwalk.Core.Services.Signature;

/// <summary>
/// 读取 PKCS#7 未认证属性中的嵌套签名（Microsoft 双签名等），仅用于展示。
/// 幂等、尽力而为：任何解析错误都不会影响 WinVerifyTrust 给出的文件整体结论。
/// </summary>
public sealed class NestedSignatureReader
{
    private const int MaxDepth = 64;
    private const int MaxSignatures = 64;

    private readonly ILogService _log;

    public NestedSignatureReader(ILogService? log = null)
    {
        _log = log ?? NullLogService.Instance;
    }

    public List<SignatureInfo> Read(string filePath, int startIndex = 0)
    {
        var result = new List<SignatureInfo>();

        try
        {
            foreach (var blob in PeCertificateReader.ReadSignedDataBlobs(filePath))
            {
                if (result.Count >= MaxSignatures)
                {
                    break;
                }

                ProcessCms(blob, 0, includeTopLevel: false, result, startIndex);
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"读取嵌套签名失败：{filePath} - {ex.Message}");
        }

        return result;
    }

    /// <summary>直接从 PKCS#7 数据解析嵌套签名（测试与诊断用）。</summary>
    internal List<SignatureInfo> Parse(byte[] pkcs7, int startIndex = 0)
    {
        var result = new List<SignatureInfo>();
        try
        {
            ProcessCms(pkcs7, 0, includeTopLevel: false, result, startIndex);
        }
        catch (Exception ex)
        {
            _log.Warn($"解析嵌套签名数据失败：{ex.Message}");
        }

        return result;
    }

    private void ProcessCms(byte[] der, int depth, bool includeTopLevel, List<SignatureInfo> result, int startIndex)
    {
        if (depth > MaxDepth || result.Count >= MaxSignatures)
        {
            return;
        }

        SignedCms cms;
        try
        {
            cms = new SignedCms();
            cms.Decode(der);
        }
        catch (CryptographicException ex)
        {
            _log.Warn($"解析 PKCS#7 数据失败（depth={depth}）：{ex.Message}");
            return;
        }

        if (includeTopLevel)
        {
            foreach (var signer in cms.SignerInfos)
            {
                if (result.Count >= MaxSignatures)
                {
                    return;
                }

                result.Add(BuildNestedSignature(signer, cms, startIndex + result.Count));
            }
        }

        foreach (var signer in cms.SignerInfos)
        {
            foreach (var attribute in signer.UnsignedAttributes)
            {
                if (attribute.Oid?.Value != WinTrustConstants.OID_NESTED_SIGNATURE)
                {
                    continue;
                }

                foreach (AsnEncodedData value in attribute.Values)
                {
                    if (result.Count >= MaxSignatures)
                    {
                        return;
                    }

                    ProcessCms(value.RawData, depth + 1, includeTopLevel: true, result, startIndex);
                }
            }
        }
    }

    private static SignatureInfo BuildNestedSignature(SignerInfo signer, SignedCms cms, int index)
    {
        X509Certificate2? certificate = signer.Certificate;
        if (certificate is null && cms.Certificates.Count > 0)
        {
            certificate = cms.Certificates[0];
        }

        return new SignatureInfo
        {
            Index = index,
            IsPrimary = false,
            IsNested = true,
            IsVerified = false,
            Status = SignatureStatus.Unknown,
            StatusText = "仅展示（未验证）",
            Warning = "嵌套签名仅用于展示，不单独参与文件整体签名状态判定。",
            Certificate = certificate is null ? null : CertificateInfoFactory.CreateSimple(certificate),
            Timestamp = TryReadTimestamp(signer),
            SignatureAlgorithm = OidNames.SignatureAlgorithm(signer.SignatureAlgorithm?.Value),
            DigestAlgorithm = OidNames.DigestAlgorithm(signer.DigestAlgorithm?.Value),
            Chain = new List<ChainElementInfo>(),
        };
    }

    private static TimestampInfo? TryReadTimestamp(SignerInfo signer)
    {
        foreach (var attribute in signer.UnsignedAttributes)
        {
            if (attribute.Oid?.Value != WinTrustConstants.OID_RFC3161_TIMESTAMP)
            {
                continue;
            }

            foreach (AsnEncodedData value in attribute.Values)
            {
                try
                {
                    if (!Rfc3161TimestampToken.TryDecode(value.RawData, out var token, out _) || token is null)
                    {
                        continue;
                    }

                    X509Certificate2? tsaCertificate = null;
                    try
                    {
                        var tsaCms = token.AsSignedCms();
                        tsaCertificate = tsaCms.SignerInfos.Count > 0
                            ? tsaCms.SignerInfos[0].Certificate
                            : null;
                        tsaCertificate ??= tsaCms.Certificates.Count > 0 ? tsaCms.Certificates[0] : null;
                    }
                    catch (CryptographicException)
                    {
                    }

                    return new TimestampInfo
                    {
                        Timestamp = token.TokenInfo.Timestamp.UtcDateTime,
                        Signer = tsaCertificate is null ? null : CertificateInfoFactory.CreateSimple(tsaCertificate),
                        Type = "RFC3161",
                    };
                }
                catch (CryptographicException)
                {
                }
                catch (ArgumentOutOfRangeException)
                {
                }
            }
        }

        return null;
    }
}
