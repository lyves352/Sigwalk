using System.Text.Json.Serialization;
using Sigwalk.Core.Utilities;

namespace Sigwalk.Core.Models;

/// <summary>单个文件的完整扫描结果。</summary>
public sealed class FileScanResult
{
    public string FilePath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public long SizeBytes { get; init; }

    /// <summary>文件扩展名（不含点，大写），例如 EXE、DLL。</summary>
    public string FileType { get; init; } = string.Empty;

    public DateTime LastWriteTime { get; init; }
    public ScanStatus ScanStatus { get; init; } = ScanStatus.Pending;
    public string? ErrorMessage { get; init; }
    public SignatureStatus SignatureStatus { get; init; } = SignatureStatus.Unknown;
    public string? SignatureWarning { get; init; }

    /// <summary>签名验证失败原因（扫描本身成功时使用，区别于扫描失败）。</summary>
    public string? SignatureError { get; init; }
    public List<SignatureInfo> Signatures { get; init; } = new();
    public bool SignatureChecked { get; init; }
    public bool HashChecked { get; init; }
    public string? Sha1 { get; init; }
    public string? Sha256 { get; init; }
    public FileVersionData? VersionInfo { get; init; }
    public DateTime ScannedAt { get; init; }

    [JsonIgnore]
    public string SizeText => FileSizeFormatter.Format(SizeBytes);

    [JsonIgnore]
    public string SignatureStatusText => StatusText.DescribeSignatureStatus(SignatureStatus);

    [JsonIgnore]
    public string ScanStatusText => StatusText.DescribeScanStatus(ScanStatus);

    [JsonIgnore]
    public bool IsFailed => ScanStatus == ScanStatus.Failed;

    /// <summary>所有签名者名称，多个签名用换行分隔。</summary>
    [JsonIgnore]
    public string SignerNames => JoinDistinct(Signatures.Select(s => s.SignerName));

    /// <summary>所有证书颁发者名称，多个签名用换行分隔。</summary>
    [JsonIgnore]
    public string IssuerNames => JoinDistinct(Signatures.Select(s => s.IssuerName));

    /// <summary>所有证书有效期区间，多个签名用换行分隔。</summary>
    [JsonIgnore]
    public string ValidityText => JoinDistinct(Signatures.Select(s => s.ValidityText));

    [JsonIgnore]
    public string Sha1Text => string.IsNullOrEmpty(Sha1) ? "—" : Sha1;

    [JsonIgnore]
    public string Sha256Text => string.IsNullOrEmpty(Sha256) ? "—" : Sha256;

    [JsonIgnore]
    public string Extension => string.IsNullOrEmpty(FileType) ? string.Empty : "." + FileType;

    private static string JoinDistinct(IEnumerable<string> values)
    {
        var list = values
            .Where(v => !string.IsNullOrWhiteSpace(v) && v != "—")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return list.Count == 0 ? "—" : string.Join(Environment.NewLine, list);
    }

    /// <summary>创建待扫描条目（用于界面占位）。</summary>
    public static FileScanResult CreatePending(string filePath)
    {
        var info = SafeFileInfo(filePath);
        return new FileScanResult
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            FileType = GetFileType(filePath),
            SizeBytes = info?.Length ?? 0,
            LastWriteTime = info?.LastWriteTimeUtc ?? DateTime.MinValue,
            ScanStatus = ScanStatus.Pending,
            SignatureChecked = false,
            HashChecked = false,
        };
    }

    /// <summary>创建扫描失败结果。</summary>
    public static FileScanResult CreateFailed(string filePath, string errorMessage, bool signatureChecked = false, bool hashChecked = false)
    {
        var info = SafeFileInfo(filePath);
        return new FileScanResult
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            FileType = GetFileType(filePath),
            SizeBytes = info?.Length ?? 0,
            LastWriteTime = info?.LastWriteTimeUtc ?? DateTime.MinValue,
            ScanStatus = ScanStatus.Failed,
            SignatureStatus = SignatureStatus.Unknown,
            ErrorMessage = errorMessage,
            ScannedAt = DateTime.UtcNow,
            SignatureChecked = signatureChecked,
            HashChecked = hashChecked,
        };
    }

    public static string GetFileType(string filePath) =>
        Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();

    private static FileInfo? SafeFileInfo(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? info : null;
        }
        catch
        {
            return null;
        }
    }
}
