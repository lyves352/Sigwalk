namespace Sigwalk.Core.Models;

/// <summary>列表过滤条件。过滤只影响显示，不修改原始结果。</summary>
public sealed class FileFilter
{
    public SignatureFilter StatusFilter { get; set; } = SignatureFilter.All;

    /// <summary>统一搜索文本：文件名、完整路径、签名者、SHA1、SHA256。</summary>
    public string? SearchText { get; set; }

    /// <summary>显示用扩展名过滤（分号/逗号/空格分隔，空表示全部）。</summary>
    public string? ExtensionFilter { get; set; }

    public bool IsActive =>
        StatusFilter != SignatureFilter.All ||
        !string.IsNullOrWhiteSpace(SearchText) ||
        !string.IsNullOrWhiteSpace(ExtensionFilter);

    public bool Matches(FileScanResult result)
    {
        if (!MatchesStatus(result))
        {
            return false;
        }

        if (!MatchesExtension(result))
        {
            return false;
        }

        if (!MatchesSearch(result))
        {
            return false;
        }

        return true;
    }

    private bool MatchesStatus(FileScanResult result)
    {
        return StatusFilter switch
        {
            SignatureFilter.All => true,
            SignatureFilter.ScanFailed => result.ScanStatus == ScanStatus.Failed,
            SignatureFilter.Valid => IsSignature(result, SignatureStatus.Valid),
            SignatureFilter.Unsigned => IsSignature(result, SignatureStatus.Unsigned),
            SignatureFilter.Invalid => IsSignature(result, SignatureStatus.Invalid),
            SignatureFilter.Unknown => IsSignature(result, SignatureStatus.Unknown),
            _ => true,
        };
    }

    private static bool IsSignature(FileScanResult result, SignatureStatus status)
    {
        // 扫描失败的文件由“扫描失败”单独筛选，避免与签名状态计数重复。
        return result.ScanStatus != ScanStatus.Failed &&
               result.SignatureChecked &&
               result.SignatureStatus == status;
    }

    private bool MatchesExtension(FileScanResult result)
    {
        if (string.IsNullOrWhiteSpace(ExtensionFilter))
        {
            return true;
        }

        var set = ScanOptions.ParseExtensions(ExtensionFilter);
        if (set.Count == 0)
        {
            return true;
        }

        var ext = Path.GetExtension(result.FilePath).TrimStart('.');
        return ext.Length > 0 && set.Contains(ext);
    }

    private bool MatchesSearch(FileScanResult result)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        var text = SearchText.Trim();
        if (Contains(result.FileName, text) ||
            Contains(result.FilePath, text) ||
            Contains(result.Sha1, text) ||
            Contains(result.Sha256, text))
        {
            return true;
        }

        foreach (var signature in result.Signatures)
        {
            var cert = signature.Certificate;
            if (cert is null)
            {
                continue;
            }

            if (Contains(cert.SubjectName, text) ||
                Contains(cert.Subject, text) ||
                Contains(cert.IssuerName, text) ||
                Contains(cert.Issuer, text) ||
                Contains(signature.Timestamp?.SignerName, text))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Contains(string? source, string value) =>
        !string.IsNullOrEmpty(source) &&
        source.Contains(value, StringComparison.OrdinalIgnoreCase);
}
