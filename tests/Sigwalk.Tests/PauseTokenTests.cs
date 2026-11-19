using Sigwalk.Core.Services.Scanning;

namespace Sigwalk.Tests;

public class PauseTokenTests
{
    [Fact]
    public async Task NotPausedCompletesImmediately()
    {
        var source = new PauseTokenSource();

        await source.Token.WaitWhilePausedAsync().WaitAsync(TimeSpan.FromSeconds(1));
        Assert.False(source.IsPaused);
    }

    [Fact]
    public async Task PauseBlocksUntilResume()
    {
        var source = new PauseTokenSource();
        source.Pause();
        Assert.True(source.IsPaused);

        var blocked = source.Token.WaitWhilePausedAsync();
        await Task.Delay(50);
        Assert.False(blocked.IsCompleted);

        source.Resume();
        await blocked.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(source.IsPaused);
    }

    [Fact]
    public async Task CancellationWhilePausedThrows()
    {
        var source = new PauseTokenSource();
        source.Pause();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => source.Token.WaitWhilePausedAsync(cts.Token));
    }

    [Fact]
    public async Task RepeatedPauseKeepsSingleGate()
    {
        var source = new PauseTokenSource();
        source.Pause();
        source.Pause();

        var blocked = source.Token.WaitWhilePausedAsync();
        source.Resume();

        await blocked.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(source.IsPaused);
    }
}
