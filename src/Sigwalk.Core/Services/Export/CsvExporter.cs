using System.Text;
using Sigwalk.Core.Models;

namespace Sigwalk.Core.Services.Export;

/// <summary>导出为 UTF-8（带 BOM）、CRLF 的 CSV，便于 Excel 正确识别中文。</summary>
public sealed class CsvExporter
{
    /// <summary>扁平导出（兼容旧行为）：表头 + 数据行。</summary>
    public void Write(string filePath, IReadOnlyList<FileScanResult> results)
    {
        using var writer = CreateWriter(filePath);
        writer.WriteLine(string.Join(",", ExportColumns.All.Select(column => Escape(column.Header))));

        for (var index = 0; index < results.Count; index++)
        {
            WriteDataRow(writer, results[index], index);
        }
    }

    /// <summary>分组导出：检测概况 + 按固定顺序的明细分组。</summary>
    public void WriteReport(string filePath, ExportDocument document)
    {
        using var writer = CreateWriter(filePath);

        foreach (var row in ExportReportRows.Build(document))
        {
            switch (row.Kind)
            {
                case ExportRowKind.Blank:
                    writer.WriteLine();
                    break;
                case ExportRowKind.SummaryPair:
                    writer.WriteLine($"{Escape(row.Label)},{Escape(row.Value)}");
                    break;
                case ExportRowKind.SectionTitle:
                case ExportRowKind.GroupTitle:
                case ExportRowKind.EmptyGroup:
                    writer.WriteLine(Escape(row.Label));
                    break;
                case ExportRowKind.ColumnHeader:
                    writer.WriteLine(string.Join(",", ExportColumns.All.Select(column => Escape(column.Header))));
                    break;
                case ExportRowKind.DataRow when row.Result is not null:
                    WriteDataRow(writer, row.Result, row.Index);
                    break;
            }
        }
    }

    private static StreamWriter CreateWriter(string filePath) =>
        new(
            filePath,
            append: false,
            encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: true))
        {
            NewLine = "\r\n",
        };

    private static void WriteDataRow(StreamWriter writer, FileScanResult result, int index)
    {
        writer.WriteLine(string.Join(
            ",",
            ExportColumns.All.Select(column => Escape(column.Value(result, index)))));
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
