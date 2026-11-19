using Sigwalk.Core.Models;
using Sigwalk.Core.Services;
using Sigwalk.Tests.TestHelpers;

namespace Sigwalk.Tests;

public class SettingsServiceTests
{
    [Fact]
    public void LoadsDefaultsWhenFileMissing()
    {
        using var directory = new TempDirectory();
        var service = new SettingsService(directory.Path);

        var settings = service.Load();

        Assert.Equal(ThemeMode.Auto, settings.Theme);
        Assert.Equal(AppLanguage.Chinese, settings.Language);
        Assert.Equal(ExportFormat.Csv, settings.ExportFormat);
        Assert.Equal(ExportContentScope.Compact, settings.ExportContent);
        Assert.Empty(settings.HiddenColumns);
        Assert.True(settings.Recursive);
        Assert.Equal(ScanMode.Full, settings.ScanMode);
        Assert.Equal(RevocationMode.None, settings.RevocationMode);
    }

    [Fact]
    public void SavesAndLoadsRoundTrip()
    {
        using var directory = new TempDirectory();
        var service = new SettingsService(directory.Path);
        var settings = new AppSettings
        {
            Theme = ThemeMode.Dark,
            Recursive = false,
            Extensions = "exe;dll",
            MaxConcurrency = 7,
            ScanMode = ScanMode.HashOnly,
            RevocationMode = RevocationMode.Online,
            AutoSortAfterScan = false,
            Language = AppLanguage.English,
            ExportFormat = ExportFormat.Json,
            ExportContent = ExportContentScope.All,
            HiddenColumns = new List<string> { ColumnKeys.Sha1, ColumnKeys.Sha256 },
            LastDirectory = @"C:\Tools",
        };

        service.Save(settings);
        var loaded = service.Load();

        Assert.Equal(ThemeMode.Dark, loaded.Theme);
        Assert.False(loaded.Recursive);
        Assert.Equal("exe;dll", loaded.Extensions);
        Assert.Equal(7, loaded.MaxConcurrency);
        Assert.Equal(ScanMode.HashOnly, loaded.ScanMode);
        Assert.Equal(RevocationMode.Online, loaded.RevocationMode);
        Assert.False(loaded.AutoSortAfterScan);
        Assert.Equal(AppLanguage.English, loaded.Language);
        Assert.Equal(ExportFormat.Json, loaded.ExportFormat);
        Assert.Equal(ExportContentScope.All, loaded.ExportContent);
        Assert.Equal(new[] { ColumnKeys.Sha1, ColumnKeys.Sha256 }, loaded.HiddenColumns);
        Assert.Equal(@"C:\Tools", loaded.LastDirectory);

        var json = File.ReadAllText(service.SettingsPath);
        Assert.Contains("\"Dark\"", json);
        Assert.DoesNotContain(".tmp", json);
    }

    [Fact]
    public void CorruptFileFallsBackToDefaults()
    {
        using var directory = new TempDirectory();
        var service = new SettingsService(directory.Path);
        Directory.CreateDirectory(directory.Path);
        File.WriteAllText(service.SettingsPath, "{ this is not json ");

        var settings = service.Load();

        Assert.Equal(ThemeMode.Auto, settings.Theme);
        Assert.True(settings.Recursive);
        Assert.Empty(settings.HiddenColumns);
    }

    [Fact]
    public void LoadsFileWithUtf8Bom()
    {
        using var directory = new TempDirectory();
        var service = new SettingsService(directory.Path);
        Directory.CreateDirectory(directory.Path);
        File.WriteAllText(
            service.SettingsPath,
            "{\"Theme\":\"Dark\",\"Language\":\"English\"}",
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var settings = service.Load();

        Assert.Equal(ThemeMode.Dark, settings.Theme);
        Assert.Equal(AppLanguage.English, settings.Language);
    }

    [Fact]
    public void AppliesAndClampsOptions()
    {
        var settings = new AppSettings { MaxConcurrency = 999 };
        var options = settings.ToScanOptions();

        Assert.Equal(32, options.MaxConcurrency);
    }
}
