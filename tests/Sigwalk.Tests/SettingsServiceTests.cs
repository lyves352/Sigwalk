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

        Assert.Equal(ThemeMode.Light, settings.Theme);
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

        Assert.Equal(ThemeMode.Light, settings.Theme);
        Assert.True(settings.Recursive);
    }

    [Fact]
    public void AppliesAndClampsOptions()
    {
        var settings = new AppSettings { MaxConcurrency = 999 };
        var options = settings.ToScanOptions();

        Assert.Equal(32, options.MaxConcurrency);
    }
}
