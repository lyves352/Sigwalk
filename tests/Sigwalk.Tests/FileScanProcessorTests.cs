using Sigwalk.Core.Models;
using Sigwalk.Core.Services;
using Sigwalk.Core.Services.Scanning;
using Sigwalk.Core.Services.Signature;
using Sigwalk.Tests.TestHelpers;

namespace Sigwalk.Tests;

public class FileScanProcessorTests
{
    [Fact]
    public async Task FullModeComputesHashesAndVerifiesSignature()
    {
        using var directory = new TempDirectory();
        var path = directory.CreateFile("hello.exe", "hello");
        var verifier = new FakeVerifier
        {
            Handler = _ => new SignatureVerificationResult
            {
                Status = SignatureStatus.Valid,
                Signatures = new List<SignatureInfo>
                {
                    new()
                    {
                        Index = 0,
                        IsPrimary = true,
                        IsVerified = true,
                        Status = SignatureStatus.Valid,
                        Certificate = new CertificateInfo { SubjectName = "Test Signer" },
                    },
                },
            },
        };
        var processor = new FileScanProcessor(new HashService(), verifier);

        var result = await processor.ProcessAsync(
            new ScanWorkItem(1, path),
            new ScanOptions { ScanMode = ScanMode.Full },
            null,
            CancellationToken.None);

        Assert.Equal(ScanStatus.Completed, result.ScanStatus);
        Assert.Equal(SignatureStatus.Valid, result.SignatureStatus);
        Assert.True(result.HashChecked);
        Assert.True(result.SignatureChecked);
        Assert.Equal("AAF4C61DDCC5E8A2DABEDE0F3B482CD9AEA9434D", result.Sha1);
        Assert.Equal("2CF24DBA5FB0A30E26E83B2AC5B9E29E1B161E5C1FA7425E73043362938B9824", result.Sha256);
        Assert.Single(result.Signatures);
        Assert.Equal(1, verifier.Calls);
    }

    [Fact]
    public async Task HashOnlyModeSkipsSignatureVerification()
    {
        using var directory = new TempDirectory();
        var path = directory.CreateFile("hello.exe", "hello");
        var verifier = new FakeVerifier();
        var processor = new FileScanProcessor(new HashService(), verifier);

        var result = await processor.ProcessAsync(
            new ScanWorkItem(1, path),
            new ScanOptions { ScanMode = ScanMode.HashOnly },
            null,
            CancellationToken.None);

        Assert.True(result.HashChecked);
        Assert.False(result.SignatureChecked);
        Assert.NotNull(result.Sha1);
        Assert.Equal(0, verifier.Calls);
    }

    [Fact]
    public async Task SignatureOnlyModeSkipsHashing()
    {
        using var directory = new TempDirectory();
        var path = directory.CreateFile("hello.exe", "hello");
        var verifier = new FakeVerifier();
        var processor = new FileScanProcessor(new HashService(), verifier);

        var result = await processor.ProcessAsync(
            new ScanWorkItem(1, path),
            new ScanOptions { ScanMode = ScanMode.SignatureOnly },
            null,
            CancellationToken.None);

        Assert.False(result.HashChecked);
        Assert.Null(result.Sha1);
        Assert.Null(result.Sha256);
        Assert.True(result.SignatureChecked);
        Assert.Equal(1, verifier.Calls);
    }

    [Fact]
    public async Task MissingFileProducesFailedResult()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "missing.exe");
        var processor = new FileScanProcessor(new HashService(), new FakeVerifier());

        var result = await processor.ProcessAsync(
            new ScanWorkItem(1, path),
            new ScanOptions(),
            null,
            CancellationToken.None);

        Assert.Equal(ScanStatus.Failed, result.ScanStatus);
        Assert.Contains("不存在", result.ErrorMessage);
    }

    [Fact]
    public async Task OversizedFileIsRejected()
    {
        using var directory = new TempDirectory();
        var path = directory.CreateFile("large.exe", new byte[10]);
        var processor = new FileScanProcessor(new HashService(), new FakeVerifier());

        var result = await processor.ProcessAsync(
            new ScanWorkItem(1, path),
            new ScanOptions { MaxFileSizeBytes = 5 },
            null,
            CancellationToken.None);

        Assert.Equal(ScanStatus.Failed, result.ScanStatus);
        Assert.Contains("过大", result.ErrorMessage);
    }

    [Fact]
    public async Task VerifierExceptionIsConvertedToFailedResult()
    {
        using var directory = new TempDirectory();
        var path = directory.CreateFile("boom.exe", "hello");
        var verifier = new FakeVerifier { Handler = _ => throw new InvalidOperationException("boom") };
        var processor = new FileScanProcessor(new HashService(), verifier);

        var result = await processor.ProcessAsync(
            new ScanWorkItem(1, path),
            new ScanOptions { ScanMode = ScanMode.SignatureOnly },
            null,
            CancellationToken.None);

        Assert.Equal(ScanStatus.Failed, result.ScanStatus);
        Assert.Contains("boom", result.ErrorMessage);
    }

    [Fact]
    public async Task DetectsFileChangedDuringScan()
    {
        using var directory = new TempDirectory();
        var path = directory.CreateFile("changes.exe", "hello");
        var verifier = new FakeVerifier
        {
            Handler = filePath =>
            {
                File.AppendAllText(filePath, "changed");
                return new SignatureVerificationResult { Status = SignatureStatus.Valid };
            },
        };
        var processor = new FileScanProcessor(new HashService(), verifier);

        var result = await processor.ProcessAsync(
            new ScanWorkItem(1, path),
            new ScanOptions { ScanMode = ScanMode.SignatureOnly },
            null,
            CancellationToken.None);

        Assert.Equal(ScanStatus.Failed, result.ScanStatus);
        Assert.Contains("变化", result.ErrorMessage);
    }

    [Fact]
    public async Task HonorsCancellation()
    {
        using var directory = new TempDirectory();
        var path = directory.CreateFile("cancel.exe", "hello");
        var processor = new FileScanProcessor(new HashService(), new FakeVerifier());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => processor.ProcessAsync(
                new ScanWorkItem(1, path),
                new ScanOptions(),
                null,
                cts.Token));
    }

    private sealed class FakeVerifier : ISignatureVerifier
    {
        public Func<string, SignatureVerificationResult> Handler { get; set; } =
            _ => new SignatureVerificationResult { Status = SignatureStatus.Unsigned };

        public int Calls { get; private set; }

        public SignatureVerificationResult Verify(
            string filePath,
            RevocationMode revocationMode,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Handler(filePath);
        }
    }
}
