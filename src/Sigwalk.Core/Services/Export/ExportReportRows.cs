using Sigwalk.Core.Localization;
using Sigwalk.Core.Models;

namespace Sigwalk.Core.Services.Export;

internal enum ExportRowKind
{
    Blank,
    SectionTitle,
    SummaryPair,
    GroupTitle,
    ColumnHeader,
    EmptyGroup,
    DataRow,
}

internal sealed record ExportRow(
    ExportRowKind Kind,
    string? Label = null,
    string? Value = null,
    FileScanResult? Result = null,
    int Index = 0);

/// <summary>
/// 把导出文档渲染为统一的「概况 + 分组」行序列，CSV 与 XLSX 共用，
/// 保证两种格式的内容组织完全一致。
/// </summary>
internal static class ExportReportRows
{
    public static IReadOnlyList<ExportRow> Build(ExportDocument document)
    {
        var rows = new List<ExportRow>
        {
            new(ExportRowKind.SectionTitle, Loc.T("Export.Section.Summary")),
            new(ExportRowKind.SummaryPair, Loc.T("Export.Summary.Result"), document.Summary.ResultText),
            new(ExportRowKind.SummaryPair, Loc.T("Export.Summary.Total"), document.Summary.Total.ToString()),
            new(ExportRowKind.SummaryPair, Loc.T("Export.Summary.Completed"), document.Summary.Completed.ToString()),
            new(ExportRowKind.SummaryPair, Loc.T("Export.Summary.Valid"), document.Summary.Valid.ToString()),
            new(ExportRowKind.SummaryPair, Loc.T("Export.Summary.Unsigned"), document.Summary.Unsigned.ToString()),
            new(ExportRowKind.SummaryPair, Loc.T("Export.Summary.Invalid"), document.Summary.Invalid.ToString()),
            new(ExportRowKind.SummaryPair, Loc.T("Export.Summary.Unknown"), document.Summary.Unknown.ToString()),
            new(ExportRowKind.SummaryPair, Loc.T("Export.Summary.Failed"), document.Summary.Failed.ToString()),
            new(ExportRowKind.Blank),
            new(ExportRowKind.SectionTitle, Loc.T("Export.Section.Details")),
            new(ExportRowKind.Blank),
        };

        for (var groupIndex = 0; groupIndex < document.Groups.Count; groupIndex++)
        {
            var group = document.Groups[groupIndex];
            var countText = group.Count == 0
                ? Loc.T("Export.Group.Empty")
                : group.Count.ToString();
            rows.Add(new ExportRow(
                ExportRowKind.GroupTitle,
                Loc.F("Export.Group.Title", ExportDocumentBuilder.DescribeGroup(group.Kind), countText)));

            if (group.Count == 0)
            {
                rows.Add(new ExportRow(ExportRowKind.EmptyGroup, Loc.T("Export.Group.Empty")));
            }
            else
            {
                rows.Add(new ExportRow(ExportRowKind.ColumnHeader));
                for (var index = 0; index < group.Files.Count; index++)
                {
                    rows.Add(new ExportRow(ExportRowKind.DataRow, Result: group.Files[index], Index: index));
                }
            }

            if (groupIndex < document.Groups.Count - 1)
            {
                rows.Add(new ExportRow(ExportRowKind.Blank));
            }
        }

        return rows;
    }
}
