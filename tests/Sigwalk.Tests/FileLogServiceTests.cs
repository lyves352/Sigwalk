using Sigwalk.Core.Logging;
using Sigwalk.Tests.TestHelpers;

namespace Sigwalk.Tests;

public class FileLogServiceTests
{
    [Fact]
    public void WritesAllLevelsToDailyFile()
    {
        using var directory = new TempDirectory();
        var log = new FileLogService(directory.Path);

        log.Info("started");
        log.Warn("warning");
        log.Error("failed", new InvalidOperationException("boom"));

        Assert.Equal(directory.Path, log.LogDirectory);
        Assert.True(File.Exists(log.CurrentLogFilePath));

        var content = File.ReadAllText(log.CurrentLogFilePath);
        Assert.Contains("[INFO] started", content);
        Assert.Contains("[WARN] warning", content);
        Assert.Contains("[ERROR] failed", content);
        Assert.Contains("boom", content);
    }
}
