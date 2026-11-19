using Sigwalk.Core.CommandLine;
using Sigwalk.Core.Models;

namespace Sigwalk.Tests;

public class CliParserTests
{
    [Fact]
    public void ParsesPathsAndOptions()
    {
        var result = CliOptions.Parse(new[]
        {
            @"C:\Tools",
            "--recursive",
            "--concurrency=4",
            "-m", "hash",
            "--revocation", "cache",
            "-f", "json",
            "-c", "all",
            "-o", "out.json",
            "--lang", "en",
            "--fail-on-issues",
            "-q",
        });

        Assert.True(result.IsSuccess);
        var options = result.Options!;
        Assert.Equal(new[] { @"C:\Tools" }, options.Paths);
        Assert.True(options.Recursive);
        Assert.Equal(4, options.Concurrency);
        Assert.Equal(ScanMode.HashOnly, options.ScanMode);
        Assert.Equal(RevocationMode.CacheOnly, options.Revocation);
        Assert.Equal(ExportFormat.Json, options.Format);
        Assert.Equal(ExportContentScope.All, options.Content);
        Assert.Equal("out.json", options.Output);
        Assert.Equal(AppLanguage.English, options.Language);
        Assert.True(options.FailOnIssues);
        Assert.True(options.Quiet);
    }

    [Fact]
    public void SupportsNoRecursiveAndTerminator()
    {
        var result = CliOptions.Parse(new[] { "--no-recursive", "--", "-weird-name.exe" });

        Assert.True(result.IsSuccess);
        Assert.False(result.Options!.Recursive);
        Assert.Equal(new[] { "-weird-name.exe" }, result.Options.Paths);
    }

    [Fact]
    public void HelpDoesNotRequirePaths()
    {
        var result = CliOptions.Parse(new[] { "--help" });

        Assert.True(result.IsSuccess);
        Assert.True(result.Options!.ShowHelp);
    }

    [Theory]
    [InlineData(new[] { "--unknown" }, CliParseErrorCode.UnknownOption)]
    [InlineData(new[] { "--concurrency" }, CliParseErrorCode.MissingValue)]
    [InlineData(new[] { "a.exe", "--concurrency", "99" }, CliParseErrorCode.InvalidValue)]
    [InlineData(new[] { "a.exe", "--mode", "nope" }, CliParseErrorCode.InvalidValue)]
    [InlineData(new[] { "--format", "csv" }, CliParseErrorCode.NoPaths)]
    [InlineData(new[] { "a.exe", "--format", "csv" }, CliParseErrorCode.OutputRequired)]
    [InlineData(new string[0], CliParseErrorCode.NoPaths)]
    public void ReportsParseErrors(string[] args, CliParseErrorCode expected)
    {
        var result = CliOptions.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public void FormatAndContentWithoutOutputIsRejected()
    {
        var result = CliOptions.Parse(new[] { "a.exe", "--content", "compact" });

        Assert.Equal(CliParseErrorCode.OutputRequired, result.ErrorCode);
    }
}
