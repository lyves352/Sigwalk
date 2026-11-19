using Sigwalk.Core.Models;
using Sigwalk.Core.Utilities;

namespace Sigwalk.ViewModels;

public sealed record EnumOption<T>(T Value, string Display);

/// <summary>设置窗口的 ViewModel。</summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private bool _recursive;
    private string _extensions;
    private string _maxConcurrencyText;
    private bool _autoSortAfterScan;
    private EnumOption<ScanMode> _selectedScanMode;
    private EnumOption<RevocationMode> _selectedRevocation;
    private EnumOption<ThemeMode> _selectedTheme;
    private string _validationMessage = string.Empty;

    public SettingsViewModel(AppSettings settings)
    {
        _recursive = settings.Recursive;
        _extensions = settings.Extensions;
        _maxConcurrencyText = Math.Clamp(settings.MaxConcurrency, 1, 32).ToString();
        _autoSortAfterScan = settings.AutoSortAfterScan;

        ScanModeOptions = Enum.GetValues<ScanMode>()
            .Select(value => new EnumOption<ScanMode>(value, StatusText.DescribeScanMode(value)))
            .ToList();
        RevocationOptions = Enum.GetValues<RevocationMode>()
            .Select(value => new EnumOption<RevocationMode>(value, StatusText.DescribeRevocationMode(value)))
            .ToList();
        ThemeOptions = Enum.GetValues<ThemeMode>()
            .Select(value => new EnumOption<ThemeMode>(value, StatusText.DescribeThemeMode(value)))
            .ToList();

        _selectedScanMode = ScanModeOptions.First(option => option.Value == settings.ScanMode);
        _selectedRevocation = RevocationOptions.First(option => option.Value == settings.RevocationMode);
        _selectedTheme = ThemeOptions.First(option => option.Value == settings.Theme);
    }

    public bool Recursive
    {
        get => _recursive;
        set => SetProperty(ref _recursive, value);
    }

    public string Extensions
    {
        get => _extensions;
        set => SetProperty(ref _extensions, value);
    }

    public string MaxConcurrencyText
    {
        get => _maxConcurrencyText;
        set => SetProperty(ref _maxConcurrencyText, value);
    }

    public bool AutoSortAfterScan
    {
        get => _autoSortAfterScan;
        set => SetProperty(ref _autoSortAfterScan, value);
    }

    public IReadOnlyList<EnumOption<ScanMode>> ScanModeOptions { get; }
    public IReadOnlyList<EnumOption<RevocationMode>> RevocationOptions { get; }
    public IReadOnlyList<EnumOption<ThemeMode>> ThemeOptions { get; }

    public EnumOption<ScanMode> SelectedScanMode
    {
        get => _selectedScanMode;
        set => SetProperty(ref _selectedScanMode, value);
    }

    public EnumOption<RevocationMode> SelectedRevocation
    {
        get => _selectedRevocation;
        set => SetProperty(ref _selectedRevocation, value);
    }

    public EnumOption<ThemeMode> SelectedTheme
    {
        get => _selectedTheme;
        set => SetProperty(ref _selectedTheme, value);
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        private set
        {
            if (SetProperty(ref _validationMessage, value))
            {
                OnPropertyChanged(nameof(HasValidationError));
            }
        }
    }

    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationMessage);

    /// <summary>校验输入并生成新的配置对象；保留原配置中未在此编辑的字段。</summary>
    public bool TryCreateSettings(AppSettings original, out AppSettings updated)
    {
        updated = original;

        if (!Validate())
        {
            return false;
        }

        var concurrency = int.Parse(MaxConcurrencyText.Trim());
        updated = new AppSettings
        {
            Version = original.Version,
            Theme = SelectedTheme.Value,
            Recursive = Recursive,
            Extensions = Extensions,
            MaxConcurrency = concurrency,
            ScanMode = SelectedScanMode.Value,
            RevocationMode = SelectedRevocation.Value,
            AutoSortAfterScan = AutoSortAfterScan,
            LastDirectory = original.LastDirectory,
        };

        return true;
    }

    /// <summary>仅校验输入，供窗口的“确定”按钮使用。</summary>
    public bool Validate()
    {
        if (!int.TryParse(MaxConcurrencyText.Trim(), out var concurrency) || concurrency < 1 || concurrency > 32)
        {
            ValidationMessage = "并发数必须是 1 到 32 之间的整数。";
            return false;
        }

        ValidationMessage = string.Empty;
        return true;
    }
}
