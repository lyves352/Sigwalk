using System.Runtime.InteropServices;

namespace Sigwalk.Core.Interop;

/// <summary>FILETIME 的互操作结构。必须使用两个 uint，不能用 long（4 字节对齐）。</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct WinFileTime
{
    public uint Low;
    public uint High;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct WINTRUST_FILE_INFO
{
    public uint cbStruct;
    public char* pcwszFilePath;
    public IntPtr hFile;
    public Guid* pgKnownSubject;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct WINTRUST_SIGNATURE_SETTINGS
{
    public uint cbStruct;
    public uint dwIndex;
    public uint dwFlags;
    public uint cSecondarySigs;
    public uint dwVerifiedSigIndex;
    public IntPtr pCryptoPolicy;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct WINTRUST_DATA
{
    public uint cbStruct;
    public IntPtr pPolicyCallbackData;
    public IntPtr pSIPClientData;
    public uint dwUIChoice;
    public uint fdwRevocationChecks;
    public uint dwUnionChoice;
    public void* pChoice;
    public uint dwStateAction;
    public IntPtr hWVTStateData;
    public IntPtr pwszURLReference;
    public uint dwProvFlags;
    public uint dwUIContext;
    public WINTRUST_SIGNATURE_SETTINGS* pSignatureSettings;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CRYPT_DATA_BLOB
{
    public uint cbData;
    public IntPtr pbData;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CRYPT_ALGORITHM_IDENTIFIER
{
    public IntPtr pszObjId;
    public CRYPT_DATA_BLOB Parameters;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CRYPT_ATTRIBUTES
{
    public uint cAttr;
    public IntPtr rgAttr;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CMSG_SIGNER_INFO
{
    public uint dwVersion;
    public CRYPT_DATA_BLOB Issuer;
    public CRYPT_DATA_BLOB SerialNumber;
    public CRYPT_ALGORITHM_IDENTIFIER HashAlgorithm;
    public CRYPT_ALGORITHM_IDENTIFIER HashEncryptionAlgorithm;
    public CRYPT_DATA_BLOB EncryptedHash;
    public CRYPT_ATTRIBUTES AuthAttrs;
    public CRYPT_ATTRIBUTES UnauthAttrs;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CRYPT_PROVIDER_SGNR
{
    public uint cbStruct;
    public WinFileTime sftVerifyAsOf;
    public uint csCertChain;
    public IntPtr pasCertChain;
    public uint dwSignerType;
    public IntPtr psSigner;
    public uint dwError;
    public uint csCounterSigners;
    public IntPtr pasCounterSigners;
    public IntPtr pChainContext;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CRYPT_PROVIDER_CERT
{
    public uint cbStruct;
    public IntPtr pCert;
    public int fCommercial;
    public int fTrustedRoot;
    public int fSelfSigned;
    public int fTestCert;
    public uint dwRevokedReason;
    public uint dwConfidence;
    public uint dwError;
    public IntPtr pTrustListContext;
    public int fTrustListSignerCert;
    public IntPtr pCtlContext;
    public uint dwCtlError;
    public int fIsCyclic;
    public IntPtr pChainElement;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CERT_CONTEXT
{
    public uint dwCertEncodingType;
    public IntPtr pbCertEncoded;
    public uint cbCertEncoded;
    public IntPtr pCertInfo;
    public IntPtr hCertStore;
}
