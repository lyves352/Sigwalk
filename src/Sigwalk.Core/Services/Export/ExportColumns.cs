using Sigwalk.Core.Localization;
using Sigwalk.Core.Models;

namespace Sigwalk.Core.Services.Export;

internal sealed record ExportColumn(string HeaderKey, Func<FileScanResult, int, string> Value, double Width)
{
    /// <summary>在写入文件时解析，确保语言切换后导出使用当前语言。</summary>
    public string Header => Loc.T(HeaderKey);
}

/// <summary>CSV 与 XLSX 共用的导出列定义。</summary>
internal static class ExportColumns
{
    public static IReadOnlyList<ExportColumn> All { get; } = new List<ExportColumn>
    {
        new("Export.Column.Index", (result, index) => (index + 1).ToString(), 6),
        new("Export.Column.FileName", (result, _) => result.FileName, 32),
        new("Export.Column.FilePath", (result, _) => result.FilePath, 65),
        new("Export.Column.Size", (result, _) => result.SizeText, 12),
        new("Export.Column.SizeBytes", (result, _) => result.SizeBytes.ToString(), 14),
        new("Export.Column.FileType", (result, _) => result.FileType, 9),
        new("Export.Column.LastWriteTime", (result, _) => FormatTime(result.LastWriteTime), 20),
        new("Export.Column.SignatureStatus",
            (result, _) => result.SignatureChecked ? result.SignatureStatusText : Loc.T("Common.NotChecked"), 12),
        new("Export.Column.Signer", (result, _) => JoinSignatures(result, s => s.SignerName), 40),
        new("Export.Column.Issuer", (result, _) => JoinSignatures(result, s => s.IssuerName), 40),
        new("Export.Column.SerialNumber", (result, _) => JoinSignatures(result, s => s.Certificate?.SerialNumber), 40),
        new("Export.Column.Thumbprint", (result, _) => JoinSignatures(result, s => s.Certificate?.Thumbprint), 46),
        new("Export.Column.Validity", (result, _) => JoinSignatures(result, s => s.Certificate?.ValidityText), 42),
        new("Export.Column.TimestampSigner", (result, _) => JoinSignatures(result, s => s.Timestamp?.SignerName), 32),
        new("Export.Column.TimestampTime", (result, _) => JoinSignatures(result, s => s.Timestamp?.TimestampText), 20),
        new("Export.Column.SignatureAlgorithm",
            (result, _) => JoinSignatures(result, s => $"{s.SignatureAlgorithm} / {s.DigestAlgorithm}"), 30),
        new("Export.Column.ChainStatus", (result, _) => JoinSignatures(result, s => s.Certificate?.ChainStatusText), 40),
        new("Export.Column.SignatureDetails", (result, _) => JoinSignatures(result, DescribeSignature), 70),
        new("Export.Column.Sha1", (result, _) => result.Sha1Text, 44),
        new("Export.Column.Sha256", (result, _) => result.Sha256Text, 68),
        new("Export.Column.ScanStatus", (result, _) => result.ScanStatusText, 10),
        new("Export.Column.Error", (result, _) => result.ErrorMessage ?? string.Empty, 40),
        new("Export.Column.SignatureError", (result, _) => result.SignatureError ?? string.Empty, 45),
        new("Export.Column.Warning", (result, _) => result.SignatureWarning ?? string.Empty, 45),
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
            signature.IsPrimary
                ? Loc.T("Signature.Role.Primary")
                : signature.IsNested
                    ? Loc.T("Signature.Role.Nested")
                    : Loc.T("Signature.Role.Secondary"),
            signature.StatusText ?? string.Empty,
        };

        if (signature.Certificate is not null)
        {
            parts.Add($"{Loc.T("Copy.Label.Signer")}={signature.Certificate.SubjectName}");
            parts.Add($"{Loc.T("Copy.Label.Issuer")}={signature.Certificate.IssuerName}");
            parts.Add($"{Loc.T("Copy.Label.Validity")}={signature.Certificate.ValidityText}");
        }

        if (!string.IsNullOrEmpty(signature.SignatureAlgorithm))
        {
            parts.Add(
                $"{Loc.T("Copy.Label.Algorithm")}={signature.SignatureAlgorithm}/{signature.DigestAlgorithm}");
        }

        if (signature.Timestamp is not null)
        {
            parts.Add($"{Loc.T("Copy.Label.TimestampSigner")}={signature.Timestamp.SignerName}");
            parts.Add($"{Loc.T("Copy.Label.TimestampTime")}={signature.Timestamp.TimestampText}");
        }

        if (!string.IsNullOrEmpty(signature.ErrorMessage))
        {
            parts.Add($"{Loc.T("Copy.Label.Error")}={signature.ErrorMessage}");
        }

        return string.Join("; ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static string FormatTime(DateTime value) =>
        value == DateTime.MinValue ? string.Empty : value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
}
