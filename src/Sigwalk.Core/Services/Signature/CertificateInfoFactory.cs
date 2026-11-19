using System.Security.Cryptography.X509Certificates;
using Sigwalk.Core.Models;
using Sigwalk.Core.Utilities;

namespace Sigwalk.Core.Services.Signature;

internal sealed record CertificateSnapshot(CertificateInfo Info, List<ChainElementInfo> Chain);

/// <summary>把 X509Certificate2 转换为可序列化的展示模型，并构建证书链。</summary>
internal static class CertificateInfoFactory
{
    public static CertificateSnapshot Create(X509Certificate2 certificate, RevocationMode revocationMode)
    {
        var chainElements = new List<ChainElementInfo>();
        X509ChainStatusFlags aggregate = X509ChainStatusFlags.NoError;
        var chainBuilt = false;

        using (var chain = new X509Chain())
        {
            var policy = chain.ChainPolicy;
            policy.RevocationMode = revocationMode == RevocationMode.None
                ? X509RevocationMode.NoCheck
                : X509RevocationMode.Online;
            policy.DisableCertificateDownloads = revocationMode != RevocationMode.Online;
            policy.VerificationFlags = X509VerificationFlags.NoFlag;

            try
            {
                chainBuilt = chain.Build(certificate);
                foreach (var status in chain.ChainStatus)
                {
                    aggregate |= status.Status;
                }

                var index = 0;
                foreach (var element in chain.ChainElements)
                {
                    var elementFlags = X509ChainStatusFlags.NoError;
                    foreach (var status in element.ChainElementStatus)
                    {
                        elementFlags |= status.Status;
                    }

                    var elementCert = element.Certificate;
                    chainElements.Add(new ChainElementInfo
                    {
                        Index = index,
                        Subject = SafeName(elementCert, false, elementCert?.Subject),
                        Issuer = SafeName(elementCert, true, elementCert?.Issuer),
                        Thumbprint = elementCert?.Thumbprint ?? string.Empty,
                        NotBefore = elementCert?.NotBefore.ToUniversalTime() ?? DateTime.MinValue,
                        NotAfter = elementCert?.NotAfter.ToUniversalTime() ?? DateTime.MinValue,
                        StatusText = DescribeFlags(elementFlags, chainBuilt),
                    });
                    index++;
                }
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                chainBuilt = false;
            }
        }

        var revocationStatus = revocationMode == RevocationMode.None
            ? RevocationStatus.NotChecked
            : aggregate.HasFlag(X509ChainStatusFlags.Revoked)
                ? RevocationStatus.Revoked
                : aggregate.HasFlag(X509ChainStatusFlags.OfflineRevocation) ||
                  aggregate.HasFlag(X509ChainStatusFlags.RevocationStatusUnknown)
                    ? RevocationStatus.Unavailable
                    : RevocationStatus.Good;

        var info = new CertificateInfo
        {
            Subject = certificate.Subject,
            SubjectName = SafeName(certificate, false, certificate.Subject),
            Issuer = certificate.Issuer,
            IssuerName = SafeName(certificate, true, certificate.Issuer),
            SerialNumber = certificate.SerialNumber,
            Thumbprint = certificate.Thumbprint,
            NotBefore = certificate.NotBefore.ToUniversalTime(),
            NotAfter = certificate.NotAfter.ToUniversalTime(),
            SignatureAlgorithm = OidNames.SignatureAlgorithm(certificate.SignatureAlgorithm.Value),
            RevocationStatus = revocationStatus,
            ChainStatusText = DescribeFlags(aggregate, chainBuilt),
            IsTrustedRoot = chainBuilt && !aggregate.HasFlag(X509ChainStatusFlags.UntrustedRoot),
            IsSelfSigned = string.Equals(certificate.Subject, certificate.Issuer, StringComparison.OrdinalIgnoreCase),
        };

        return new CertificateSnapshot(info, chainElements);
    }

    /// <summary>只读取证书基本字段，不做链验证（用于时间戳/嵌套签名展示）。</summary>
    public static CertificateInfo CreateSimple(X509Certificate2 certificate)
    {
        return new CertificateInfo
        {
            Subject = certificate.Subject,
            SubjectName = SafeName(certificate, false, certificate.Subject),
            Issuer = certificate.Issuer,
            IssuerName = SafeName(certificate, true, certificate.Issuer),
            SerialNumber = certificate.SerialNumber,
            Thumbprint = certificate.Thumbprint,
            NotBefore = certificate.NotBefore.ToUniversalTime(),
            NotAfter = certificate.NotAfter.ToUniversalTime(),
            SignatureAlgorithm = OidNames.SignatureAlgorithm(certificate.SignatureAlgorithm.Value),
            RevocationStatus = RevocationStatus.NotChecked,
            ChainStatusText = "未验证",
            IsTrustedRoot = false,
            IsSelfSigned = string.Equals(certificate.Subject, certificate.Issuer, StringComparison.OrdinalIgnoreCase),
        };
    }

    private static string SafeName(X509Certificate2? certificate, bool forIssuer, string? fallback)
    {
        if (certificate is null)
        {
            return fallback ?? string.Empty;
        }

        try
        {
            var name = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer);
            return string.IsNullOrWhiteSpace(name) ? fallback ?? string.Empty : name;
        }
        catch
        {
            return fallback ?? string.Empty;
        }
    }

    private static string DescribeFlags(X509ChainStatusFlags flags, bool chainBuilt)
    {
        if (flags == X509ChainStatusFlags.NoError)
        {
            return chainBuilt ? "证书链验证通过" : "无法构建证书链";
        }

        var parts = new List<string>();
        if (flags.HasFlag(X509ChainStatusFlags.NotTimeValid)) parts.Add("证书已过期或尚未生效");
        if (flags.HasFlag(X509ChainStatusFlags.NotTimeNested)) parts.Add("证书有效期不嵌套");
        if (flags.HasFlag(X509ChainStatusFlags.Revoked)) parts.Add("证书已吊销");
        if (flags.HasFlag(X509ChainStatusFlags.NotSignatureValid)) parts.Add("证书签名无效");
        if (flags.HasFlag(X509ChainStatusFlags.NotValidForUsage)) parts.Add("证书用途不匹配");
        if (flags.HasFlag(X509ChainStatusFlags.UntrustedRoot)) parts.Add("根证书不受信任");
        if (flags.HasFlag(X509ChainStatusFlags.RevocationStatusUnknown)) parts.Add("吊销状态未知");
        if (flags.HasFlag(X509ChainStatusFlags.OfflineRevocation)) parts.Add("无法获取吊销信息");
        if (flags.HasFlag(X509ChainStatusFlags.PartialChain)) parts.Add("证书链不完整");
        if (flags.HasFlag(X509ChainStatusFlags.HasWeakSignature)) parts.Add("使用弱签名算法");
        if (flags.HasFlag(X509ChainStatusFlags.HasNotSupportedCriticalExtension)) parts.Add("包含不支持的关键扩展");
        if (flags.HasFlag(X509ChainStatusFlags.InvalidBasicConstraints)) parts.Add("基本约束无效");
        if (flags.HasFlag(X509ChainStatusFlags.InvalidNameConstraints)) parts.Add("名称约束无效");
        if (flags.HasFlag(X509ChainStatusFlags.HasNotSupportedNameConstraint)) parts.Add("包含不支持的名称约束");
        if (flags.HasFlag(X509ChainStatusFlags.HasNotDefinedNameConstraint)) parts.Add("包含未定义的名称约束");
        if (flags.HasFlag(X509ChainStatusFlags.HasNotPermittedNameConstraint)) parts.Add("包含不允许的名称约束");
        if (flags.HasFlag(X509ChainStatusFlags.HasExcludedNameConstraint)) parts.Add("包含被排除的名称约束");
        if (flags.HasFlag(X509ChainStatusFlags.InvalidPolicyConstraints)) parts.Add("策略约束无效");
        if (flags.HasFlag(X509ChainStatusFlags.ExplicitDistrust)) parts.Add("证书被明确不信任");
        if (flags.HasFlag(X509ChainStatusFlags.Cyclic)) parts.Add("证书链存在循环");
        if (flags.HasFlag(X509ChainStatusFlags.CtlNotTimeValid)) parts.Add("证书信任列表已过期");
        if (flags.HasFlag(X509ChainStatusFlags.CtlNotSignatureValid)) parts.Add("证书信任列表签名无效");
        if (flags.HasFlag(X509ChainStatusFlags.CtlNotValidForUsage)) parts.Add("证书信任列表用途不匹配");
        if (flags.HasFlag(X509ChainStatusFlags.NoIssuanceChainPolicy)) parts.Add("缺少颁发链策略");

        return parts.Count == 0 ? flags.ToString() : string.Join("；", parts.Distinct());
    }
}
