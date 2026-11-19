using System.Reflection.PortableExecutable;
using Sigwalk.Core.Models;
using Sigwalk.Core.Services.Signature;
using Sigwalk.Tests.TestHelpers;
using Xunit.Abstractions;

namespace Sigwalk.Tests;

public class SignatureSmokeTests
{
    private readonly ITestOutputHelper _output;

    public SignatureSmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void UnsignedTempFileIsReportedAsUnsigned()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sigwalk-unsigned-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        try
        {
            var service = new AuthenticodeSignatureService();
            var result = service.Verify(path, RevocationMode.None);

            _output.WriteLine($"Status={result.Status}, Error={result.ErrorMessage}, Embedded={result.HasEmbeddedSignature}");
            Assert.Equal(SignatureStatus.Unsigned, result.Status);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SignedDotnetHostProducesCertificateInfo()
    {
        var path = @"C:\Program Files\dotnet\dotnet.exe";
        if (!File.Exists(path))
        {
            _output.WriteLine("SKIP: dotnet.exe not found");
            return;
        }

        var service = new AuthenticodeSignatureService();
        var result = service.Verify(path, RevocationMode.None);

        _output.WriteLine($"Status={result.Status}, Error={result.ErrorMessage}, Warning={result.Warning}");
        _output.WriteLine($"Embedded={result.HasEmbeddedSignature}, Signatures={result.Signatures.Count}");
        foreach (var signature in result.Signatures)
        {
            _output.WriteLine(
                $"  [{signature.Index}] primary={signature.IsPrimary} nested={signature.IsNested} status={signature.Status} " +
                $"signer={signature.SignerName} issuer={signature.IssuerName} alg={signature.SignatureAlgorithm}/{signature.DigestAlgorithm} " +
                $"timestamp={signature.Timestamp?.TimestampText} tsa={signature.Timestamp?.SignerName} error={signature.ErrorMessage}");
        }

        Assert.NotEqual(SignatureStatus.Unsigned, result.Status);
        Assert.NotEmpty(result.Signatures);
        Assert.Contains(result.Signatures, s => s.Certificate is not null);
    }

    [Fact]
    public void CatalogOnlySystemFileIsNotReportedAsInvalid()
    {
        var path = @"C:\Windows\System32\notepad.exe";
        if (!File.Exists(path))
        {
            _output.WriteLine("SKIP: notepad.exe not found");
            return;
        }

        var service = new AuthenticodeSignatureService();
        var result = service.Verify(path, RevocationMode.None);
        _output.WriteLine($"Status={result.Status}, Embedded={result.HasEmbeddedSignature}, Error={result.ErrorMessage}");
        Assert.True(
            result.Status is SignatureStatus.Unsigned or SignatureStatus.Valid,
            $"Catalog-only file should not be Invalid; got {result.Status} ({result.ErrorMessage})");
    }

    [Fact]
    public void TamperedSignedFileIsInvalid()
    {
        var source = @"C:\Program Files\dotnet\dotnet.exe";
        if (!File.Exists(source))
        {
            _output.WriteLine("SKIP: dotnet.exe not found");
            return;
        }

        using var directory = new TempDirectory();
        var copy = Path.Combine(directory.Path, "dotnet-tampered.exe");
        File.Copy(source, copy);

        var bytes = File.ReadAllBytes(copy);
        using (var stream = File.OpenRead(copy))
        using (var peReader = new PEReader(stream))
        {
            var textSection = peReader.PEHeaders.SectionHeaders
                .FirstOrDefault(s => string.Equals(s.Name, ".text", StringComparison.Ordinal));
            if (textSection.Name is null)
            {
                _output.WriteLine("SKIP: .text section not found");
                return;
            }

            var offset = textSection.PointerToRawData + 0x100;
            if (offset <= 0 || offset >= bytes.Length)
            {
                _output.WriteLine("SKIP: tamper offset out of range");
                return;
            }

            bytes[offset] ^= 0xFF;
        }

        File.WriteAllBytes(copy, bytes);

        var result = new AuthenticodeSignatureService().Verify(copy, RevocationMode.None);
        _output.WriteLine($"Status={result.Status}, Error={result.ErrorMessage}");
        Assert.Equal(SignatureStatus.Invalid, result.Status);
    }
}
