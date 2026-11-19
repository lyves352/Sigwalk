namespace Sigwalk.Core.Models;

/// <summary>持久化的用户配置。</summary>
public sealed class AppSettings
{
    public int Version { get; set; } = 1;
    public ThemeMode Theme { get; set; } = ThemeMode.Auto;
    public AppLanguage Language { get; set; } = AppLanguage.Chinese;
    public bool Recursive { get; set; } = true;
    public string Extensions { get; set; } = ScanOptions.DefaultExtensions;
    public int MaxConcurrency { get; set; } = ScanOptions.DefaultConcurrency;
    public ScanMode ScanMode { get; set; } = ScanMode.Full;
    public RevocationMode RevocationMode { get; set; } = RevocationMode.None;
    public bool AutoSortAfterScan { get; set; } = true;
    public ExportFormat ExportFormat { get; set; } = ExportFormat.Csv;

    /// <summary>导出内容：默认「精简」。</summary>
    public ExportContentScope ExportContent { get; set; } = ExportContentScope.Compact;

    /// <summary>隐藏的表格列键；空表示全部显示。</summary>
    public List<string> HiddenColumns { get; set; } = new();

    public string? LastDirectory { get; set; }

    public ScanOptions ToScanOptions() => new()
    {
        Recursive = Recursive,
        Extensions = Extensions,
        MaxConcurrency = Math.Clamp(MaxConcurrency, 1, 32),
        ScanMode = ScanMode,
        RevocationMode = RevocationMode,
    };

    public void Apply(ScanOptions options)
    {
        Recursive = options.Recursive;
        Extensions = options.Extensions;
        MaxConcurrency = Math.Clamp(options.MaxConcurrency, 1, 32);
        ScanMode = options.ScanMode;
        RevocationMode = options.RevocationMode;
    }
}
