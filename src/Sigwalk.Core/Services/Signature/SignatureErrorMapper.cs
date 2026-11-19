using Sigwalk.Core.Interop;
using Sigwalk.Core.Localization;

namespace Sigwalk.Core.Services.Signature;

internal enum SignatureOutcome
{
    Valid,
    ValidWithWarning,
    Invalid,
    Unsigned,
    Unknown,
}

internal sealed record SignatureMapping(SignatureOutcome Outcome, string Message, string? Warning = null);

/// <summary>把 WinVerifyTrust 状态码映射为签名状态与中文说明。</summary>
internal static class SignatureErrorMapper
{
    public static SignatureMapping Map(int status, bool hasEmbeddedSignature)
    {
        if (status == Win32ErrorCodes.S_OK)
        {
            return new SignatureMapping(SignatureOutcome.Valid, Loc.T("Signature.Message.Valid"));
        }

        if (Win32ErrorCodes.IsNoSignatureCode(status))
        {
            return hasEmbeddedSignature
                ? new SignatureMapping(
                    SignatureOutcome.Invalid,
                    Loc.F("Signature.Message.InvalidWithEmbedded", Win32ErrorCodes.Format(status)))
                : new SignatureMapping(
                    SignatureOutcome.Unsigned,
                    Loc.T("Signature.Message.Unsigned"));
        }

        if (Win32ErrorCodes.IsRevocationUnavailableCode(status))
        {
            return new SignatureMapping(
                SignatureOutcome.ValidWithWarning,
                Loc.T("Signature.Message.Valid"),
                Loc.F("Signature.Message.RevocationUnavailable", Win32ErrorCodes.Format(status)));
        }

        if (status == Win32ErrorCodes.TRUST_E_PROVIDER_UNKNOWN)
        {
            return new SignatureMapping(
                SignatureOutcome.Unknown,
                Loc.F("Signature.Message.ProviderUnknown", Win32ErrorCodes.Format(status)));
        }

        if (IsDefiniteInvalid(status))
        {
            return new SignatureMapping(SignatureOutcome.Invalid, DescribeInvalid(status));
        }

        return hasEmbeddedSignature
            ? new SignatureMapping(
                SignatureOutcome.Invalid,
                Loc.F("Signature.Message.VerifyFailed", Win32ErrorCodes.Format(status)))
            : new SignatureMapping(
                SignatureOutcome.Unknown,
                Loc.F("Signature.Message.Unknown", Win32ErrorCodes.Format(status)));
    }

    private static bool IsDefiniteInvalid(int status) =>
        status is Win32ErrorCodes.TRUST_E_BAD_DIGEST
            or Win32ErrorCodes.CERT_E_EXPIRED
            or Win32ErrorCodes.CERT_E_UNTRUSTEDROOT
            or Win32ErrorCodes.CERT_E_CHAINING
            or Win32ErrorCodes.CERT_E_REVOKED
            or Win32ErrorCodes.CERT_E_WRONG_USAGE
            or Win32ErrorCodes.TRUST_E_EXPLICIT_DISTRUST
            or Win32ErrorCodes.TRUST_E_SUBJECT_NOT_TRUSTED
            or Win32ErrorCodes.TRUST_E_TIME_STAMP
            or Win32ErrorCodes.TRUST_E_CERT_SIGNATURE
            or Win32ErrorCodes.CRYPT_E_SECURITY_SETTINGS
            or Win32ErrorCodes.NTE_BAD_ALGID;

    private static string DescribeInvalid(int status)
    {
        var reason = status switch
        {
            Win32ErrorCodes.TRUST_E_BAD_DIGEST => Loc.T("Signature.Reason.BadDigest"),
            Win32ErrorCodes.CERT_E_EXPIRED => Loc.T("Signature.Reason.Expired"),
            Win32ErrorCodes.CERT_E_UNTRUSTEDROOT => Loc.T("Signature.Reason.UntrustedRoot"),
            Win32ErrorCodes.CERT_E_CHAINING => Loc.T("Signature.Reason.Chaining"),
            Win32ErrorCodes.CERT_E_REVOKED => Loc.T("Signature.Reason.Revoked"),
            Win32ErrorCodes.CERT_E_WRONG_USAGE => Loc.T("Signature.Reason.WrongUsage"),
            Win32ErrorCodes.TRUST_E_EXPLICIT_DISTRUST => Loc.T("Signature.Reason.ExplicitDistrust"),
            Win32ErrorCodes.TRUST_E_SUBJECT_NOT_TRUSTED => Loc.T("Signature.Reason.SubjectNotTrusted"),
            Win32ErrorCodes.TRUST_E_TIME_STAMP => Loc.T("Signature.Reason.Timestamp"),
            Win32ErrorCodes.TRUST_E_CERT_SIGNATURE => Loc.T("Signature.Reason.CertSignature"),
            Win32ErrorCodes.CRYPT_E_SECURITY_SETTINGS => Loc.T("Signature.Reason.SecuritySettings"),
            Win32ErrorCodes.NTE_BAD_ALGID => Loc.T("Signature.Reason.BadAlgId"),
            _ => Loc.T("Signature.Reason.VerifyFailed"),
        };

        return reason + Loc.F("Error.Detail", Win32ErrorCodes.Format(status));
    }
}
