using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using Sigwalk.Core.Localization;
using Sigwalk.Core.Models;
using Sigwalk.Core.Services.Export;
using Sigwalk.Tests.TestHelpers;

namespace Sigwalk.Tests;

[Collection("Localization")]
public class ExportReportTests
{
    private const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    [Fact]
    public void AllScopeIncludesFiveGroupsAndConsistentSummary()
    {
        var results = new[]
        {
            Sample("valid.exe", SignatureStatus.Valid),
            Sample("unsigned.exe", SignatureStatus.Unsigned),
            Sample("invalid.exe", SignatureStatus.Invalid),
            Sample("unknown.exe", SignatureStatus.Unknown),
            Failed("missing.exe"),
        };

        var document = ExportDocumentBuilder.Build(results, ExportContentScope.All);

        Assert.Equal(
            new[]
            {
                ExportGroupKind.Valid,
                ExportGroupKind.Unsigned,
                ExportGroupKind.Invalid,
                ExportGroupKind.Unknown,
                ExportGroupKind.Failed,
            },
            document.Groups.Select(group => group.Kind));
        Assert.All(document.Groups, group => Assert.Equal(1, group.Count));
        Assert.Equal(5, document.Summary.Total);
        Assert.Equal(4, document.Summary.Completed);
        Assert.Equal(1, document.Summary.Valid);
        Assert.Equal(1, document.Summary.Unsigned);
        Assert.Equal(1, document.Summary.Invalid);
        Assert.Equal(1, document.Summary.Unknown);
        Assert.Equal(1, document.Summary.Failed);
        Assert.Equal(ExportVerdict.IssuesFound, document.Summary.Result);
    }

    [Fact]
    public void CompactScopeExcludesValidButKeepsZeroGroups()
    {
        var results = new[] { Sample("valid.exe", SignatureStatus.Valid) };

        var document = ExportDocumentBuilder.Build(results, ExportContentScope.Compact);

        Assert.Equal(
            new[]
            {
                ExportGroupKind.Unsigned,
                ExportGroupKind.Invalid,
                ExportGroupKind.Unknown,
                ExportGroupKind.Failed,
            },
            document.Groups.Select(group => group.Kind));
        Assert.All(document.Groups, group => Assert.Equal(0, group.Count));
        Assert.Equal(1, document.Summary.Valid);
        Assert.Equal(ExportVerdict.Ok, document.Summary.Result);
    }

    [Fact]
    public void FailedTakesPrecedenceAndUncheckedCountsAsUnknown()
    {
        var failed = Sample(
            "failed.exe",
            SignatureStatus.Valid,
            ScanStatus.Failed,
            signatureChecked: false,
            error: "boom");
        var uncheckedResult = Sample(
            "hash-only.exe",
            SignatureStatus.Valid,
            signatureChecked: false);

        var document = ExportDocumentBuilder.Build(
            new[] { failed, uncheckedResult },
            ExportContentScope.All);

        Assert.Equal(ExportGroupKind.Failed, ExportDocumentBuilder.Classify(failed));
        Assert.Equal(ExportGroupKind.Unknown, ExportDocumentBuilder.Classify(uncheckedResult));
        Assert.Equal(1, document.Summary.Failed);
        Assert.Equal(1, document.Summary.Unknown);
        Assert.Equal(1, document.Summary.Completed);
    }

    [Fact]
    public void CsvReportContainsSummaryGroupsAndNoneMarker()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "report.csv");
        var results = new[]
        {
            Sample("valid.exe", SignatureStatus.Valid),
            Sample("unsigned.exe", SignatureStatus.Unsigned),
        };

        new ExportService().Export(path, ExportFormat.Csv, results, ExportContentScope.Compact);
        var text = File.ReadAllText(path);

        Assert.StartsWith("检测概况", text.TrimStart('\uFEFF'));
        Assert.Contains("检测总数,2", text);
        Assert.Contains("未签名数量,1", text);
        Assert.Contains("无效数量,0", text);
        Assert.Contains("未签名（1）", text);
        Assert.Contains("无效（无）", text);
        Assert.Contains("无", text);
        Assert.Contains("序号,文件名称", text);
    }

    [Fact]
    public void JsonReportUsesSchemaTwoAndLoadsBackAsItems()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "report.json");
        var results = new[]
        {
            Sample("valid.exe", SignatureStatus.Valid),
            Sample("unsigned.exe", SignatureStatus.Unsigned),
        };

        new ExportService().Export(path, ExportFormat.Json, results, ExportContentScope.Compact);

        using (var json = JsonDocument.Parse(File.ReadAllText(path)))
        {
            var root = json.RootElement;
            Assert.Equal(2, root.GetProperty("SchemaVersion").GetInt32());
            Assert.Equal("Compact", root.GetProperty("Content").GetString());
            Assert.Equal(2, root.GetProperty("Summary").GetProperty("Total").GetInt32());
            var groups = root.GetProperty("Groups").EnumerateArray().ToList();
            Assert.Equal(4, groups.Count);
            Assert.DoesNotContain(
                groups,
                group => group.GetProperty("Kind").GetString() == "Valid");
        }

        // Compact 导出刻意省略有效文件，导入后只恢复被导出的分组内容。
        var loaded = new ResultSerializer().Load(path);
        Assert.Single(loaded.Items);
        Assert.Equal("unsigned.exe", loaded.Items[0].FileName);
    }

    [Fact]
    public void XlsxReportHasSummaryAndGroupRows()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "report.xlsx");
        var results = new[] { Sample("valid.exe", SignatureStatus.Valid) };

        new ExportService().Export(path, ExportFormat.Xlsx, results, ExportContentScope.Compact);

        using var archive = ZipFile.OpenRead(path);
        using var stream = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var document = XDocument.Load(stream);
        var values = document.Descendants(XName.Get("t", MainNs)).Select(element => element.Value).ToList();

        Assert.Contains("检测概况", values);
        Assert.Contains("检测总数", values);
        Assert.Contains("未签名（无）", values);
    }

    [Fact]
    public void EnglishExportUsesEnglishHeaders()
    {
        var original = Loc.Language;
        try
        {
            Loc.SetLanguage(AppLanguage.English);
            using var directory = new TempDirectory();
            var path = Path.Combine(directory.Path, "report.csv");
            var results = new[] { Sample("valid.exe", SignatureStatus.Valid) };

            new ExportService().Export(path, ExportFormat.Csv, results, ExportContentScope.Compact);
            var text = File.ReadAllText(path);

            Assert.Contains("Detection Summary", text);
            Assert.Contains("Unsigned (None)", text);
            Assert.Contains("Result", text);
        }
        finally
        {
            Loc.SetLanguage(original);
        }
    }

    private static FileScanResult Sample(
        string fileName,
        SignatureStatus status,
        ScanStatus scanStatus = ScanStatus.Completed,
        bool signatureChecked = true,
        string? error = null)
    {
        var now = DateTime.UtcNow;
        return new FileScanResult
        {
            FilePath = @"C:\Tools\" + fileName,
            FileName = fileName,
            FileType = FileScanResult.GetFileType(fileName),
            SizeBytes = 1234,
            LastWriteTime = now,
            ScanStatus = scanStatus,
            SignatureChecked = signatureChecked,
            SignatureStatus = status,
            ErrorMessage = error,
            HashChecked = true,
            Sha1 = "A9993E364706816ABA3E25717850C26C9CD0D89D",
            Sha256 = "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD",
            ScannedAt = now,
        };
    }

    private static FileScanResult Failed(string fileName) =>
        Sample(fileName, SignatureStatus.Unknown, ScanStatus.Failed, signatureChecked: false, error: "文件不存在");
}
