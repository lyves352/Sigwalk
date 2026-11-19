using Sigwalk.Core.Models;
using Sigwalk.Core.Services;
using Sigwalk.Tests.TestHelpers;

namespace Sigwalk.Tests;

public class FileDiscoveryServiceTests
{
    [Fact]
    public void RecursiveScanFiltersExtensions()
    {
        using var directory = new TempDirectory();
        directory.CreateFile("a.exe", "a");
        directory.CreateFile("b.txt", "b");
        directory.CreateFile("sub/c.dll", "c");
        directory.CreateFile("sub/deep/d.sys", "d");

        var options = new ScanOptions { Recursive = true, Extensions = "exe;dll;sys" };
        var result = new FileDiscoveryService().Discover(new[] { directory.Path }, options);

        Assert.Empty(result.Errors);
        Assert.Equal(3, result.Files.Count);
        Assert.Contains(result.Files, f => f.EndsWith("a.exe", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Files, f => f.EndsWith("c.dll", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Files, f => f.EndsWith("d.sys", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Files, f => f.EndsWith("b.txt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NonRecursiveScanOnlyReadsRoot()
    {
        using var directory = new TempDirectory();
        directory.CreateFile("a.exe", "a");
        directory.CreateFile("sub/c.dll", "c");

        var options = new ScanOptions { Recursive = false, Extensions = "exe;dll" };
        var result = new FileDiscoveryService().Discover(new[] { directory.Path }, options);

        Assert.Single(result.Files);
        Assert.EndsWith("a.exe", result.Files[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EmptyExtensionFilterIncludesAllFiles()
    {
        using var directory = new TempDirectory();
        directory.CreateFile("a.exe", "a");
        directory.CreateFile("b.txt", "b");
        directory.CreateFile("sub/c.dll", "c");

        var options = new ScanOptions { Recursive = true, Extensions = string.Empty };
        var result = new FileDiscoveryService().Discover(new[] { directory.Path }, options);

        Assert.Equal(3, result.Files.Count);
    }

    [Fact]
    public void ExplicitFileBypassesExtensionFilter()
    {
        using var directory = new TempDirectory();
        var textFile = directory.CreateFile("b.txt", "b");
        var options = new ScanOptions { Extensions = "exe;dll" };

        var result = new FileDiscoveryService().Discover(new[] { textFile }, options);

        Assert.Single(result.Files);
        Assert.Equal(textFile, result.Files[0], ignoreCase: true);
    }

    [Fact]
    public void ReportsMissingPath()
    {
        using var directory = new TempDirectory();
        var missing = Path.Combine(directory.Path, "not-exists");

        var result = new FileDiscoveryService().Discover(new[] { missing }, new ScanOptions());

        Assert.Single(result.Errors);
        Assert.Contains("路径不存在", result.Errors[0]);
        Assert.Empty(result.Files);
    }

    [Fact]
    public void PathRegistryPreventsDuplicateResults()
    {
        using var directory = new TempDirectory();
        directory.CreateFile("a.exe", "a");
        directory.CreateFile("sub/b.dll", "b");
        var registry = new PathRegistry();
        var service = new FileDiscoveryService();
        var options = new ScanOptions { Recursive = true, Extensions = string.Empty };

        var first = service.Discover(new[] { directory.Path }, options, registry);
        var second = service.Discover(new[] { directory.Path }, options, registry);

        Assert.Equal(2, first.Files.Count);
        Assert.Empty(second.Files);
        Assert.Equal(2, registry.Count);
    }

    [Fact]
    public void SplitPastedPathsSupportsQuotesAndSpaces()
    {
        var text = "\"C:\\Program Files\\App\\a.exe\"\r\nC:\\Windows\\System32\\b.dll\r\n\r\n";

        var paths = FileDiscoveryService.SplitPastedPaths(text);

        Assert.Equal(2, paths.Count);
        Assert.Equal(@"C:\Program Files\App\a.exe", paths[0]);
        Assert.Equal(@"C:\Windows\System32\b.dll", paths[1]);
    }

    [Fact]
    public void NormalizeInputPathExpandsEnvironmentVariables()
    {
        Environment.SetEnvironmentVariable("SIGWALK_TEST_DIR", @"C:\Temp\SigwalkTest");

        var normalized = FileDiscoveryService.NormalizeInputPath("%SIGWALK_TEST_DIR%\\a.exe");

        Assert.Equal(@"C:\Temp\SigwalkTest\a.exe", normalized);
    }
}
