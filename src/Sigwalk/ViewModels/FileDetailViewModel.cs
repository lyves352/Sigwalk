using System.Text;
using System.Windows.Input;
using Sigwalk.Core.Localization;
using Sigwalk.Core.Models;
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
    public string SignatureStatusText =>
        Result.SignatureChecked ? Result.SignatureStatusText : Loc.T("Common.NotChecked");
    public string ScanStatusText => Result.ScanStatusText;
    public string SignatureWarning => Result.SignatureWarning ?? string.Empty;
    public string SignatureError => Result.SignatureError ?? string.Empty;
    public string Sha1Text => Result.Sha1Text;
    public string Sha256Text => Result.Sha256Text;
    public string SignatureCountText => Loc.F("Detail.Header.SignatureCount", Result.Signatures.Count);

    public ICommand CopyPathCommand { get; }
    public ICommand CopySha1Command { get; }
    public ICommand CopySha256Command { get; }
    public ICommand CopySignatureCommand { get; }

    private void Copy(string? text) => _clipboard.TrySetText(text);

    private string BuildSignatureText()
    {
        if (Result.Signatures.Count == 0)
        {
            return $"{Loc.T("Copy.Label.SignatureStatus")}: {SignatureStatusText}\n" +
                   $"{Loc.T("Copy.Label.Error")}: {SignatureError}";
        }

        var builder = new StringBuilder();
        foreach (var signature in Result.Signatures)
        {
            builder.AppendLine(signature.DisplayName);
            builder.AppendLine($"  {Loc.T("Copy.Label.Status")}: {signature.StatusText}");
            builder.AppendLine($"  {Loc.T("Copy.Label.Signer")}: {signature.SignerName}");
            builder.AppendLine($"  {Loc.T("Copy.Label.Issuer")}: {signature.IssuerName}");
            builder.AppendLine($"  {Loc.T("Copy.Label.Validity")}: {signature.ValidityText}");
            builder.AppendLine($"  {Loc.T("Copy.Label.SerialNumber")}: {signature.Certificate?.SerialNumber}");
            builder.AppendLine($"  {Loc.T("Copy.Label.Thumbprint")}: {signature.Certificate?.Thumbprint}");
            builder.AppendLine(
                $"  {Loc.T("Copy.Label.Algorithm")}: {signature.SignatureAlgorithm} / {signature.DigestAlgorithm}");
            builder.AppendLine(
                $"  {Loc.T("Copy.Label.TimestampSigner")}: {signature.Timestamp?.SignerName}");
            builder.AppendLine(
                $"  {Loc.T("Copy.Label.TimestampTime")}: {signature.Timestamp?.TimestampText}");
            builder.AppendLine($"  {Loc.T("Copy.Label.Chain")}: {signature.Certificate?.ChainStatusText}");
            if (!string.IsNullOrWhiteSpace(signature.ErrorMessage))
            {
                builder.AppendLine($"  {Loc.T("Copy.Label.Error")}: {signature.ErrorMessage}");
            }

            if (!string.IsNullOrWhiteSpace(signature.Warning))
            {
                builder.AppendLine($"  {Loc.T("Copy.Label.Warning")}: {signature.Warning}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static IReadOnlyList<DetailField> BuildBasicFields(FileScanResult result)
    {
        var version = result.VersionInfo;
        var dash = Loc.T("Common.Dash");
        var fields = new List<DetailField>
        {
            new(Loc.T("Detail.Field.FileName"), result.FileName),
            new(Loc.T("Detail.Field.FilePath"), result.FilePath),
            new(Loc.T("Detail.Field.FileSize"), result.SizeText),
            new(Loc.T("Detail.Field.SizeBytes"), result.SizeBytes.ToString()),
            new(Loc.T("Detail.Field.FileType"), result.FileType),
            new(Loc.T("Detail.Field.LastWriteTime"), result.LastWriteTime == DateTime.MinValue
                ? dash
                : result.LastWriteTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")),
            new(Loc.T("Detail.Field.ScannedAt"), result.ScannedAt == default
                ? dash
                : result.ScannedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")),
            new(Loc.T("Detail.Field.ScanStatus"), result.ScanStatusText),
            new(Loc.T("Detail.Field.SignatureStatus"),
                result.SignatureChecked ? result.SignatureStatusText : Loc.T("Common.NotChecked")),
            new(Loc.T("Detail.Field.SignatureWarning"),
                string.IsNullOrWhiteSpace(result.SignatureWarning) ? dash : result.SignatureWarning),
            new(Loc.T("Detail.Field.SignatureError"),
                string.IsNullOrWhiteSpace(result.SignatureError) ? dash : result.SignatureError),
            new(Loc.T("Detail.Field.ScanError"),
                string.IsNullOrWhiteSpace(result.ErrorMessage) ? dash : result.ErrorMessage),
            new(Loc.T("Detail.Field.Company"), version?.CompanyName ?? dash),
            new(Loc.T("Detail.Field.Product"), version?.ProductName ?? dash),
            new(Loc.T("Detail.Field.Description"), version?.FileDescription ?? dash),
            new(Loc.T("Detail.Field.FileVersion"), version?.FileVersion ?? dash),
            new(Loc.T("Detail.Field.ProductVersion"), version?.ProductVersion ?? dash),
            new(Loc.T("Detail.Field.OriginalFileName"), version?.OriginalFilename ?? dash),
            new(Loc.T("Detail.Field.InternalName"), version?.InternalName ?? dash),
            new(Loc.T("Detail.Field.Copyright"), version?.Copyright ?? dash),
            new(Loc.T("Detail.Field.Comments"), version?.Comments ?? dash),
        };

        return fields;
    }
}
