using System.Security.Cryptography;
using System.Text;
using Sigwalk.Core.Services;
using Sigwalk.Tests.TestHelpers;

namespace Sigwalk.Tests;

public class HashServiceTests
{
    [Fact]
    public async Task ComputesKnownSha1AndSha256()
    {
        using var directory = new TempDirectory();
        var path = directory.CreateFile("abc.txt", "abc");

        var result = await new HashService().ComputeAsync(path);

        Assert.Equal("A9993E364706816ABA3E25717850C26C9CD0D89D", result.Sha1);
        Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", result.Sha256);
        Assert.Equal(3, result.BytesRead);
    }

    [Fact]
    public async Task ComputesEmptyFileHashes()
    {
        using var directory = new TempDirectory();
        var path = directory.CreateFile("empty.bin", Array.Empty<byte>());

        var result = await new HashService().ComputeAsync(path);

        Assert.Equal("DA39A3EE5E6B4B0D3255BFEF95601890AFD80709", result.Sha1);
        Assert.Equal("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855", result.Sha256);
        Assert.Equal(0, result.BytesRead);
    }

    [Fact]
    public async Task ComputesLargeFileWithSinglePass()
    {
        using var directory = new TempDirectory();
        var data = new byte[5 * 1024 * 1024 + 17];
        new Random(42).NextBytes(data);
        var path = directory.CreateFile("large.bin", data);

        long lastProgress = 0;
        var progressCount = 0;
        var progress = new ActionProgress<long>(value =>
        {
            lastProgress = value;
            progressCount++;
        });

        var result = await new HashService().ComputeAsync(path, progress);

        Assert.Equal(Convert.ToHexString(SHA1.HashData(data)), result.Sha1);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(data)), result.Sha256);
        Assert.Equal(data.Length, result.BytesRead);
        Assert.Equal(data.Length, lastProgress);
        Assert.True(progressCount >= 1);
    }

    [Fact]
    public async Task HonorsCancellation()
    {
        using var directory = new TempDirectory();
        var path = directory.CreateFile("cancel.bin", new byte[1024]);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new HashService().ComputeAsync(path, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task ReportsMissingFile()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "missing.bin");

        await Assert.ThrowsAnyAsync<IOException>(() => new HashService().ComputeAsync(path));
    }
}
