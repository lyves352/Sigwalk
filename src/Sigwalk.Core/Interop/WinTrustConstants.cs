namespace Sigwalk.Core.Interop;

/// <summary>WinTrust 与相关 CryptoAPI 常量。</summary>
internal static class WinTrustConstants
{
    /// <summary>WinVerifyTrust 的 Authenticode 验证动作。</summary>
    public static readonly Guid WINTRUST_ACTION_GENERIC_VERIFY_V2 =
        new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    public const uint WTD_UI_NONE = 2;
    public const uint WTD_REVOKE_NONE = 0;
    public const uint WTD_CHOICE_FILE = 1;
    public const uint WTD_STATEACTION_VERIFY = 1;
    public const uint WTD_STATEACTION_CLOSE = 2;

    public const uint WTD_REVOCATION_CHECK_NONE = 0x00000010;
    public const uint WTD_REVOCATION_CHECK_END_CERT = 0x00000020;
    public const uint WTD_REVOCATION_CHECK_CHAIN = 0x00000040;
    public const uint WTD_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT = 0x00000080;
    public const uint WTD_CACHE_ONLY_URL_RETRIEVAL = 0x00001000;
    public const uint WTD_DISABLE_MD2_MD4 = 0x00002000;
    public const uint WTD_LIFETIME_SIGNING_FLAG = 0x00000800;

    public const uint WSS_VERIFY_SPECIFIC = 0x00000001;
    public const uint WSS_GET_SECONDARY_SIG_COUNT = 0x00000002;
    public const uint SGNR_TYPE_TIMESTAMP = 0x00000010;

    public const uint CERT_QUERY_OBJECT_FILE = 1;
    public const uint CERT_QUERY_CONTENT_FLAG_PKCS7_SIGNED_EMBED = 0x00000400;
    public const uint CERT_QUERY_CONTENT_PKCS7_SIGNED_EMBED = 10;
    public const uint CERT_QUERY_FORMAT_FLAG_BINARY = 0x00000002;

    public const ushort WIN_CERT_TYPE_PKCS_SIGNED_DATA = 0x0002;

    public const string OID_NESTED_SIGNATURE = "1.3.6.1.4.1.311.2.4.1";
    public const string OID_RFC3161_TIMESTAMP = "1.3.6.1.4.1.311.3.3.1";
    public const string OID_LEGACY_COUNTERSIGN = "1.2.840.113549.1.9.6";
}
