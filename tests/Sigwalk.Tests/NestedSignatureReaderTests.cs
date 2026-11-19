using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Sigwalk.Core.Interop;
using Sigwalk.Core.Models;
using Sigwalk.Core.Services.Signature;

namespace Sigwalk.Tests;

public class NestedSignatureReaderTests
{
    [Fact]
    public void FindsNestedSignatureInsideUnsignedAttribute()
    {
        using var innerCertificate = CreateCertificate("Inner Nested Signer");
        using var outerCertificate = CreateCertificate("Outer Signer");

        var innerBytes = Sign(innerCertificate, "inner");
        var outerBytes = SignWithNested(outerCertificate, "outer", innerBytes);

        var nested = new NestedSignatureReader().Parse(outerBytes);

        var signature = Assert.Single(nested);
        Assert.True(signature.IsNested);
        Assert.False(signature.IsVerified);
        Assert.Equal(SignatureStatus.Unknown, signature.Status);
        Assert.Contains("Inner Nested Signer", signature.SignerName);
        Assert.Contains("仅展示", signature.StatusText);
        Assert.NotEmpty(signature.SignatureAlgorithm ?? string.Empty);
        Assert.NotEmpty(signature.DigestAlgorithm ?? string.Empty);
    }

    [Fact]
    public void RecursivelyFindsMultipleNestedLevels()
    {
        using var innermost = CreateCertificate("Innermost Signer");
        using var middle = CreateCertificate("Middle Signer");
        using var outer = CreateCertificate("Outer Signer");

        var innermostBytes = Sign(innermost, "innermost");
        var middleBytes = SignWithNested(middle, "middle", innermostBytes);
        var outerBytes = SignWithNested(outer, "outer", middleBytes);

        var nested = new NestedSignatureReader().Parse(outerBytes);

        Assert.Equal(2, nested.Count);
        Assert.Contains(nested, s => s.SignerName.Contains("Middle Signer"));
        Assert.Contains(nested, s => s.SignerName.Contains("Innermost Signer"));
    }

    [Fact]
    public void ReturnsEmptyWhenNoNestedAttribute()
    {
        using var certificate = CreateCertificate("Plain Signer");

        var cms = Sign(certificate, "plain");

        Assert.Empty(new NestedSignatureReader().Parse(cms));
    }

    private static X509Certificate2 CreateCertificate(string commonName)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={commonName}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.3") },
                critical: false));

        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));
    }

    private static byte[] Sign(X509Certificate2 certificate, string content)
    {
        var cms = new SignedCms(new ContentInfo(Encoding.UTF8.GetBytes(content)));
        cms.ComputeSignature(new CmsSigner(certificate) { IncludeOption = X509IncludeOption.EndCertOnly });
        return cms.Encode();
    }

    private static byte[] SignWithNested(X509Certificate2 certificate, string content, byte[] nested)
    {
        var signer = new CmsSigner(certificate) { IncludeOption = X509IncludeOption.EndCertOnly };
        var values = new AsnEncodedDataCollection();
        values.Add(new AsnEncodedData(new Oid(WinTrustConstants.OID_NESTED_SIGNATURE), nested));
        signer.UnsignedAttributes.Add(
            new CryptographicAttributeObject(new Oid(WinTrustConstants.OID_NESTED_SIGNATURE), values));

        var cms = new SignedCms(new ContentInfo(Encoding.UTF8.GetBytes(content)));
        cms.ComputeSignature(signer);
        return cms.Encode();
    }
}
