namespace Sigwalk.Core.Models;

/// <summary>扫描配置。</summary>
public sealed class ScanOptions
{
    /// <summary>默认扫描的可执行文件类型。</summary>
    public const string DefaultExtensions = "exe;dll;sys;msi;ocx;cpl;scr;com";

    /// <summary>是否递归扫描子目录。</summary>
    public bool Recursive { get; set; } = true;

    /// <summary>目录扫描时的扩展名过滤（分号/逗号/空格分隔，空表示全部文件）。</summary>
    public string Extensions { get; set; } = DefaultExtensions;

    /// <summary>并发处理文件数。</summary>
    public int MaxConcurrency { get; set; } = DefaultConcurrency;

    public ScanMode ScanMode { get; set; } = ScanMode.Full;

    public RevocationMode RevocationMode { get; set; } = RevocationMode.None;

    /// <summary>允许的最大文件字节数，0 表示不限制。</summary>
    public long MaxFileSizeBytes { get; set; }

    public static int DefaultConcurrency => Math.Clamp(Environment.ProcessorCount, 2, 8);

    public ScanOptions Clone() => new()
    {
        Recursive = Recursive,
        Extensions = Extensions,
        MaxConcurrency = MaxConcurrency,
        ScanMode = ScanMode,
        RevocationMode = RevocationMode,
        MaxFileSizeBytes = MaxFileSizeBytes,
    };

    /// <summary>解析扩展名过滤字符串；返回空集合表示不过滤。</summary>
    public static HashSet<string> ParseExtensions(string? value)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value))
        {
            return result;
        }

        foreach (var raw in value.Split(new[] { ';', ',', ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var item = raw.Trim();
            if (item.StartsWith("*.", StringComparison.Ordinal))
            {
                item = item[2..];
            }
            item = item.TrimStart('.');
            if (item.Length > 0)
            {
                result.Add(item);
            }
        }

        return result;
    }

    /// <summary>判断扩展名是否符合扫描过滤条件。</summary>
    public static bool IsExtensionIncluded(string filePath, string? extensionFilter)
    {
        var set = ParseExtensions(extensionFilter);
        if (set.Count == 0)
        {
            return true;
        }

        var ext = Path.GetExtension(filePath).TrimStart('.');
        return ext.Length > 0 && set.Contains(ext);
    }
}
