using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Sigwalk.Core.Models;
using Sigwalk.Core.Utilities;

namespace Sigwalk.Core.Services.Scanning;

public sealed record ScanWorkItem(int Id, string Path);

public sealed record ScanItemResult(int Id, FileScanResult Result);

public sealed record HashProgressInfo(int Id, string FilePath, long BytesRead, long TotalBytes)
{
    public double Percentage => TotalBytes > 0 ? Math.Clamp((double)BytesRead / TotalBytes * 100, 0, 100) : 0;
}

/// <summary>扫描进度快照，由 UI 定时轮询，避免高频跨线程 UI 更新。</summary>
public sealed class ScanProgressSnapshot
{
    public int Total { get; init; }
    public int Completed { get; init; }
    public int Active { get; init; }
    public int Pending => Math.Max(0, Total - Completed - Active);
    public int Valid { get; init; }
    public int Unsigned { get; init; }
    public int Invalid { get; init; }
    public int Unknown { get; init; }
    public int Failed { get; init; }
    public string? CurrentFilePath { get; init; }
    public string? CurrentDirectory { get; init; }
    public HashProgressInfo? HashProgress { get; init; }
    public TimeSpan Elapsed { get; init; }
    public bool IsPaused { get; init; }
}

/// <summary>单文件处理器抽象，便于扫描协调器单元测试注入假实现。</summary>
public interface IFileScanProcessor
{
    Task<FileScanResult> ProcessAsync(
        ScanWorkItem item,
        ScanOptions options,
        IProgress<HashProgressInfo>? progress,
        CancellationToken cancellationToken);
}

/// <summary>扫描协调器工厂。</summary>
public sealed class ScanCoordinator
{
    private readonly IFileScanProcessor _processor;
    private readonly Logging.ILogService _log;

    public ScanCoordinator(IFileScanProcessor processor, Logging.ILogService? log = null)
    {
        _processor = processor;
        _log = log ?? Logging.NullLogService.Instance;
    }

    public ScanSession Start(
        ScanOptions options,
        IEnumerable<ScanWorkItem> initialItems,
        CancellationToken cancellationToken = default)
    {
        return new ScanSession(_processor, options.Clone(), initialItems, cancellationToken, _log);
    }
}

/// <summary>一次扫描会话：N 个 worker 从队列取文件，支持暂停、取消和动态追加。</summary>
public sealed class ScanSession : IDisposable, IAsyncDisposable
{
    private readonly Channel<ScanWorkItem> _queue;
    private readonly CancellationTokenSource _cts;
    private readonly IFileScanProcessor _processor;
    private readonly ScanOptions _options;
    private readonly Logging.ILogService _log;
    private readonly ConcurrentQueue<ScanItemResult> _results = new();
    private readonly PauseTokenSource _pause = new();
    private readonly Task[] _workers;
    private readonly Stopwatch _stopwatch;
    private readonly object _gate = new();

    private TaskCompletionSource<bool> _idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _outstanding;
    private int _total;
    private int _completed;
    private int _active;
    private int _valid;
    private int _unsigned;
    private int _invalid;
    private int _unknown;
    private int _failed;
    private string? _currentFilePath;
    private HashProgressInfo? _hashProgress;
    private bool _accepting = true;
    private bool _disposed;

    internal ScanSession(
        IFileScanProcessor processor,
        ScanOptions options,
        IEnumerable<ScanWorkItem> initialItems,
        CancellationToken cancellationToken,
        Logging.ILogService log)
    {
        _processor = processor;
        _options = options;
        _log = log;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _queue = Channel.CreateUnbounded<ScanWorkItem>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });
        _stopwatch = Stopwatch.StartNew();

        foreach (var item in initialItems)
        {
            TryEnqueueCore(item);
        }

        var workerCount = Math.Clamp(options.MaxConcurrency, 1, 32);
        _workers = Enumerable.Range(0, workerCount)
            .Select(_ => Task.Run(() => WorkerLoopAsync()))
            .ToArray();

        SignalIfIdle();
    }

    public bool IsPaused => _pause.IsPaused;

    public bool IsAccepting => Volatile.Read(ref _accepting);

    public void Pause() => _pause.Pause();

    public void Resume() => _pause.Resume();

    /// <summary>扫描进行中动态追加文件；会话结束后返回 false。</summary>
    public bool TryEnqueue(ScanWorkItem item) => TryEnqueueCore(item);

    /// <summary>取出已完成结果，供 UI 定时批量合并。</summary>
    public List<ScanItemResult> DrainResults(int maxCount = 500)
    {
        var list = new List<ScanItemResult>(Math.Min(maxCount, 256));
        while (list.Count < maxCount && _results.TryDequeue(out var result))
        {
            list.Add(result);
        }

        return list;
    }

    public bool HasPendingResults => !_results.IsEmpty;

    public ScanProgressSnapshot GetProgress()
    {
        var currentFile = Volatile.Read(ref _currentFilePath);
        return new ScanProgressSnapshot
        {
            Total = Volatile.Read(ref _total),
            Completed = Volatile.Read(ref _completed),
            Active = Volatile.Read(ref _active),
            Valid = Volatile.Read(ref _valid),
            Unsigned = Volatile.Read(ref _unsigned),
            Invalid = Volatile.Read(ref _invalid),
            Unknown = Volatile.Read(ref _unknown),
            Failed = Volatile.Read(ref _failed),
            CurrentFilePath = currentFile,
            CurrentDirectory = string.IsNullOrEmpty(currentFile) ? null : Path.GetDirectoryName(currentFile),
            HashProgress = Volatile.Read(ref _hashProgress),
            Elapsed = _stopwatch.Elapsed,
            IsPaused = _pause.IsPaused,
        };
    }

    /// <summary>等待队列清空并停止接收新条目。取消时抛出 OperationCanceledException。</summary>
    public async Task WaitForCompletionAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            await _idle.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

            lock (_gate)
            {
                if (Volatile.Read(ref _outstanding) == 0)
                {
                    _accepting = false;
                    _queue.Writer.TryComplete();
                    return;
                }

                if (_idle.Task.IsCompleted)
                {
                    _idle = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }
        }
    }

    /// <summary>取消并等待 worker 退出。</summary>
    public async Task StopAsync()
    {
        lock (_gate)
        {
            _accepting = false;
            _queue.Writer.TryComplete();
        }

        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        try
        {
            await Task.WhenAll(_workers).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _log.Error("等待扫描 worker 退出时发生异常", ex);
        }
    }

    private bool TryEnqueueCore(ScanWorkItem item)
    {
        lock (_gate)
        {
            if (!_accepting || _cts.IsCancellationRequested)
            {
                return false;
            }

            if (_idle.Task.IsCompleted)
            {
                _idle = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            Interlocked.Increment(ref _outstanding);
            Interlocked.Increment(ref _total);

            if (!_queue.Writer.TryWrite(item))
            {
                Interlocked.Decrement(ref _outstanding);
                Interlocked.Decrement(ref _total);
                return false;
            }

            return true;
        }
    }

    private async Task WorkerLoopAsync()
    {
        var token = _cts.Token;
        try
        {
            await foreach (var item in _queue.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                try
                {
                    await _pause.Token.WaitWhilePausedAsync(token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                }
                catch (OperationCanceledException)
                {
                    DecrementOutstanding();
                    continue;
                }

                Interlocked.Increment(ref _active);
                Volatile.Write(ref _currentFilePath, item.Path);

                try
                {
                    var progress = new DelegateProgress<HashProgressInfo>(
                        value => Volatile.Write(ref _hashProgress, value));

                    FileScanResult result;
                    try
                    {
                        result = await _processor
                            .ProcessAsync(item, _options, progress, token)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        continue;
                    }
                    catch (Exception ex)
                    {
                        _log.Error($"扫描文件失败：{item.Path}", ex);
                        result = FileScanResult.CreateFailed(item.Path, ErrorText.Describe(ex));
                    }

                    _results.Enqueue(new ScanItemResult(item.Id, result));
                    UpdateCounters(result);
                }
                finally
                {
                    Interlocked.Decrement(ref _active);
                    DecrementOutstanding();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _log.Error("扫描 worker 异常退出", ex);
        }
    }

    private void UpdateCounters(FileScanResult result)
    {
        Interlocked.Increment(ref _completed);
        if (result.ScanStatus == ScanStatus.Failed)
        {
            Interlocked.Increment(ref _failed);
            return;
        }

        if (!result.SignatureChecked)
        {
            return;
        }

        switch (result.SignatureStatus)
        {
            case SignatureStatus.Valid:
                Interlocked.Increment(ref _valid);
                break;
            case SignatureStatus.Unsigned:
                Interlocked.Increment(ref _unsigned);
                break;
            case SignatureStatus.Invalid:
                Interlocked.Increment(ref _invalid);
                break;
            default:
                Interlocked.Increment(ref _unknown);
                break;
        }
    }

    private void DecrementOutstanding()
    {
        if (Interlocked.Decrement(ref _outstanding) <= 0)
        {
            _idle.TrySetResult(true);
        }
    }

    private void SignalIfIdle()
    {
        if (Volatile.Read(ref _outstanding) == 0)
        {
            _idle.TrySetResult(true);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _queue.Writer.TryComplete();
        _cts.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        Dispose();
    }

    private sealed class DelegateProgress<T> : IProgress<T>
    {
        private readonly Action<T> _action;

        public DelegateProgress(Action<T> action)
        {
            _action = action;
        }

        public void Report(T value) => _action(value);
    }
}
