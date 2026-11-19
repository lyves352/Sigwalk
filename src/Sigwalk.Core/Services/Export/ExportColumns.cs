using Sigwalk.Core.Models;

namespace Sigwalk.Core.Services.Export;

public enum ExportFormat
{
    Csv,
    Xlsx,
    Json,
}

internal sealed record ExportColumn(string Header, Func<FileScanResult, int, string> Value, double Width);

/// <summary>CSV 与 XLSX 共用的导出列定义。</summary>
internal static class ExportColumns
{
    public static IReadOnlyList<ExportColumn> All { get; } = new List<ExportColumn>
    {
        new("序号", (result, index) => (index + 1).ToString(), 6),
        new("文件名称", (result, _) => result.FileName, 32),
        new("文件路径", (result, _) => result.FilePath, 65),
        new("文件大小", (result, _) => result.SizeText, 12),
        new("大小(字节)", (result, _) => result.SizeBytes.ToString(), 14),
        new("文件类型", (result, _) => result.FileType, 9),
        new("修改时间", (result, _) => FormatTime(result.LastWriteTime), 20),
        new("签名状态", (result, _) => result.SignatureChecked ? result.SignatureStatusText : "未检测", 12),
        new("签名者", (result, _) => JoinSignatures(result, s => s.SignerName), 40),
        new("证书颁发者", (result, _) => JoinSignatures(result, s => s.IssuerName), 40),
        new("证书序列号", (result, _) => JoinSignatures(result, s => s.Certificate?.SerialNumber), 40),
        new("证书指纹", (result, _) => JoinSignatures(result, s => s.Certificate?.Thumbprint), 46),
        new("证书有效期", (result, _) => JoinSignatures(result, s => s.Certificate?.ValidityText), 42),
        new("时间戳签名者", (result, _) => JoinSignatures(result, s => s.Timestamp?.SignerName), 32),
        new("时间戳时间", (result, _) => JoinSignatures(result, s => s.Timestamp?.TimestampText), 20),
        new("签名算法", (result, _) => JoinSignatures(result, s => $"{s.SignatureAlgorithm} / {s.DigestAlgorithm}"), 30),
        new("证书链状态", (result, _) => JoinSignatures(result, s => s.Certificate?.ChainStatusText), 40),
        new("签名详情", (result, _) => JoinSignatures(result, DescribeSignature), 70),
        new("SHA1", (result, _) => result.Sha1Text, 44),
        new("SHA256", (result, _) => result.Sha256Text, 68),
        new("扫描状态", (result, _) => result.ScanStatusText, 10),
        new("错误信息", (result, _) => result.ErrorMessage ?? string.Empty, 40),
        new("签名验证错误", (result, _) => result.SignatureError ?? string.Empty, 45),
        new("警告", (result, _) => result.SignatureWarning ?? string.Empty, 45),
    };

    private static string JoinSignatures(FileScanResult result, Func<SignatureInfo, string?> selector)
    {
        var values = result.Signatures
            .Select(selector)
            .Where(value => !string.IsNullOrWhiteSpace(value) && value != "—")
            .Select(value => value!.ReplaceLineEndings(" "))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return string.Join(" | ", values);
    }

    private static string DescribeSignature(SignatureInfo signature)
    {
        var parts = new List<string>
        {
            $"#{signature.Index + 1}",
            signature.IsPrimary ? "主签名" : signature.IsNested ? "嵌套签名" : "附加签名",
            signature.StatusText ?? string.Empty,
        };

        if (signature.Certificate is not null)
        {
            parts.Add($"签名者={signature.Certificate.SubjectName}");
            parts.Add($"颁发者={signature.Certificate.IssuerName}");
            parts.Add($"有效期={signature.Certificate.ValidityText}");
        }

        if (!string.IsNullOrEmpty(signature.SignatureAlgorithm))
        {
            parts.Add($"算法={signature.SignatureAlgorithm}/{signature.DigestAlgorithm}");
        }

        if (signature.Timestamp is not null)
        {
            parts.Add($"时间戳签名者={signature.Timestamp.SignerName}");
            parts.Add($"时间戳时间={signature.Timestamp.TimestampText}");
        }

        if (!string.IsNullOrEmpty(signature.ErrorMessage))
        {
            parts.Add($"错误={signature.ErrorMessage}");
        }

        return string.Join("; ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static string FormatTime(DateTime value) =>
        value == DateTime.MinValue ? string.Empty : value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
}
