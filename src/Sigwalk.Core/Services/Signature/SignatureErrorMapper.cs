using Sigwalk.Core.Interop;

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
            return new SignatureMapping(SignatureOutcome.Valid, "签名有效");
        }

        if (Win32ErrorCodes.IsNoSignatureCode(status))
        {
            return hasEmbeddedSignature
                ? new SignatureMapping(
                    SignatureOutcome.Invalid,
                    $"检测到嵌入式签名，但系统无法完成验证（{Win32ErrorCodes.Format(status)}）")
                : new SignatureMapping(SignatureOutcome.Unsigned, "文件没有嵌入式数字签名");
        }

        if (Win32ErrorCodes.IsRevocationUnavailableCode(status))
        {
            return new SignatureMapping(
                SignatureOutcome.ValidWithWarning,
                "签名有效",
                $"证书吊销状态无法确认（{Win32ErrorCodes.Format(status)}）");
        }

        if (status == Win32ErrorCodes.TRUST_E_PROVIDER_UNKNOWN)
        {
            return new SignatureMapping(
                SignatureOutcome.Unknown,
                $"找不到签名验证提供程序（{Win32ErrorCodes.Format(status)}）");
        }

        if (IsDefiniteInvalid(status))
        {
            return new SignatureMapping(SignatureOutcome.Invalid, DescribeInvalid(status));
        }

        return hasEmbeddedSignature
            ? new SignatureMapping(
                SignatureOutcome.Invalid,
                $"签名验证失败（{Win32ErrorCodes.Format(status)}）")
            : new SignatureMapping(
                SignatureOutcome.Unknown,
                $"无法确定签名状态（{Win32ErrorCodes.Format(status)}）");
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
            Win32ErrorCodes.TRUST_E_BAD_DIGEST => "文件已被修改或签名摘要不匹配",
            Win32ErrorCodes.CERT_E_EXPIRED => "签名证书已过期，且没有有效时间戳",
            Win32ErrorCodes.CERT_E_UNTRUSTEDROOT => "证书链的根证书不受信任",
            Win32ErrorCodes.CERT_E_CHAINING => "证书链构建失败",
            Win32ErrorCodes.CERT_E_REVOKED => "签名证书已被吊销",
            Win32ErrorCodes.CERT_E_WRONG_USAGE => "证书用途不是代码签名",
            Win32ErrorCodes.TRUST_E_EXPLICIT_DISTRUST => "证书被系统明确不信任",
            Win32ErrorCodes.TRUST_E_SUBJECT_NOT_TRUSTED => "签名主体不被系统信任",
            Win32ErrorCodes.TRUST_E_TIME_STAMP => "时间戳签名验证失败",
            Win32ErrorCodes.TRUST_E_CERT_SIGNATURE => "证书签名无效",
            Win32ErrorCodes.CRYPT_E_SECURITY_SETTINGS => "安全设置禁止该签名算法",
            Win32ErrorCodes.NTE_BAD_ALGID => "不支持的签名算法",
            _ => "签名验证失败",
        };

        return $"{reason}（{Win32ErrorCodes.Format(status)}）";
    }
}
