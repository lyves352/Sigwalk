using System.Text;
using Sigwalk.Core.Models;

namespace Sigwalk.Core.Services.Export;

/// <summary>导出为 UTF-8（带 BOM）、CRLF 的 CSV，便于 Excel 正确识别中文。</summary>
public sealed class CsvExporter
{
    public void Write(string filePath, IReadOnlyList<FileScanResult> results)
    {
        using var writer = new StreamWriter(
            filePath,
            append: false,
            encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: true))
        {
            NewLine = "\r\n",
        };

        writer.WriteLine(string.Join(",", ExportColumns.All.Select(column => Escape(column.Header))));

        for (var index = 0; index < results.Count; index++)
        {
            var result = results[index];
            writer.WriteLine(string.Join(",", ExportColumns.All.Select(column => Escape(column.Value(result, index)))));
        }
    }

    private static string Escape(string? value)
    {
        value ??= string.Empty;
        var needsQuote = value.Contains('"') ||
                         value.Contains(',') ||
                         value.Contains('\r') ||
                         value.Contains('\n') ||
                         value.StartsWith(' ') ||
                         value.EndsWith(' ');

        return needsQuote ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
