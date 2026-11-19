using Sigwalk.Core.Models;

namespace Sigwalk.Core.Services.Export;

/// <summary>导出入口：根据格式选择 CSV / XLSX / JSON。</summary>
public sealed class ExportService
{
    public const string FileFilter =
        "CSV 文件 (*.csv)|*.csv|Excel 工作簿 (*.xlsx)|*.xlsx|JSON 文件 (*.json)|*.json";

    public void Export(string filePath, ExportFormat format, IReadOnlyList<FileScanResult> results)
    {
        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

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
                throw new ArgumentOutOfRangeException(nameof(format), format, "不支持的导出格式");
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
}
