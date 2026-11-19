using Sigwalk.Core.Localization;
using Sigwalk.Core.Models;

namespace Sigwalk.Core.Services.Export;

/// <summary>导出入口：根据格式选择 CSV / XLSX / JSON。</summary>
public sealed class ExportService
{
    /// <summary>本地化的导出文件对话框过滤器（当前格式优先由调用方重排）。</summary>
    public static string GetFileFilter() => Loc.T("Dialog.Filter.Export");

    /// <summary>兼容旧行为的扁平导出。</summary>
    public void Export(string filePath, ExportFormat format, IReadOnlyList<FileScanResult> results)
    {
        format = NormalizeFormat(format);
        var fullPath = Prepare(filePath);
        switch (format)
        {
            case ExportFormat.Csv:
                new CsvExporter().Write(fullPath, results);
                break;
            case ExportFormat.Xlsx:
                new XlsxExporter().Write(fullPath, results);
                break;
            case ExportFormat.Json:
                new ResultSerializer().Save(fullPath, results);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, Loc.T("Error.ExportFormat"));
        }
    }

    /// <summary>概况 + 分类分组导出（GUI 与 CLI 使用）。</summary>
    public void Export(
        string filePath,
        ExportFormat format,
        IReadOnlyList<FileScanResult> results,
        ExportContentScope content)
    {
        format = NormalizeFormat(format);
        var fullPath = Prepare(filePath);
        var document = ExportDocumentBuilder.Build(results, content);

        switch (format)
        {
            case ExportFormat.Csv:
                new CsvExporter().WriteReport(fullPath, document);
                break;
            case ExportFormat.Xlsx:
                new XlsxExporter().WriteReport(fullPath, document);
                break;
            case ExportFormat.Json:
                ExportJsonWriter.Write(fullPath, document);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, Loc.T("Error.ExportFormat"));
        }
    }

    public static ExportFormat DetectFormat(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".csv" => ExportFormat.Csv,
            ".xlsx" => ExportFormat.Xlsx,
            ".json" => ExportFormat.Json,
            _ => ExportFormat.Csv,
        };
    }

    public static string GetExtension(ExportFormat format) => format switch
    {
        ExportFormat.Xlsx => ".xlsx",
        ExportFormat.Json => ".json",
        _ => ".csv",
    };

    private static ExportFormat NormalizeFormat(ExportFormat format) =>
        format is ExportFormat.Csv or ExportFormat.Xlsx or ExportFormat.Json
            ? format
            : ExportFormat.Csv;

    private static string Prepare(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return fullPath;
    }
}
