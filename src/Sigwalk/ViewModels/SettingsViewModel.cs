using Sigwalk.Core.Localization;
using Sigwalk.Core.Models;
using Sigwalk.Core.Utilities;

namespace Sigwalk.ViewModels;

public sealed record EnumOption<T>(T Value, string Display);

/// <summary>设置窗口中的单个列显隐选项。</summary>
public sealed class ColumnVisibilityOption : ViewModelBase
{
    private bool _isVisible;

    public ColumnVisibilityOption(string key, string display, bool isVisible)
    {
        Key = key;
        Display = display;
        _isVisible = isVisible;
    }

    public string Key { get; }
    public string Display { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }
}

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
    private EnumOption<ExportFormat> _selectedExportFormat;
    private EnumOption<AppLanguage> _selectedLanguage;
    private ExportContentScope _exportContent;
    private string _validationMessage = string.Empty;

    public SettingsViewModel(AppSettings settings)
    {
        _recursive = settings.Recursive;
        _extensions = settings.Extensions;
        _maxConcurrencyText = Math.Clamp(settings.MaxConcurrency, 1, 32).ToString();
        _autoSortAfterScan = settings.AutoSortAfterScan;
        _exportContent = settings.ExportContent;

        ScanModeOptions = Enum.GetValues<ScanMode>()
            .Select(value => new EnumOption<ScanMode>(value, StatusText.DescribeScanMode(value)))
            .ToList();
        RevocationOptions = Enum.GetValues<RevocationMode>()
            .Select(value => new EnumOption<RevocationMode>(value, StatusText.DescribeRevocationMode(value)))
            .ToList();
        ThemeOptions = new[]
        {
            new EnumOption<ThemeMode>(ThemeMode.Auto, StatusText.DescribeThemeMode(ThemeMode.Auto)),
            new EnumOption<ThemeMode>(ThemeMode.Light, StatusText.DescribeThemeMode(ThemeMode.Light)),
            new EnumOption<ThemeMode>(ThemeMode.Dark, StatusText.DescribeThemeMode(ThemeMode.Dark)),
        };
        ExportFormatOptions = new[]
        {
            new EnumOption<ExportFormat>(ExportFormat.Csv, StatusText.DescribeExportFormat(ExportFormat.Csv)),
            new EnumOption<ExportFormat>(ExportFormat.Json, StatusText.DescribeExportFormat(ExportFormat.Json)),
            new EnumOption<ExportFormat>(ExportFormat.Xlsx, StatusText.DescribeExportFormat(ExportFormat.Xlsx)),
        };
        LanguageOptions = new[]
        {
            new EnumOption<AppLanguage>(AppLanguage.Chinese, StatusText.DescribeLanguage(AppLanguage.Chinese)),
            new EnumOption<AppLanguage>(AppLanguage.English, StatusText.DescribeLanguage(AppLanguage.English)),
        };

        // 配置可能被手工修改为未定义枚举值；回退到默认项，避免设置窗口崩溃。
        _selectedScanMode = ScanModeOptions.FirstOrDefault(option => option.Value == settings.ScanMode)
                            ?? ScanModeOptions[0];
        _selectedRevocation = RevocationOptions.FirstOrDefault(option => option.Value == settings.RevocationMode)
                              ?? RevocationOptions[0];
        _selectedTheme = ThemeOptions.FirstOrDefault(option => option.Value == settings.Theme)
                         ?? ThemeOptions[0];
        _selectedExportFormat = ExportFormatOptions.FirstOrDefault(option => option.Value == settings.ExportFormat)
                                ?? ExportFormatOptions[0];
        _selectedLanguage = LanguageOptions.FirstOrDefault(option => option.Value == settings.Language)
                            ?? LanguageOptions[0];

        var hidden = new HashSet<string>(
            settings.HiddenColumns ?? new List<string>(),
            StringComparer.OrdinalIgnoreCase);
        Columns = ColumnKeys.All
            .Select(key => new ColumnVisibilityOption(
                key,
                Loc.T("Column." + key),
                !hidden.Contains(key)))
            .ToList();
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
    public IReadOnlyList<EnumOption<ExportFormat>> ExportFormatOptions { get; }
    public IReadOnlyList<EnumOption<AppLanguage>> LanguageOptions { get; }
    public IReadOnlyList<ColumnVisibilityOption> Columns { get; }

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

    public EnumOption<ExportFormat> SelectedExportFormat
    {
        get => _selectedExportFormat;
        set => SetProperty(ref _selectedExportFormat, value);
    }

    public EnumOption<AppLanguage> SelectedLanguage
    {
        get => _selectedLanguage;
        set => SetProperty(ref _selectedLanguage, value);
    }

    public bool ExportContentAll
    {
        get => _exportContent == ExportContentScope.All;
        set
        {
            if (value && _exportContent != ExportContentScope.All)
            {
                _exportContent = ExportContentScope.All;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ExportContentCompact));
            }
        }
    }

    public bool ExportContentCompact
    {
        get => _exportContent == ExportContentScope.Compact;
        set
        {
            if (value && _exportContent != ExportContentScope.Compact)
            {
                _exportContent = ExportContentScope.Compact;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ExportContentAll));
            }
        }
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
            Language = SelectedLanguage.Value,
            Recursive = Recursive,
            Extensions = Extensions,
            MaxConcurrency = concurrency,
            ScanMode = SelectedScanMode.Value,
            RevocationMode = SelectedRevocation.Value,
            AutoSortAfterScan = AutoSortAfterScan,
            ExportFormat = SelectedExportFormat.Value,
            ExportContent = _exportContent,
            HiddenColumns = Columns.Where(column => !column.IsVisible).Select(column => column.Key).ToList(),
            LastDirectory = original.LastDirectory,
        };

        return true;
    }

    /// <summary>仅校验输入，供窗口的“确定”按钮使用。</summary>
    public bool Validate()
    {
        if (!int.TryParse(MaxConcurrencyText.Trim(), out var concurrency) || concurrency < 1 || concurrency > 32)
        {
            ValidationMessage = Loc.T("Settings.Validation.Concurrency");
            return false;
        }

        ValidationMessage = string.Empty;
        return true;
    }
}
