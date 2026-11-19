using System.Text.Json.Serialization;

namespace Sigwalk.Core.Models;

/// <summary>时间戳签名信息。</summary>
public sealed class TimestampInfo
{
    /// <summary>时间戳时间（UTC）。</summary>
    public DateTime? Timestamp { get; init; }

    /// <summary>时间戳签名者证书。</summary>
    public CertificateInfo? Signer { get; init; }

    /// <summary>时间戳类型，例如 RFC3161 或旧式计数器签名。</summary>
    public string Type { get; init; } = string.Empty;

    public string? ErrorMessage { get; init; }

    [JsonIgnore]
    public string TimestampText =>
        Timestamp.HasValue ? Timestamp.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : "—";

    [JsonIgnore]
    public string SignerName => Signer?.SubjectName ?? "—";
}

/// <summary>单个签名（主签名、附加签名或嵌套签名）的完整信息。</summary>
public sealed class SignatureInfo
{
    public int Index { get; init; }
    public bool IsPrimary { get; init; }
    public bool IsNested { get; init; }

    /// <summary>是否由 WinVerifyTrust 参与验证（嵌套签名仅展示）。</summary>
    public bool IsVerified { get; init; }

    public SignatureStatus Status { get; init; } = SignatureStatus.Unknown;
    public string? StatusText { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Warning { get; init; }

    /// <summary>WinVerifyTrust 返回的原始状态码。</summary>
    public int NativeStatus { get; init; }

    public CertificateInfo? Certificate { get; init; }
    public TimestampInfo? Timestamp { get; init; }
    public string? SignatureAlgorithm { get; init; }
    public string? DigestAlgorithm { get; init; }
    public List<ChainElementInfo> Chain { get; init; } = new();

    [JsonIgnore]
    public string SignerName => Certificate?.SubjectName ?? "—";

    [JsonIgnore]
    public string IssuerName => Certificate?.IssuerName ?? "—";

    [JsonIgnore]
    public string ValidityText => Certificate?.ValidityText ?? "—";

    [JsonIgnore]
    public string DisplayName =>
        (IsPrimary ? "主签名" : IsNested ? "嵌套签名" : $"附加签名 #{Index}") +
        (string.IsNullOrEmpty(SignerName) ? string.Empty : $" - {SignerName}");
}
