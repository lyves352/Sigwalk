using System.Text.Json.Serialization;

namespace Sigwalk.Core.Models;

/// <summary>X.509 证书的展示信息。所有字段在签名状态数据释放前复制完成。</summary>
public sealed class CertificateInfo
{
    public string Subject { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string IssuerName { get; init; } = string.Empty;
    public string SerialNumber { get; init; } = string.Empty;
    public string Thumbprint { get; init; } = string.Empty;

    /// <summary>证书生效时间（UTC）。</summary>
    public DateTime NotBefore { get; init; }

    /// <summary>证书失效时间（UTC）。</summary>
    public DateTime NotAfter { get; init; }

    public string SignatureAlgorithm { get; init; } = string.Empty;
    public RevocationStatus RevocationStatus { get; init; } = RevocationStatus.Unknown;
    public string ChainStatusText { get; init; } = string.Empty;
    public bool IsTrustedRoot { get; init; }
    public bool IsSelfSigned { get; init; }

    [JsonIgnore]
    public string ValidityText =>
        $"{NotBefore.ToLocalTime():yyyy-MM-dd HH:mm} ~ {NotAfter.ToLocalTime():yyyy-MM-dd HH:mm}";

    [JsonIgnore]
    public bool IsExpired => DateTime.UtcNow > NotAfter;

    [JsonIgnore]
    public string RevocationStatusText => Utilities.StatusText.DescribeRevocationStatus(RevocationStatus);
}

/// <summary>证书链中的一个元素。</summary>
public sealed class ChainElementInfo
{
    public int Index { get; init; }
    public string Subject { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Thumbprint { get; init; } = string.Empty;
    public DateTime NotBefore { get; init; }
    public DateTime NotAfter { get; init; }
    public string StatusText { get; init; } = string.Empty;

    [JsonIgnore]
    public string ValidityText =>
        $"{NotBefore.ToLocalTime():yyyy-MM-dd HH:mm} ~ {NotAfter.ToLocalTime():yyyy-MM-dd HH:mm}";
}
