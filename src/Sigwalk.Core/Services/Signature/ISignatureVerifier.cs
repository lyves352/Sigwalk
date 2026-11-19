using Sigwalk.Core.Models;

namespace Sigwalk.Core.Services.Signature;

/// <summary>单文件签名验证结果。</summary>
public sealed class SignatureVerificationResult
{
    public SignatureStatus Status { get; init; } = SignatureStatus.Unknown;
    public List<SignatureInfo> Signatures { get; init; } = new();
    public string? ErrorMessage { get; init; }
    public string? Warning { get; init; }
    public bool HasEmbeddedSignature { get; init; }
}

/// <summary>Authenticode 签名验证服务。</summary>
public interface ISignatureVerifier
{
    SignatureVerificationResult Verify(
        string filePath,
        RevocationMode revocationMode,
        CancellationToken cancellationToken = default);
}
