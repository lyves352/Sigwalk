namespace Sigwalk.Core.Interop;

/// <summary>WinVerifyTrust 可能返回的常见状态码。</summary>
internal static class Win32ErrorCodes
{
    public const int S_OK = 0;

    public const int TRUST_E_PROVIDER_UNKNOWN = unchecked((int)0x800B0001);
    public const int TRUST_E_ACTION_UNKNOWN = unchecked((int)0x800B0002);
    public const int TRUST_E_SUBJECT_FORM_UNKNOWN = unchecked((int)0x800B0003);
    public const int TRUST_E_SUBJECT_NOT_TRUSTED = unchecked((int)0x800B0004);
    public const int TRUST_E_NOSIGNATURE = unchecked((int)0x800B0100);
    public const int CERT_E_EXPIRED = unchecked((int)0x800B0101);
    public const int CERT_E_UNTRUSTEDROOT = unchecked((int)0x800B0109);
    public const int CERT_E_CHAINING = unchecked((int)0x800B010A);
    public const int CERT_E_REVOKED = unchecked((int)0x800B010C);
    public const int CERT_E_REVOCATION_FAILURE = unchecked((int)0x800B010E);
    public const int CERT_E_WRONG_USAGE = unchecked((int)0x800B0110);
    public const int TRUST_E_EXPLICIT_DISTRUST = unchecked((int)0x800B0111);

    public const int TRUST_E_BAD_DIGEST = unchecked((int)0x80096010);
    public const int TRUST_E_CERT_SIGNATURE = unchecked((int)0x80096004);
    public const int TRUST_E_TIME_STAMP = unchecked((int)0x80096005);

    public const int CRYPT_E_REVOCATION_OFFLINE = unchecked((int)0x80092013);
    public const int CRYPT_E_SECURITY_SETTINGS = unchecked((int)0x80092026);

    public const int NTE_BAD_ALGID = unchecked((int)0x80090008);

    public const int ERROR_FILE_NOT_FOUND = unchecked((int)0x80070002);
    public const int ERROR_PATH_NOT_FOUND = unchecked((int)0x80070003);
    public const int ERROR_ACCESS_DENIED = unchecked((int)0x80070005);
    public const int ERROR_SHARING_VIOLATION = unchecked((int)0x80070020);
    public const int ERROR_INVALID_PARAMETER = unchecked((int)0x80070057);

    public static bool IsNoSignatureCode(int status) =>
        status == TRUST_E_NOSIGNATURE ||
        status == TRUST_E_SUBJECT_FORM_UNKNOWN;

    public static bool IsRevocationUnavailableCode(int status) =>
        status == CERT_E_REVOCATION_FAILURE ||
        status == CRYPT_E_REVOCATION_OFFLINE;

    public static string Format(int status) => $"0x{status:X8}";
}
