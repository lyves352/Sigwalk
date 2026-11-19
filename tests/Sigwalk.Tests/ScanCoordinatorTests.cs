using System.Diagnostics;
using Sigwalk.Core.Models;
using Sigwalk.Core.Services.Scanning;

namespace Sigwalk.Tests;

public class ScanCoordinatorTests
{
    [Fact]
    public async Task ProcessesAllItemsAndReportsProgress()
    {
        var processor = new FakeProcessor();
        var coordinator = new ScanCoordinator(processor);
        var items = Enumerable.Range(1, 5).Select(Item).ToList();

        await using var session = coordinator.Start(Options(2), items);
        await session.WaitForCompletionAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await session.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));

        var results = session.DrainResults(100);
        Assert.Equal(5, results.Count);
        Assert.Equal(5, results.Select(r => r.Id).Distinct().Count());
        Assert.All(results, r => Assert.Equal(ScanStatus.Completed, r.Result.ScanStatus));

        var progress = session.GetProgress();
        Assert.Equal(5, progress.Total);
        Assert.Equal(5, progress.Completed);
        Assert.Equal(5, progress.Valid);
        Assert.Equal(0, progress.Active);
        Assert.Equal(0, progress.Pending);
    }

    [Fact]
    public async Task AcceptsItemsAddedWhileScanning()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var processor = new FakeProcessor
        {
            Handler = async (item, ct) =>
            {
                started.TrySetResult(true);
                await gate.Task.WaitAsync(ct);
                return Ok(item.Path);
            },
        };

        var coordinator = new ScanCoordinator(processor);
        await using var session = coordinator.Start(Options(1), new[] { Item(1), Item(2) });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(session.TryEnqueue(Item(3)));
        gate.TrySetResult(true);

        await session.WaitForCompletionAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await session.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));

        var results = session.DrainResults(100);
        Assert.Equal(3, results.Count);
        Assert.Equal(3, results.Select(r => r.Id).Distinct().Count());
    }

    [Fact]
    public async Task PauseStopsNewFilesAndResumeContinues()
    {
        var gate = new SemaphoreSlim(0, 10);
        var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var processor = new FakeProcessor
        {
            Handler = async (item, ct) =>
            {
                firstStarted.TrySetResult(true);
                await gate.WaitAsync(ct);
                return Ok(item.Path);
            },
        };

        var coordinator = new ScanCoordinator(processor);
        await using var session = coordinator.Start(Options(1), new[] { Item(1), Item(2), Item(3) });
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        session.Pause();
        gate.Release();
        await WaitUntilAsync(() => session.GetProgress().Completed == 1, TimeSpan.FromSeconds(5));
        await Task.Delay(250);
        Assert.Equal(1, session.GetProgress().Completed);

        session.Resume();
        gate.Release(2);
        await session.WaitForCompletionAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await session.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(3, session.DrainResults(100).Count);
    }

    [Fact]
    public async Task CancellationStopsWorkers()
    {
        using var cts = new CancellationTokenSource();
        var processor = new FakeProcessor
        {
            Handler = async (item, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return Ok(item.Path);
            },
        };

        var coordinator = new ScanCoordinator(processor);
        await using var session = coordinator.Start(Options(1), new[] { Item(1), Item(2) }, cts.Token);
        await Task.Delay(200);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => session.WaitForCompletionAsync(cts.Token).WaitAsync(TimeSpan.FromSeconds(5)));
        await session.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SingleItemFailureDoesNotStopOtherItems()
    {
        var processor = new FakeProcessor
        {
            Handler = (item, _) => item.Id == 2
                ? Task.FromException<FileScanResult>(new InvalidOperationException("boom"))
                : Task.FromResult(Ok(item.Path)),
        };

        var coordinator = new ScanCoordinator(processor);
        await using var session = coordinator.Start(Options(2), new[] { Item(1), Item(2), Item(3) });
        await session.WaitForCompletionAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await session.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));

        var results = session.DrainResults(100);
        Assert.Equal(3, results.Count);
        var failed = Assert.Single(results, r => r.Result.ScanStatus == ScanStatus.Failed);
        Assert.Contains("boom", failed.Result.ErrorMessage);
    }

    [Fact]
    public async Task EmptySessionCompletes()
    {
        var coordinator = new ScanCoordinator(new FakeProcessor());
        await using var session = coordinator.Start(Options(1), Array.Empty<ScanWorkItem>());

        await session.WaitForCompletionAsync().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(0, session.GetProgress().Total);
        Assert.Empty(session.DrainResults());
    }

    private static ScanOptions Options(int concurrency) => new()
    {
        MaxConcurrency = concurrency,
        Recursive = false,
    };

    private static ScanWorkItem Item(int id) => new(id, $@"C:\fake\file{id}.exe");

    private static FileScanResult Ok(string path) => new()
    {
        FilePath = path,
        FileName = Path.GetFileName(path),
        FileType = "EXE",
        SizeBytes = 1,
        LastWriteTime = DateTime.UtcNow,
        ScanStatus = ScanStatus.Completed,
        SignatureChecked = true,
        SignatureStatus = SignatureStatus.Valid,
        ScannedAt = DateTime.UtcNow,
    };

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.Elapsed > timeout)
            {
                throw new TimeoutException("等待条件超时");
            }

            await Task.Delay(20);
        }
    }

    private sealed class FakeProcessor : IFileScanProcessor
    {
        public Func<ScanWorkItem, CancellationToken, Task<FileScanResult>> Handler { get; set; } =
            (item, _) => Task.FromResult(Ok(item.Path));

        public Task<FileScanResult> ProcessAsync(
            ScanWorkItem item,
            ScanOptions options,
            IProgress<HashProgressInfo>? progress,
            CancellationToken cancellationToken) => Handler(item, cancellationToken);
    }
}
