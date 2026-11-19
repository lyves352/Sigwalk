using Sigwalk.Core.Models;

namespace Sigwalk.Tests;

public class FileFilterTests
{
    [Fact]
    public void StatusFiltersAreMutuallyExclusive()
    {
        var filter = new FileFilter { StatusFilter = SignatureFilter.Valid };

        Assert.True(filter.Matches(Create("C:\\a.exe", SignatureStatus.Valid)));
        Assert.False(filter.Matches(Create("C:\\b.exe", SignatureStatus.Unsigned)));
        Assert.False(filter.Matches(Create("C:\\c.exe", SignatureStatus.Invalid)));
        Assert.False(filter.Matches(Create("C:\\d.exe", SignatureStatus.Unknown)));
    }

    [Fact]
    public void FailedFilesAreOnlySelectedByScanFailedFilter()
    {
        var failed = new FileScanResult
        {
            FilePath = "C:\\bad.exe",
            FileName = "bad.exe",
            ScanStatus = ScanStatus.Failed,
            ErrorMessage = "读取失败",
        };

        Assert.True(new FileFilter { StatusFilter = SignatureFilter.ScanFailed }.Matches(failed));
        Assert.False(new FileFilter { StatusFilter = SignatureFilter.Unknown }.Matches(failed));
        Assert.False(new FileFilter { StatusFilter = SignatureFilter.Valid }.Matches(failed));
    }

    [Fact]
    public void HashOnlyResultsAreNotCountedAsUnknownSignature()
    {
        var hashOnly = new FileScanResult
        {
            FilePath = "C:\\hash.exe",
            ScanStatus = ScanStatus.Completed,
            SignatureChecked = false,
            SignatureStatus = SignatureStatus.Unknown,
        };

        Assert.False(new FileFilter { StatusFilter = SignatureFilter.Unknown }.Matches(hashOnly));
        Assert.True(new FileFilter { StatusFilter = SignatureFilter.All }.Matches(hashOnly));
    }

    [Fact]
    public void SearchesNamePathSignerHashAndSha()
    {
        var result = Create("C:\\Tools\\Contoso\\app.exe", SignatureStatus.Valid, signer: "Contoso Corporation");

        Assert.True(new FileFilter { SearchText = "app.exe" }.Matches(result));
        Assert.True(new FileFilter { SearchText = "Contoso" }.Matches(result));
        Assert.True(new FileFilter { SearchText = "Tools" }.Matches(result));
        Assert.True(new FileFilter { SearchText = "deadbeef" }.Matches(result));
        Assert.True(new FileFilter { SearchText = "cafebabe" }.Matches(result));
        Assert.False(new FileFilter { SearchText = "missing" }.Matches(result));
    }

    [Fact]
    public void ExtensionFilterIgnoresCaseAndLeadingDot()
    {
        var exe = Create("C:\\a.EXE", SignatureStatus.Valid);
        var dll = Create("C:\\a.dll", SignatureStatus.Valid);

        var filter = new FileFilter { ExtensionFilter = ".exe; .sys" };

        Assert.True(filter.Matches(exe));
        Assert.False(filter.Matches(dll));
    }

    [Fact]
    public void CombinesMultipleConditions()
    {
        var result = Create("C:\\Tools\\a.exe", SignatureStatus.Valid, signer: "Contoso");
        var filter = new FileFilter
        {
            StatusFilter = SignatureFilter.Valid,
            SearchText = "contoso",
            ExtensionFilter = "exe",
        };

        Assert.True(filter.Matches(result));

        filter.SearchText = "Fabrikam";
        Assert.False(filter.Matches(result));
    }

    private static FileScanResult Create(
        string path,
        SignatureStatus status,
        string signer = "Test Signer",
        bool signatureChecked = true)
    {
        return new FileScanResult
        {
            FilePath = path,
            FileName = Path.GetFileName(path),
            FileType = FileScanResult.GetFileType(path),
            SizeBytes = 1024,
            LastWriteTime = DateTime.UtcNow,
            ScanStatus = ScanStatus.Completed,
            SignatureChecked = signatureChecked,
            SignatureStatus = status,
            Sha1 = "DEADBEEF00112233445566778899AABBCCDDEEFF",
            Sha256 = "CAFEBABE00112233445566778899AABBCCDDEEFF00112233445566778899AABB",
            Signatures = new List<SignatureInfo>
            {
                new()
                {
                    Index = 0,
                    IsPrimary = true,
                    IsVerified = true,
                    Status = status,
                    Certificate = new CertificateInfo
                    {
                        SubjectName = signer,
                        Subject = "CN=" + signer,
                        IssuerName = "Test CA",
                        Issuer = "CN=Test CA",
                    },
                },
            },
        };
    }
}
