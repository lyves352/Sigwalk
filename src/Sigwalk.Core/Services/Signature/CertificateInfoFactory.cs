using System.Security.Cryptography.X509Certificates;
using Sigwalk.Core.Localization;
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
            ChainStatusText = Loc.T("Chain.Unverified"),
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
            return Loc.T(chainBuilt ? "Chain.Built" : "Chain.NotBuilt");
        }

        var parts = new List<string>();
        if (flags.HasFlag(X509ChainStatusFlags.NotTimeValid)) parts.Add(Loc.T("Chain.Flag.NotTimeValid"));
        if (flags.HasFlag(X509ChainStatusFlags.NotTimeNested)) parts.Add(Loc.T("Chain.Flag.NotTimeNested"));
        if (flags.HasFlag(X509ChainStatusFlags.Revoked)) parts.Add(Loc.T("Chain.Flag.Revoked"));
        if (flags.HasFlag(X509ChainStatusFlags.NotSignatureValid)) parts.Add(Loc.T("Chain.Flag.NotSignatureValid"));
        if (flags.HasFlag(X509ChainStatusFlags.NotValidForUsage)) parts.Add(Loc.T("Chain.Flag.NotValidForUsage"));
        if (flags.HasFlag(X509ChainStatusFlags.UntrustedRoot)) parts.Add(Loc.T("Chain.Flag.UntrustedRoot"));
        if (flags.HasFlag(X509ChainStatusFlags.RevocationStatusUnknown)) parts.Add(Loc.T("Chain.Flag.RevocationStatusUnknown"));
        if (flags.HasFlag(X509ChainStatusFlags.OfflineRevocation)) parts.Add(Loc.T("Chain.Flag.OfflineRevocation"));
        if (flags.HasFlag(X509ChainStatusFlags.PartialChain)) parts.Add(Loc.T("Chain.Flag.PartialChain"));
        if (flags.HasFlag(X509ChainStatusFlags.HasWeakSignature)) parts.Add(Loc.T("Chain.Flag.HasWeakSignature"));
        if (flags.HasFlag(X509ChainStatusFlags.HasNotSupportedCriticalExtension)) parts.Add(Loc.T("Chain.Flag.HasNotSupportedCriticalExtension"));
        if (flags.HasFlag(X509ChainStatusFlags.InvalidBasicConstraints)) parts.Add(Loc.T("Chain.Flag.InvalidBasicConstraints"));
        if (flags.HasFlag(X509ChainStatusFlags.InvalidNameConstraints)) parts.Add(Loc.T("Chain.Flag.InvalidNameConstraints"));
        if (flags.HasFlag(X509ChainStatusFlags.HasNotSupportedNameConstraint)) parts.Add(Loc.T("Chain.Flag.HasNotSupportedNameConstraint"));
        if (flags.HasFlag(X509ChainStatusFlags.HasNotDefinedNameConstraint)) parts.Add(Loc.T("Chain.Flag.HasNotDefinedNameConstraint"));
        if (flags.HasFlag(X509ChainStatusFlags.HasNotPermittedNameConstraint)) parts.Add(Loc.T("Chain.Flag.HasNotPermittedNameConstraint"));
        if (flags.HasFlag(X509ChainStatusFlags.HasExcludedNameConstraint)) parts.Add(Loc.T("Chain.Flag.HasExcludedNameConstraint"));
        if (flags.HasFlag(X509ChainStatusFlags.InvalidPolicyConstraints)) parts.Add(Loc.T("Chain.Flag.InvalidPolicyConstraints"));
        if (flags.HasFlag(X509ChainStatusFlags.ExplicitDistrust)) parts.Add(Loc.T("Chain.Flag.ExplicitDistrust"));
        if (flags.HasFlag(X509ChainStatusFlags.Cyclic)) parts.Add(Loc.T("Chain.Flag.Cyclic"));
        if (flags.HasFlag(X509ChainStatusFlags.CtlNotTimeValid)) parts.Add(Loc.T("Chain.Flag.CtlNotTimeValid"));
        if (flags.HasFlag(X509ChainStatusFlags.CtlNotSignatureValid)) parts.Add(Loc.T("Chain.Flag.CtlNotSignatureValid"));
        if (flags.HasFlag(X509ChainStatusFlags.CtlNotValidForUsage)) parts.Add(Loc.T("Chain.Flag.CtlNotValidForUsage"));
        if (flags.HasFlag(X509ChainStatusFlags.NoIssuanceChainPolicy)) parts.Add(Loc.T("Chain.Flag.NoIssuanceChainPolicy"));

        return parts.Count == 0
            ? flags.ToString()
            : string.Join(Loc.T("Chain.Separator"), parts.Distinct());
    }
}
