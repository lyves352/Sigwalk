using Sigwalk.Core.Interop;
using Sigwalk.Core.Services.Signature;

namespace Sigwalk.Tests;

public class SignatureErrorMapperTests
{
    [Fact]
    public void SuccessIsValid()
    {
        var mapping = SignatureErrorMapper.Map(Win32ErrorCodes.S_OK, hasEmbeddedSignature: true);

        Assert.Equal(SignatureOutcome.Valid, mapping.Outcome);
    }

    [Fact]
    public void NoSignatureWithoutEmbeddedDataIsUnsigned()
    {
        var mapping = SignatureErrorMapper.Map(Win32ErrorCodes.TRUST_E_NOSIGNATURE, hasEmbeddedSignature: false);

        Assert.Equal(SignatureOutcome.Unsigned, mapping.Outcome);
    }

    [Fact]
    public void NoSignatureWithEmbeddedDataIsInvalid()
    {
        var mapping = SignatureErrorMapper.Map(Win32ErrorCodes.TRUST_E_NOSIGNATURE, hasEmbeddedSignature: true);

        Assert.Equal(SignatureOutcome.Invalid, mapping.Outcome);
    }

    [Fact]
    public void BadDigestIsInvalid()
    {
        var mapping = SignatureErrorMapper.Map(Win32ErrorCodes.TRUST_E_BAD_DIGEST, hasEmbeddedSignature: true);

        Assert.Equal(SignatureOutcome.Invalid, mapping.Outcome);
        Assert.Contains("摘要", mapping.Message);
    }

    [Fact]
    public void ExpiredCertificateIsInvalid()
    {
        var mapping = SignatureErrorMapper.Map(Win32ErrorCodes.CERT_E_EXPIRED, hasEmbeddedSignature: true);

        Assert.Equal(SignatureOutcome.Invalid, mapping.Outcome);
        Assert.Contains("过期", mapping.Message);
    }

    [Fact]
    public void RevocationUnavailableIsValidWithWarning()
    {
        var mapping = SignatureErrorMapper.Map(Win32ErrorCodes.CERT_E_REVOCATION_FAILURE, hasEmbeddedSignature: true);

        Assert.Equal(SignatureOutcome.ValidWithWarning, mapping.Outcome);
        Assert.NotNull(mapping.Warning);
        Assert.Contains("吊销", mapping.Warning);
    }

    [Fact]
    public void ProviderUnknownIsUnknown()
    {
        var mapping = SignatureErrorMapper.Map(Win32ErrorCodes.TRUST_E_PROVIDER_UNKNOWN, hasEmbeddedSignature: true);

        Assert.Equal(SignatureOutcome.Unknown, mapping.Outcome);
    }

    [Fact]
    public void UnclassifiedStatusDependsOnEmbeddedPresence()
    {
        const int unknownStatus = unchecked((int)0x81234567);

        Assert.Equal(
            SignatureOutcome.Invalid,
            SignatureErrorMapper.Map(unknownStatus, hasEmbeddedSignature: true).Outcome);
        Assert.Equal(
            SignatureOutcome.Unknown,
            SignatureErrorMapper.Map(unknownStatus, hasEmbeddedSignature: false).Outcome);
    }
}
