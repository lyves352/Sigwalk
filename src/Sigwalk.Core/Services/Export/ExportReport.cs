using Sigwalk.Core.Localization;
using Sigwalk.Core.Models;

namespace Sigwalk.Core.Services.Export;

/// <summary>导出文档（JSON schema v2）：概况 + 分类分组明细。</summary>
public sealed class ExportDocument
{
    public int SchemaVersion { get; set; } = 2;
    public string ToolVersion { get; set; } = "1.0.0";
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public ExportContentScope Content { get; set; }
    public ExportSummary Summary { get; set; } = new();
    public List<ExportGroup> Groups { get; set; } = new();
}

/// <summary>检测概况。五个签名分类互不重叠，且与 Failed 一起覆盖全部结果。</summary>
public sealed class ExportSummary
{
    public ExportVerdict Result { get; set; }
    public string ResultText { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Completed { get; set; }
    public int Valid { get; set; }
    public int Unsigned { get; set; }
    public int Invalid { get; set; }
    public int Unknown { get; set; }
    public int Failed { get; set; }
}

public sealed class ExportGroup
{
    public ExportGroupKind Kind { get; set; }
    public int Count => Files.Count;
    public List<FileScanResult> Files { get; set; } = new();
}

/// <summary>按固定顺序构建导出文档；零条分组也保留。</summary>
public static class ExportDocumentBuilder
{
    private static readonly ExportGroupKind[] AllGroups =
    {
        ExportGroupKind.Valid,
        ExportGroupKind.Unsigned,
        ExportGroupKind.Invalid,
        ExportGroupKind.Unknown,
        ExportGroupKind.Failed,
    };

    private static readonly ExportGroupKind[] CompactGroups =
    {
        ExportGroupKind.Unsigned,
        ExportGroupKind.Invalid,
        ExportGroupKind.Unknown,
        ExportGroupKind.Failed,
    };

    public static ExportDocument Build(IReadOnlyList<FileScanResult> results, ExportContentScope content)
    {
        var summary = new ExportSummary { Total = results.Count };
        var buckets = AllGroups.ToDictionary(kind => kind, _ => new List<FileScanResult>());

        foreach (var result in results)
        {
            if (result.ScanStatus == ScanStatus.Completed)
            {
                summary.Completed++;
            }

            var kind = Classify(result);
            buckets[kind].Add(result);
            switch (kind)
            {
                case ExportGroupKind.Valid:
                    summary.Valid++;
                    break;
                case ExportGroupKind.Unsigned:
                    summary.Unsigned++;
                    break;
                case ExportGroupKind.Invalid:
                    summary.Invalid++;
                    break;
                case ExportGroupKind.Unknown:
                    summary.Unknown++;
                    break;
                case ExportGroupKind.Failed:
                    summary.Failed++;
                    break;
            }
        }

        var issues = summary.Unsigned + summary.Invalid + summary.Unknown + summary.Failed > 0;
        summary.Result = issues ? ExportVerdict.IssuesFound : ExportVerdict.Ok;
        summary.ResultText = Loc.T(issues ? "Export.Result.Issues" : "Export.Result.Ok");

        var selectedGroups = content == ExportContentScope.Compact ? CompactGroups : AllGroups;
        var groups = selectedGroups
            .Select(kind => new ExportGroup { Kind = kind, Files = buckets[kind] })
            .ToList();

        return new ExportDocument
        {
            SchemaVersion = 2,
            ToolVersion = typeof(ExportDocumentBuilder).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            ExportedAt = DateTime.UtcNow,
            Content = content,
            Summary = summary,
            Groups = groups,
        };
    }

    public static ExportGroupKind Classify(FileScanResult result)
    {
        if (result.ScanStatus == ScanStatus.Failed)
        {
            return ExportGroupKind.Failed;
        }

        // 未完成或未做签名检查的行无法归入签名状态，统一计入 Unknown。
        if (result.ScanStatus != ScanStatus.Completed || !result.SignatureChecked)
        {
            return ExportGroupKind.Unknown;
        }

        return result.SignatureStatus switch
        {
            SignatureStatus.Valid => ExportGroupKind.Valid,
            SignatureStatus.Unsigned => ExportGroupKind.Unsigned,
            SignatureStatus.Invalid => ExportGroupKind.Invalid,
            _ => ExportGroupKind.Unknown,
        };
    }

    public static string DescribeGroup(ExportGroupKind kind) => kind switch
    {
        ExportGroupKind.Valid => Loc.T("Export.Group.Valid"),
        ExportGroupKind.Unsigned => Loc.T("Export.Group.Unsigned"),
        ExportGroupKind.Invalid => Loc.T("Export.Group.Invalid"),
        ExportGroupKind.Failed => Loc.T("Export.Group.Failed"),
        _ => Loc.T("Export.Group.Unknown"),
    };
}
