using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Sigwalk.Core.Services;

public sealed record HashResult(string Sha1, string Sha256, long BytesRead);

public interface IHashService
{
    /// <summary>以流式方式同时计算 SHA1 与 SHA256。</summary>
    Task<HashResult> ComputeAsync(string filePath, IProgress<long>? progress = null, CancellationToken cancellationToken = default);
}

/// <summary>文件哈希服务：单次读取、流式计算，避免把大文件载入内存。</summary>
public sealed class HashService : IHashService
{
    private const int BufferSize = 1024 * 1024;
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(250);

    public async Task<HashResult> ComputeAsync(
        string filePath,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        var stopwatch = Stopwatch.StartNew();
        long total = 0;

        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.ReadWrite | FileShare.Delete,
                BufferSize = 1, // 禁用 FileStream 内部缓冲，使用下面的 1 MB 缓冲区
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            };

            await using var stream = new FileStream(filePath, options);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = await stream.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken).ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
                }

                sha1.AppendData(buffer, 0, read);
                sha256.AppendData(buffer, 0, read);
                total += read;

                if (progress is not null && stopwatch.Elapsed >= ProgressInterval)
                {
                    stopwatch.Restart();
                    progress.Report(total);
                }
            }

            progress?.Report(total);
            return new HashResult(
                Convert.ToHexString(sha1.GetHashAndReset()),
                Convert.ToHexString(sha256.GetHashAndReset()),
                total);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
