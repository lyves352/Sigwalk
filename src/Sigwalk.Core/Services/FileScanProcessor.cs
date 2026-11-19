using Sigwalk.Core.Localization;
using Sigwalk.Core.Logging;
using Sigwalk.Core.Models;
using Sigwalk.Core.Services.Signature;
using Sigwalk.Core.Services.Scanning;
using Sigwalk.Core.Utilities;

namespace Sigwalk.Core.Services;

/// <summary>
/// 单文件扫描处理器：读取文件信息、计算哈希、验证签名，并检测文件是否在扫描期间变化。
/// </summary>
public sealed class FileScanProcessor : IFileScanProcessor
{
    private readonly IHashService _hashService;
    private readonly ISignatureVerifier _signatureVerifier;
    private readonly FileVersionService _versionService;
    private readonly ILogService _log;

    public FileScanProcessor(
        IHashService? hashService = null,
        ISignatureVerifier? signatureVerifier = null,
        FileVersionService? versionService = null,
        ILogService? log = null)
    {
        _hashService = hashService ?? new HashService();
        _signatureVerifier = signatureVerifier ?? new AuthenticodeSignatureService(log);
        _versionService = versionService ?? new FileVersionService();
        _log = log ?? NullLogService.Instance;
    }

    public async Task<FileScanResult> ProcessAsync(
        ScanWorkItem item,
        ScanOptions options,
        IProgress<HashProgressInfo>? progress,
        CancellationToken cancellationToken)
    {
        var path = item.Path;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var before = new FileInfo(path);
            if (!before.Exists)
            {
                return FileScanResult.CreateFailed(path, Loc.T("Error.FileNotFound"));
            }

            var size = before.Length;
            var lastWriteTime = before.LastWriteTimeUtc;

            if (options.MaxFileSizeBytes > 0 && size > options.MaxFileSizeBytes)
            {
                return FileScanResult.CreateFailed(
                    path,
                    Loc.F(
                        "Error.FileTooLarge",
                        FileSizeFormatter.Format(size),
                        FileSizeFormatter.Format(options.MaxFileSizeBytes)));
            }

            // 可读性探测：被独占占用/无权限的文件在此失败，而不是产生错误的签名结论。
            using (new FileStream(
                       path,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite | FileShare.Delete,
                       bufferSize: 1,
                       FileOptions.SequentialScan))
            {
            }

            var versionInfo = _versionService.Read(path);

            string? sha1 = null;
            string? sha256 = null;
            var hashChecked = false;

            if (options.ScanMode != ScanMode.SignatureOnly)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var adapter = progress is null ? null : new HashProgressAdapter(progress, item, size);
                var hash = await _hashService.ComputeAsync(path, adapter, cancellationToken).ConfigureAwait(false);
                sha1 = hash.Sha1;
                sha256 = hash.Sha256;
                hashChecked = true;
            }

            var signatureStatus = SignatureStatus.Unknown;
            var signatureChecked = false;
            string? signatureError = null;
            string? signatureWarning = null;
            var signatures = new List<SignatureInfo>();

            if (options.ScanMode != ScanMode.HashOnly)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var verification = _signatureVerifier.Verify(path, options.RevocationMode, cancellationToken);
                signatureStatus = verification.Status;
                signatureError = verification.ErrorMessage;
                signatureWarning = verification.Warning;
                signatures = verification.Signatures;
                signatureChecked = true;
            }

            // 扫描前后快照比对，避免把已变化文件的结果当作完整结果。
            var after = new FileInfo(path);
            if (!after.Exists)
            {
                return FileScanResult.CreateFailed(
                    path,
                    Loc.T("Error.DeletedDuringScan"),
                    signatureChecked,
                    hashChecked);
            }

            if (after.Length != size || after.LastWriteTimeUtc != lastWriteTime)
            {
                return FileScanResult.CreateFailed(
                    path,
                    Loc.T("Error.ChangedDuringScan"),
                    signatureChecked,
                    hashChecked);
            }

            return new FileScanResult
            {
                FilePath = path,
                FileName = Path.GetFileName(path),
                FileType = FileScanResult.GetFileType(path),
                SizeBytes = size,
                LastWriteTime = lastWriteTime,
                ScanStatus = ScanStatus.Completed,
                SignatureStatus = signatureStatus,
                SignatureChecked = signatureChecked,
                SignatureError = signatureError,
                SignatureWarning = signatureWarning,
                Signatures = signatures,
                HashChecked = hashChecked,
                Sha1 = sha1,
                Sha256 = sha256,
                VersionInfo = versionInfo,
                ScannedAt = DateTime.UtcNow,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.Warn($"扫描文件失败：{path} - {ErrorText.Describe(ex)}");
            return FileScanResult.CreateFailed(path, ErrorText.Describe(ex));
        }
    }

    private sealed class HashProgressAdapter : IProgress<long>
    {
        private readonly IProgress<HashProgressInfo> _target;
        private readonly ScanWorkItem _item;
        private readonly long _totalBytes;

        public HashProgressAdapter(IProgress<HashProgressInfo> target, ScanWorkItem item, long totalBytes)
        {
            _target = target;
            _item = item;
            _totalBytes = totalBytes;
        }

        public void Report(long value) =>
            _target.Report(new HashProgressInfo(_item.Id, _item.Path, value, _totalBytes));
    }
}
