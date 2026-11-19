namespace Sigwalk.Core.Services.Scanning;

/// <summary>
/// 暂停门闩：worker 在处理每个文件前等待。暂停不阻塞 UI 或线程池，
/// 正在处理的文件会先完成。
/// </summary>
public sealed class PauseTokenSource
{
    private TaskCompletionSource<bool>? _pausedSource;

    public bool IsPaused => _pausedSource is not null;

    public PauseToken Token => new(this);

    public void Pause()
    {
        var source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Interlocked.CompareExchange(ref _pausedSource, source, null);
    }

    public void Resume()
    {
        Interlocked.Exchange(ref _pausedSource, null)?.TrySetResult(true);
    }

    internal Task WaitWhilePausedAsync(CancellationToken cancellationToken)
    {
        var source = _pausedSource;
        return source is null ? Task.CompletedTask : source.Task.WaitAsync(cancellationToken);
    }
}

public readonly struct PauseToken
{
    private readonly PauseTokenSource? _source;

    internal PauseToken(PauseTokenSource source)
    {
        _source = source;
    }

    public bool IsPaused => _source?.IsPaused ?? false;

    public Task WaitWhilePausedAsync(CancellationToken cancellationToken = default) =>
        _source?.WaitWhilePausedAsync(cancellationToken) ?? Task.CompletedTask;
}
