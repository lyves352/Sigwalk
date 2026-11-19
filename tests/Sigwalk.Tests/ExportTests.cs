using System.IO.Compression;
using System.Xml.Linq;
using Sigwalk.Core.Models;
using Sigwalk.Core.Services.Export;
using Sigwalk.Tests.TestHelpers;

namespace Sigwalk.Tests;

public class ExportTests
{
    private const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    [Fact]
    public void CsvHasBomHeaderAndQuotedFields()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "result.csv");
        var results = new[] { Sample("a.exe", SignatureStatus.Valid), Sample("b.dll", SignatureStatus.Unsigned) };

        new ExportService().Export(path, ExportFormat.Csv, results);

        var bytes = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());

        var text = File.ReadAllText(path);
        Assert.Contains("序号", text);
        Assert.Contains("SHA256", text);
        Assert.Contains("\"Contoso, Inc.\"", text);
        Assert.Equal(3, text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Contains("A9993E364706816ABA3E25717850C26C9CD0D89D", text);
    }

    [Fact]
    public void JsonRoundTripPreservesSignaturesAndHashes()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "result.json");
        var result = Sample("a.exe", SignatureStatus.Valid);
        var serializer = new ResultSerializer();

        serializer.Save(path, new[] { result });
        var document = serializer.Load(path);

        Assert.Single(document.Items);
        Assert.Equal(1, document.Summary.Total);
        Assert.Equal(SignatureStatus.Valid, document.Items[0].SignatureStatus);
        Assert.Equal(result.Sha1, document.Items[0].Sha1);
        Assert.Equal(result.Sha256, document.Items[0].Sha256);
        Assert.Single(document.Items[0].Signatures);
        Assert.Equal("Contoso, Inc.", document.Items[0].Signatures[0].Certificate?.SubjectName);
        Assert.Equal("RFC3161", document.Items[0].Signatures[0].Timestamp?.Type);

        var json = File.ReadAllText(path);
        Assert.Contains("\"SchemaVersion\": 1", json);
        Assert.Contains("\"Valid\"", json);
    }

    [Fact]
    public void XlsxHasRequiredPartsAndCellValues()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "result.xlsx");
        var results = new[] { Sample("a.exe", SignatureStatus.Valid), Sample("b.dll", SignatureStatus.Unsigned) };

        new ExportService().Export(path, ExportFormat.Xlsx, results);

        using var archive = ZipFile.OpenRead(path);
        var entries = archive.Entries.Select(e => e.FullName).ToList();
        Assert.Contains("[Content_Types].xml", entries);
        Assert.Contains("_rels/.rels", entries);
        Assert.Contains("xl/workbook.xml", entries);
        Assert.Contains("xl/_rels/workbook.xml.rels", entries);
        Assert.Contains("xl/styles.xml", entries);
        Assert.Contains("xl/worksheets/sheet1.xml", entries);

        var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml")!;
        using var stream = sheetEntry.Open();
        var document = XDocument.Load(stream);
        XNamespace ns = MainNs;

        var rows = document.Descendants(ns + "row").ToList();
        Assert.Equal(3, rows.Count);

        var values = document.Descendants(ns + "t").Select(e => e.Value).ToList();
        Assert.Contains("文件名称", values);
        Assert.Contains("a.exe", values);
        Assert.Contains(results[0].Sha256, values);
        Assert.Contains("签名有效", values);
        Assert.Contains("未签名", values);
    }

    [Fact]
    public void XlsxSanitizesIllegalXmlCharacters()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "control.xlsx");
        var result = Sample("a\u0001b.exe", SignatureStatus.Valid);

        new ExportService().Export(path, ExportFormat.Xlsx, new[] { result });

        using var archive = ZipFile.OpenRead(path);
        using var stream = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var document = XDocument.Load(stream);
        var values = document.Descendants(XName.Get("t", MainNs)).Select(e => e.Value).ToList();
        Assert.Contains("a b.exe", values);
    }

    [Fact]
    public void XmlSanitizerLimitsLengthAndReplacesControlCharacters()
    {
        Assert.Equal("A B C", XmlTextSanitizer.Clean("A\u0001B\u0000C"));
        Assert.Equal("abc", XmlTextSanitizer.Clean("abcdef", 3));
    }

    [Fact]
    public void XlsxRejectsMoreRowsThanExcelLimit()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "too-many.xlsx");

        Assert.Throws<InvalidOperationException>(
            () => new XlsxExporter().Write(path, new CountOnlyList(1_048_576)));
    }

    [Theory]
    [InlineData("a.csv", ExportFormat.Csv)]
    [InlineData("a.XLSX", ExportFormat.Xlsx)]
    [InlineData("a.json", ExportFormat.Json)]
    public void DetectsFormatFromExtension(string file, ExportFormat expected)
    {
        Assert.Equal(expected, ExportService.DetectFormat(file));
    }

    private sealed class CountOnlyList : IReadOnlyList<FileScanResult>
    {
        public CountOnlyList(int count)
        {
            Count = count;
        }

        public int Count { get; }

        public FileScanResult this[int index] => throw new InvalidOperationException("不应读取元素");

        public IEnumerator<FileScanResult> GetEnumerator() => throw new InvalidOperationException("不应枚举元素");

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static FileScanResult Sample(string fileName, SignatureStatus status)
    {
        var now = DateTime.UtcNow;
        var certificate = new CertificateInfo
        {
            Subject = "CN=Contoso, Inc.",
            SubjectName = "Contoso, Inc.",
            Issuer = "CN=Test CA",
            IssuerName = "Test CA",
            SerialNumber = "0102",
            Thumbprint = "ABCDEF1234567890",
            NotBefore = now.AddDays(-10),
            NotAfter = now.AddDays(10),
            SignatureAlgorithm = "SHA256-RSA",
            ChainStatusText = "证书链验证通过",
            RevocationStatus = RevocationStatus.NotChecked,
        };

        return new FileScanResult
        {
            FilePath = @"C:\Tools\" + fileName,
            FileName = fileName,
            FileType = FileScanResult.GetFileType(fileName),
            SizeBytes = 1_234_567,
            LastWriteTime = now,
            ScanStatus = ScanStatus.Completed,
            SignatureChecked = true,
            SignatureStatus = status,
            HashChecked = true,
            Sha1 = "A9993E364706816ABA3E25717850C26C9CD0D89D",
            Sha256 = "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD",
            ScannedAt = now,
            VersionInfo = new FileVersionData
            {
                CompanyName = "Contoso",
                ProductName = "Contoso App",
                FileVersion = "1.2.3.4",
            },
            Signatures = new List<SignatureInfo>
            {
                new()
                {
                    Index = 0,
                    IsPrimary = true,
                    IsVerified = true,
                    Status = status,
                    StatusText = status == SignatureStatus.Valid ? "签名有效" : "未签名",
                    Certificate = certificate,
                    Timestamp = new TimestampInfo
                    {
                        Timestamp = now,
                        Signer = certificate,
                        Type = "RFC3161",
                    },
                    SignatureAlgorithm = "SHA256-RSA",
                    DigestAlgorithm = "SHA256",
                    Chain = new List<ChainElementInfo>
                    {
                        new()
                        {
                            Index = 0,
                            Subject = "CN=Contoso, Inc.",
                            Issuer = "CN=Test CA",
                            StatusText = "正常",
                            NotBefore = now.AddDays(-10),
                            NotAfter = now.AddDays(10),
                        },
                    },
                },
            },
        };
    }
}
