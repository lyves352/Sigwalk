using System.Text;
using System.Windows.Input;
using Sigwalk.Core.Models;
using Sigwalk.Core.Utilities;
using Sigwalk.Services;

namespace Sigwalk.ViewModels;

public sealed record DetailField(string Label, string Value);

/// <summary>文件详情窗口的 ViewModel。</summary>
public sealed class FileDetailViewModel : ViewModelBase
{
    private readonly ClipboardService _clipboard;

    public FileDetailViewModel(FileScanResult result, ClipboardService clipboard)
    {
        Result = result;
        _clipboard = clipboard;
        BasicFields = BuildBasicFields(result);
        CopyPathCommand = new RelayCommand(() => Copy(result.FilePath));
        CopySha1Command = new RelayCommand(() => Copy(result.Sha1));
        CopySha256Command = new RelayCommand(() => Copy(result.Sha256));
        CopySignatureCommand = new RelayCommand(() => Copy(BuildSignatureText()));
    }

    public FileScanResult Result { get; }
    public IReadOnlyList<DetailField> BasicFields { get; }
    public IReadOnlyList<SignatureInfo> Signatures => Result.Signatures;
    public bool HasSignatures => Result.Signatures.Count > 0;

    public string FileName => Result.FileName;
    public string FilePath => Result.FilePath;
    public string SignatureStatusText => Result.SignatureChecked ? Result.SignatureStatusText : "未检测";
    public string ScanStatusText => Result.ScanStatusText;
    public string SignatureWarning => Result.SignatureWarning ?? string.Empty;
    public string SignatureError => Result.SignatureError ?? string.Empty;
    public string Sha1Text => Result.Sha1Text;
    public string Sha256Text => Result.Sha256Text;
    public string SignatureCountText => $"{Result.Signatures.Count} 个签名";

    public ICommand CopyPathCommand { get; }
    public ICommand CopySha1Command { get; }
    public ICommand CopySha256Command { get; }
    public ICommand CopySignatureCommand { get; }

    private void Copy(string? text) => _clipboard.TrySetText(text);

    private string BuildSignatureText()
    {
        if (Result.Signatures.Count == 0)
        {
            return $"签名状态：{SignatureStatusText}\n错误：{SignatureError}";
        }

        var builder = new StringBuilder();
        foreach (var signature in Result.Signatures)
        {
            builder.AppendLine(signature.DisplayName);
            builder.AppendLine($"  状态：{signature.StatusText}");
            builder.AppendLine($"  签名者：{signature.SignerName}");
            builder.AppendLine($"  颁发者：{signature.IssuerName}");
            builder.AppendLine($"  有效期：{signature.ValidityText}");
            builder.AppendLine($"  序列号：{signature.Certificate?.SerialNumber}");
            builder.AppendLine($"  指纹：{signature.Certificate?.Thumbprint}");
            builder.AppendLine($"  算法：{signature.SignatureAlgorithm} / {signature.DigestAlgorithm}");
            builder.AppendLine($"  时间戳签名者：{signature.Timestamp?.SignerName}");
            builder.AppendLine($"  时间戳时间：{signature.Timestamp?.TimestampText}");
            builder.AppendLine($"  证书链：{signature.Certificate?.ChainStatusText}");
            if (!string.IsNullOrWhiteSpace(signature.ErrorMessage))
            {
                builder.AppendLine($"  错误：{signature.ErrorMessage}");
            }

            if (!string.IsNullOrWhiteSpace(signature.Warning))
            {
                builder.AppendLine($"  警告：{signature.Warning}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static IReadOnlyList<DetailField> BuildBasicFields(FileScanResult result)
    {
        var version = result.VersionInfo;
        var fields = new List<DetailField>
        {
            new("文件名", result.FileName),
            new("完整路径", result.FilePath),
            new("文件大小", result.SizeText),
            new("字节数", result.SizeBytes.ToString()),
            new("文件类型", result.FileType),
            new("修改时间", result.LastWriteTime == DateTime.MinValue
                ? "—"
                : result.LastWriteTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")),
            new("扫描时间", result.ScannedAt == default
                ? "—"
                : result.ScannedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")),
            new("扫描状态", result.ScanStatusText),
            new("签名状态", result.SignatureChecked ? result.SignatureStatusText : "未检测"),
            new("签名警告", string.IsNullOrWhiteSpace(result.SignatureWarning) ? "—" : result.SignatureWarning),
            new("签名错误", string.IsNullOrWhiteSpace(result.SignatureError) ? "—" : result.SignatureError),
            new("扫描错误", string.IsNullOrWhiteSpace(result.ErrorMessage) ? "—" : result.ErrorMessage),
            new("公司名称", version?.CompanyName ?? "—"),
            new("产品名称", version?.ProductName ?? "—"),
            new("文件说明", version?.FileDescription ?? "—"),
            new("文件版本", version?.FileVersion ?? "—"),
            new("产品版本", version?.ProductVersion ?? "—"),
            new("原始文件名", version?.OriginalFilename ?? "—"),
            new("内部名称", version?.InternalName ?? "—"),
            new("版权", version?.Copyright ?? "—"),
            new("备注", version?.Comments ?? "—"),
        };

        return fields;
    }
}
