namespace Sigwalk.Core.Utilities;

/// <summary>常见签名/摘要算法 OID 到可读名称的映射。</summary>
public static class OidNames
{
    private static readonly Dictionary<string, string> SignatureAlgorithms = new(StringComparer.Ordinal)
    {
        ["1.2.840.113549.1.1.1"] = "RSA",
        ["1.2.840.113549.1.1.4"] = "MD5-RSA",
        ["1.2.840.113549.1.1.5"] = "SHA1-RSA",
        ["1.2.840.113549.1.1.11"] = "SHA256-RSA",
        ["1.2.840.113549.1.1.12"] = "SHA384-RSA",
        ["1.2.840.113549.1.1.13"] = "SHA512-RSA",
        ["1.2.840.113549.1.1.14"] = "SHA224-RSA",
        ["1.3.14.3.2.29"] = "SHA1-RSA",
        ["1.2.840.10045.2.1"] = "ECDSA",
        ["1.2.840.10045.4.1"] = "ECDSA-SHA1",
        ["1.2.840.10045.4.3.2"] = "ECDSA-SHA256",
        ["1.2.840.10045.4.3.3"] = "ECDSA-SHA384",
        ["1.2.840.10045.4.3.4"] = "ECDSA-SHA512",
        ["1.2.840.10040.4.3"] = "DSA-SHA1",
        ["2.16.840.1.101.3.4.3.2"] = "DSA-SHA256",
    };

    private static readonly Dictionary<string, string> DigestAlgorithms = new(StringComparer.Ordinal)
    {
        ["1.2.840.113549.2.5"] = "MD5",
        ["1.3.14.3.2.26"] = "SHA1",
        ["2.16.840.1.101.3.4.2.4"] = "SHA224",
        ["2.16.840.1.101.3.4.2.1"] = "SHA256",
        ["2.16.840.1.101.3.4.2.2"] = "SHA384",
        ["2.16.840.1.101.3.4.2.3"] = "SHA512",
    };

    public static string SignatureAlgorithm(string? oid)
    {
        if (string.IsNullOrWhiteSpace(oid))
        {
            return "未知";
        }

        return SignatureAlgorithms.TryGetValue(oid, out var name) ? name : oid;
    }

    public static string DigestAlgorithm(string? oid)
    {
        if (string.IsNullOrWhiteSpace(oid))
        {
            return "未知";
        }

        return DigestAlgorithms.TryGetValue(oid, out var name) ? name : oid;
    }

    /// <summary>组合显示，例如 “SHA256-RSA / SHA256”。</summary>
    public static string Describe(string? signatureOid, string? digestOid)
    {
        var signature = SignatureAlgorithm(signatureOid);
        var digest = DigestAlgorithm(digestOid);
        return digest == "未知" ? signature : $"{signature} / {digest}";
    }
}
