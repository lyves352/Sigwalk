using Sigwalk.Core.Localization;
using Sigwalk.Core.Models;
using Sigwalk.Core.Utilities;

namespace Sigwalk.ViewModels;

/// <summary>列表中一行的显示模型，持有不可变扫描结果。</summary>
public sealed class FileItemViewModel : ViewModelBase
{
    private FileScanResult _result;
    private bool _isScanning;
    private int _index;

    public FileItemViewModel(int id, int index, FileScanResult result)
    {
        Id = id;
        _index = index;
        _result = result;
    }

    /// <summary>与扫描任务关联的稳定 ID（不随排序/过滤变化）。</summary>
    public int Id { get; }

    public int Index
    {
        get => _index;
        set => SetProperty(ref _index, value);
    }

    public FileScanResult Result => _result;

    public string FilePath => _result.FilePath;
    public string FileName => _result.FileName;
    public string FileType => _result.FileType;
    public string SizeText => _result.SizeText;
    public long SizeBytes => _result.SizeBytes;
    public DateTime LastWriteTime => _result.LastWriteTime;

    public string LastWriteTimeText => _result.LastWriteTime == DateTime.MinValue
        ? "—"
        : _result.LastWriteTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public SignatureStatus SignatureStatus => _result.SignatureStatus;

    public int SignatureStatusOrder => _result.SignatureStatus switch
    {
        SignatureStatus.Valid => 0,
        SignatureStatus.Unsigned => 1,
        SignatureStatus.Invalid => 2,
        _ => 3,
    };

    public string SignatureStatusText =>
        _result.ScanStatus == ScanStatus.Pending
            ? "—"
            : !_result.SignatureChecked
                ? Loc.T("Common.NotChecked")
                : _result.SignatureStatusText;

    public string SignerNames => _result.SignerNames;
    public string IssuerNames => _result.IssuerNames;
    public string ValidityText => _result.ValidityText;
    public string Sha1Text => HashDisplay(_result.Sha1);
    public string Sha256Text => HashDisplay(_result.Sha256);
    public string ErrorMessage => _result.ErrorMessage ?? string.Empty;
    public string SignatureError => _result.SignatureError ?? string.Empty;
    public string SignatureWarning => _result.SignatureWarning ?? string.Empty;

    public ScanStatus ScanStatus => _isScanning ? ScanStatus.Scanning : _result.ScanStatus;

    public int ScanStatusOrder => ScanStatus switch
    {
        ScanStatus.Pending => 0,
        ScanStatus.Scanning => 1,
        ScanStatus.Completed => 2,
        ScanStatus.Failed => 3,
        _ => 4,
    };

    public string ScanStatusText => _isScanning ? Loc.T("Item.Scanning") : _result.ScanStatusText;
    public bool IsScanning => _isScanning;
    public bool IsFailed => !_isScanning && _result.ScanStatus == ScanStatus.Failed;

    public string ToolTipText
    {
        get
        {
            var text =
                $"{_result.FilePath}\n" +
                Loc.F("Item.ToolTip.SignatureStatus", SignatureStatusText) + "\n" +
                Loc.F("Item.ToolTip.ScanStatus", ScanStatusText);
            if (!string.IsNullOrWhiteSpace(SignatureError))
            {
                text += "\n" + Loc.F("Item.ToolTip.SignatureError", SignatureError);
            }

            if (!string.IsNullOrWhiteSpace(SignatureWarning))
            {
                text += "\n" + Loc.F("Item.ToolTip.Warning", SignatureWarning);
            }

            if (!string.IsNullOrWhiteSpace(ErrorMessage))
            {
                text += "\n" + Loc.F("Item.ToolTip.Error", ErrorMessage);
            }

            return text;
        }
    }

    public void SetScanning(bool isScanning)
    {
        if (_isScanning == isScanning)
        {
            return;
        }

        _isScanning = isScanning;
        OnPropertyChanged(string.Empty);
    }

    public void SetResult(FileScanResult result)
    {
        _result = result;
        _isScanning = false;
        OnPropertyChanged(string.Empty);
    }

    /// <summary>语言切换后刷新所有派生的显示文本。</summary>
    public void RefreshLocalization() => OnPropertyChanged(string.Empty);

    private string HashDisplay(string? hash)
    {
        if (!string.IsNullOrWhiteSpace(hash))
        {
            return hash;
        }

        return _result.ScanStatus == ScanStatus.Failed ? Loc.T("Common.ReadFailed") : "—";
    }
}

/// <summary>过滤下拉项。</summary>
public sealed record FilterOption(SignatureFilter Value, string Display);
