using Sigwalk.Core.Interop;
using Sigwalk.Core.Logging;
using Sigwalk.Core.Models;
using Sigwalk.Core.Utilities;

namespace Sigwalk.Core.Services.Signature;

/// <summary>
/// Authenticode 签名验证服务。
/// 以 WinVerifyTrust 的逐签名结果为权威结论，WTHelper 仅用于读取证书与时间戳。
/// </summary>
public sealed class AuthenticodeSignatureService : ISignatureVerifier
{
    private const int MaxSignaturesToVerify = 64;

    private readonly WinTrustInvoker _invoker;
    private readonly SignaturePresenceProbe _presenceProbe;
    private readonly NestedSignatureReader _nestedReader;
    private readonly ILogService _log;

    public AuthenticodeSignatureService(ILogService? log = null)
    {
        _log = log ?? NullLogService.Instance;
        _invoker = new WinTrustInvoker();
        _presenceProbe = new SignaturePresenceProbe();
        _nestedReader = new NestedSignatureReader(_log);
    }

    public SignatureVerificationResult Verify(
        string filePath,
        RevocationMode revocationMode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(filePath))
        {
            return new SignatureVerificationResult
            {
                Status = SignatureStatus.Unknown,
                ErrorMessage = "文件不存在",
            };
        }

        bool hasEmbedded;
        try
        {
            hasEmbedded = _presenceProbe.HasEmbeddedSignature(filePath);
        }
        catch (Exception ex)
        {
            _log.Warn($"探测嵌入式签名失败：{filePath} - {ex.Message}");
            hasEmbedded = false;
        }

        WinTrustCallResult countCall;
        try
        {
            countCall = _invoker.Invoke(filePath, revocationMode, signatureIndex: null, countSecondary: true, knownSecondaryCount: 0);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.Warn($"WinVerifyTrust 调用失败：{filePath} - {ex.Message}");
            return new SignatureVerificationResult
            {
                Status = SignatureStatus.Unknown,
                ErrorMessage = $"签名验证发生异常：{ex.Message}",
                HasEmbeddedSignature = hasEmbedded,
            };
        }

        var initialMapping = SignatureErrorMapper.Map(countCall.Status, hasEmbedded);
        if (!hasEmbedded && initialMapping.Outcome == SignatureOutcome.Unsigned)
        {
            return new SignatureVerificationResult
            {
                Status = SignatureStatus.Unsigned,
                HasEmbeddedSignature = false,
            };
        }

        var total = 1;
        if (hasEmbedded)
        {
            total = (int)Math.Clamp((long)countCall.SecondarySignatureCount + 1, 1, MaxSignaturesToVerify);
        }

        var mappings = new List<(int Index, SignatureMapping Mapping)>(total);
        var signatures = new List<SignatureInfo>(total);

        for (var index = 0; index < total; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            WinTrustCallResult call;
            try
            {
                call = _invoker.Invoke(
                    filePath,
                    revocationMode,
                    signatureIndex: index,
                    countSecondary: false,
                    knownSecondaryCount: countCall.SecondarySignatureCount);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.Warn($"验证第 {index + 1} 个签名失败：{filePath} - {ex.Message}");
                var exceptionMapping = new SignatureMapping(SignatureOutcome.Unknown, $"签名验证异常：{ex.Message}");
                mappings.Add((index, exceptionMapping));
                signatures.Add(CreateSignatureInfo(index, null, SignatureStatus.Unknown, exceptionMapping, 0));
                continue;
            }

            var mapping = SignatureErrorMapper.Map(call.Status, hasEmbedded);
            if (mapping.Outcome == SignatureOutcome.Unsigned)
            {
                mapping = new SignatureMapping(
                    SignatureOutcome.Unknown,
                    $"第 {index + 1} 个签名无法验证（{Win32ErrorCodes.Format(call.Status)}）");
            }

            var status = mapping.Outcome switch
            {
                SignatureOutcome.Valid or SignatureOutcome.ValidWithWarning => SignatureStatus.Valid,
                SignatureOutcome.Invalid => SignatureStatus.Invalid,
                SignatureOutcome.Unsigned => SignatureStatus.Unsigned,
                _ => SignatureStatus.Unknown,
            };

            mappings.Add((index, mapping));
            signatures.Add(CreateSignatureInfo(index, call.Signature, status, mapping, call.Status));
        }

        var (overallStatus, overallError, overallWarning) = Aggregate(mappings, signatures);

        // 嵌套签名（例如 Microsoft 双签名）仅用于展示；去重已由 WinVerifyTrust 验证的签名。
        try
        {
            var nested = _nestedReader.Read(filePath, signatures.Count);
            if (nested.Count > 0)
            {
                var seen = new HashSet<string>(
                    signatures.Where(s => !string.IsNullOrEmpty(s.Certificate?.Thumbprint)).Select(BuildSignatureKey),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var item in nested)
                {
                    if (string.IsNullOrEmpty(item.Certificate?.Thumbprint) || seen.Add(BuildSignatureKey(item)))
                    {
                        signatures.Add(item);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"读取嵌套签名失败：{filePath} - {ex.Message}");
        }

        return new SignatureVerificationResult
        {
            Status = overallStatus,
            Signatures = signatures,
            ErrorMessage = overallError,
            Warning = overallWarning,
            HasEmbeddedSignature = hasEmbedded,
        };
    }

    private static (SignatureStatus Status, string? Error, string? Warning) Aggregate(
        List<(int Index, SignatureMapping Mapping)> mappings,
        List<SignatureInfo> signatures)
    {
        var valid = mappings
            .Where(m => m.Mapping.Outcome is SignatureOutcome.Valid or SignatureOutcome.ValidWithWarning)
            .ToList();

        if (valid.Count > 0)
        {
            string? warning = null;
            var failed = mappings
                .Where(m => m.Mapping.Outcome is not (SignatureOutcome.Valid or SignatureOutcome.ValidWithWarning))
                .ToList();

            if (failed.Count > 0)
            {
                warning = $"多重签名中第 {string.Join("、", failed.Select(f => f.Index + 1))} 个签名未通过验证";
            }

            var revocationWarnings = mappings
                .Where(m => !string.IsNullOrWhiteSpace(m.Mapping.Warning))
                .Select(m => m.Mapping.Warning!)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (revocationWarnings.Count > 0)
            {
                warning = AppendWarning(warning, string.Join("；", revocationWarnings));
            }

            if (signatures.Count > 0 && signatures.All(s => s.Certificate is null))
            {
                warning = AppendWarning(warning, "系统验证通过，但未能读取签名证书信息。");
            }

            return (SignatureStatus.Valid, null, warning);
        }

        if (mappings.Any(m => m.Mapping.Outcome == SignatureOutcome.Invalid))
        {
            var message = mappings.First(m => m.Mapping.Outcome == SignatureOutcome.Invalid).Mapping.Message;
            return (SignatureStatus.Invalid, message, null);
        }

        var unknownMessage = mappings
            .Select(m => m.Mapping.Message)
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m)) ?? "无法确定签名状态";
        return (SignatureStatus.Unknown, unknownMessage, null);
    }

    private static SignatureInfo CreateSignatureInfo(
        int index,
        ExtractedSignatureInfo? extracted,
        SignatureStatus status,
        SignatureMapping mapping,
        int nativeStatus)
    {
        var warning = mapping.Warning;
        if (status == SignatureStatus.Valid && extracted?.Certificate is null)
        {
            warning = AppendWarning(warning, "未能读取该签名的证书信息。");
        }

        return new SignatureInfo
        {
            Index = index,
            IsPrimary = index == 0,
            IsNested = false,
            IsVerified = true,
            Status = status,
            StatusText = StatusText.DescribeSignatureStatus(status),
            ErrorMessage = status == SignatureStatus.Valid ? null : mapping.Message,
            Warning = warning,
            NativeStatus = nativeStatus,
            Certificate = extracted?.Certificate,
            Timestamp = extracted?.Timestamp,
            SignatureAlgorithm = OidNames.SignatureAlgorithm(extracted?.SignatureAlgorithmOid),
            DigestAlgorithm = OidNames.DigestAlgorithm(extracted?.DigestAlgorithmOid),
            Chain = extracted?.Chain ?? new List<ChainElementInfo>(),
        };
    }

    private static string BuildSignatureKey(SignatureInfo signature) =>
        $"{signature.Certificate?.Thumbprint}|{signature.DigestAlgorithm}|{signature.SignatureAlgorithm}";

    private static string AppendWarning(string? current, string addition) =>
        string.IsNullOrWhiteSpace(current) ? addition : $"{current}；{addition}";
}
