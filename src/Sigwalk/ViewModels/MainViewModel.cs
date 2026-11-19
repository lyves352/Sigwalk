using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Sigwalk.Core.Localization;
using Sigwalk.Core.Logging;
using Sigwalk.Core.Models;
using Sigwalk.Core.Services;
using Sigwalk.Core.Services.Export;
using Sigwalk.Core.Services.Scanning;
using Sigwalk.Core.Utilities;
using Sigwalk.Helpers;
using Sigwalk.Services;

namespace Sigwalk.ViewModels;

/// <summary>主窗口 ViewModel：导入、扫描控制、过滤、导出与详情入口。</summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly DialogService _dialog;
    private readonly FileOpenService _fileOpen;
    private readonly ClipboardService _clipboard;
    private readonly SettingsService _settingsService;
    private readonly ILogService _log;
    private readonly ThemeService _themeService;
    private readonly ScanCoordinator _coordinator;
    private readonly FileDiscoveryService _discovery = new();
    private readonly ScanInputTracker _inputTracker = new();
    private readonly PathRegistry _registry = new();
    private readonly ExportService _exporter = new();
    private readonly ResultSerializer _serializer = new();
    private readonly Dictionary<int, FileItemViewModel> _itemsById = new();
    private readonly List<FileItemViewModel> _selectedFiles = new();
    private readonly DispatcherTimer _uiTimer;
    private readonly DispatcherTimer _filterTimer;

    private AppSettings _settings;
    private FileFilter _filter = new();
    private ScanSession? _session;
    private CancellationTokenSource? _scanCts;
    private IReadOnlyList<FilterOption> _filterOptions;
    private FilterOption _selectedFilter;
    private string _searchText = string.Empty;
    private bool _isScanning;
    private string _statusKey = "Status.Ready";
    private object?[] _statusArgs = Array.Empty<object?>();
    private string _currentDirectory = string.Empty;
    private string _currentFile = string.Empty;
    private string _hashProgressText = string.Empty;
    private string _elapsedText = "00:00:00";
    private double _progressValue;
    private int _activeCount;
    private int _visibleCount;
    private int _nextId;
    private DateTime _scanStartedAt;

    private int _totalCount;
    private int _pendingCount;
    private int _completedCount;
    private int _validCount;
    private int _unsignedCount;
    private int _invalidCount;
    private int _unknownCount;
    private int _failedCount;

    public MainViewModel(
        DialogService dialog,
        FileOpenService fileOpen,
        ClipboardService clipboard,
        SettingsService settingsService,
        ILogService log,
        ThemeService themeService,
        AppSettings settings)
    {
        _dialog = dialog;
        _fileOpen = fileOpen;
        _clipboard = clipboard;
        _settingsService = settingsService;
        _log = log;
        _themeService = themeService;
        _settings = settings;
        _coordinator = new ScanCoordinator(new FileScanProcessor(log: log), log);
        _filterOptions = BuildFilterOptions();
        LocalizationService.Current.LanguageChanged += OnLanguageChanged;

        Files = new BulkObservableCollection<FileItemViewModel>();

        FilesView = CollectionViewSource.GetDefaultView(Files);
        FilesView.Filter = FilterItem;
        FilesView.SortDescriptions.Clear();

        _selectedFilter = FilterOptions[0];
        _filter.StatusFilter = _selectedFilter.Value;

        _uiTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(150),
        };
        _uiTimer.Tick += OnUiTimerTick;

        _filterTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300),
        };
        _filterTimer.Tick += OnFilterTimerTick;

        CreateCommands();
    }

    public BulkObservableCollection<FileItemViewModel> Files { get; }

    public IReadOnlyList<FileItemViewModel> SelectedFiles => _selectedFiles;

    public ICollectionView FilesView { get; }

    public IReadOnlyList<FilterOption> FilterOptions => _filterOptions;

    private static IReadOnlyList<FilterOption> BuildFilterOptions() =>
        Enum.GetValues<SignatureFilter>()
            .Select(value => new FilterOption(value, StatusText.DescribeSignatureFilter(value)))
            .ToList();

    public ICommand OpenFilesCommand { get; private set; } = null!;
    public ICommand AddFilesCommand { get; private set; } = null!;
    public ICommand OpenDirectoryCommand { get; private set; } = null!;
    public ICommand AddDirectoryCommand { get; private set; } = null!;
    public ICommand PastePathsCommand { get; private set; } = null!;
    public ICommand ClearListCommand { get; private set; } = null!;
    public ICommand ExitCommand { get; private set; } = null!;
    public ICommand StartScanCommand { get; private set; } = null!;
    public ICommand ScanActionCommand { get; private set; } = null!;
    public ICommand CancelScanCommand { get; private set; } = null!;
    public ICommand RescanSelectedCommand { get; private set; } = null!;
    public ICommand RemoveSelectedCommand { get; private set; } = null!;
    public ICommand SaveResultsCommand { get; private set; } = null!;
    public ICommand LoadResultsCommand { get; private set; } = null!;
    public ICommand ExportCommand { get; private set; } = null!;
    public ICommand ExportFilteredCommand { get; private set; } = null!;
    public ICommand SetThemeCommand { get; private set; } = null!;
    public ICommand SetLanguageCommand { get; private set; } = null!;
    public ICommand CopySelectedCommand { get; private set; } = null!;
    public ICommand CopyPathCommand { get; private set; } = null!;
    public ICommand CopyNameCommand { get; private set; } = null!;
    public ICommand CopySha1Command { get; private set; } = null!;
    public ICommand CopySha256Command { get; private set; } = null!;
    public ICommand CopySignatureCommand { get; private set; } = null!;
    public ICommand OpenFileCommand { get; private set; } = null!;
    public ICommand OpenContainingFolderCommand { get; private set; } = null!;
    public ICommand RevealInExplorerCommand { get; private set; } = null!;
    public ICommand ShowDetailsCommand { get; private set; } = null!;
    public ICommand ShowSettingsCommand { get; private set; } = null!;
    public ICommand OpenLogFolderCommand { get; private set; } = null!;
    public ICommand ShowAboutCommand { get; private set; } = null!;
    public ICommand ClearFiltersCommand { get; private set; } = null!;

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
            {
                OnPropertyChanged(nameof(IsIdle));
                OnPropertyChanged(nameof(ScanActionText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsIdle => !_isScanning;

    /// <summary>合并后的扫描按钮文案：空闲开始，扫描中取消。</summary>
    public string ScanActionText =>
        Loc.T(_isScanning ? "Menu.Scan.Cancel" : "Menu.Scan.Start");

    public string StatusMessage => Loc.F(_statusKey, _statusArgs);

    public bool Recursive
    {
        get => _settings.Recursive;
        set
        {
            if (_settings.Recursive == value)
            {
                return;
            }

            _settings.Recursive = value;
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public string ScanModeText => StatusText.DescribeScanMode(_settings.ScanMode);
    public string RevocationModeText => StatusText.DescribeRevocationMode(_settings.RevocationMode);
    public string ConcurrencyText =>
        Loc.F("Filter.Concurrency", Math.Clamp(_settings.MaxConcurrency, 1, 32));

    public string VisibleCountText => Loc.F("Filter.VisibleCount", VisibleCount);
    public string TotalCountText => Loc.F("StatusBar.Total", TotalCount);
    public string CompletedCountText => Loc.F("StatusBar.Completed", CompletedCount);
    public string ValidCountText => Loc.F("StatusBar.Valid", ValidCount);
    public string UnsignedCountText => Loc.F("StatusBar.Unsigned", UnsignedCount);
    public string InvalidCountText => Loc.F("StatusBar.Invalid", InvalidCount);
    public string UnknownCountText => Loc.F("StatusBar.Unknown", UnknownCount);
    public string FailedCountText => Loc.F("StatusBar.Failed", FailedCount);
    public string CurrentDirectoryText => string.IsNullOrEmpty(CurrentDirectory)
        ? string.Empty
        : Loc.F("StatusBar.Directory", CurrentDirectory);
    public string CurrentFileText => string.IsNullOrEmpty(CurrentFile)
        ? string.Empty
        : Loc.F("StatusBar.File", CurrentFile);
    public string ElapsedDisplayText => Loc.F("StatusBar.Elapsed", ElapsedText);

    public bool IsAutoTheme => _settings.Theme == ThemeMode.Auto;
    public bool IsLightTheme => _settings.Theme == ThemeMode.Light;
    public bool IsDarkTheme => _settings.Theme == ThemeMode.Dark;
    public bool IsChineseLanguage => Loc.Language == AppLanguage.Chinese;
    public bool IsEnglishLanguage => Loc.Language == AppLanguage.English;

    public IReadOnlyCollection<string> HiddenColumns => _settings.HiddenColumns;

    public event EventHandler? ColumnVisibilityChanged;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RestartFilterTimer();
            }
        }
    }

    public FilterOption SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (value is null || !SetProperty(ref _selectedFilter, value))
            {
                return;
            }

            _filter.StatusFilter = value.Value;
            RefreshView();
        }
    }

    public string CurrentDirectory
    {
        get => _currentDirectory;
        private set
        {
            if (SetProperty(ref _currentDirectory, value))
            {
                OnPropertyChanged(nameof(CurrentDirectoryText));
            }
        }
    }

    public string CurrentFile
    {
        get => _currentFile;
        private set
        {
            if (SetProperty(ref _currentFile, value))
            {
                OnPropertyChanged(nameof(CurrentFileText));
            }
        }
    }

    public string HashProgressText
    {
        get => _hashProgressText;
        private set => SetProperty(ref _hashProgressText, value);
    }

    public string ElapsedText
    {
        get => _elapsedText;
        private set
        {
            if (SetProperty(ref _elapsedText, value))
            {
                OnPropertyChanged(nameof(ElapsedDisplayText));
            }
        }
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public int ActiveCount
    {
        get => _activeCount;
        private set => SetProperty(ref _activeCount, value);
    }

    public int VisibleCount
    {
        get => _visibleCount;
        private set
        {
            if (SetProperty(ref _visibleCount, value))
            {
                OnPropertyChanged(nameof(VisibleCountText));
            }
        }
    }

    public int TotalCount => _totalCount;
    public int PendingCount => _pendingCount;
    public int CompletedCount => _completedCount;
    public int ValidCount => _validCount;
    public int UnsignedCount => _unsignedCount;
    public int InvalidCount => _invalidCount;
    public int UnknownCount => _unknownCount;
    public int FailedCount => _failedCount;

    public bool HasSelection => SelectedFiles.Count > 0;
    public FileItemViewModel? SelectedFile => SelectedFiles.FirstOrDefault();

    public void SetSelection(IEnumerable<FileItemViewModel> selection)
    {
        _selectedFiles.Clear();
        _selectedFiles.AddRange(selection);
        NotifySelectionChanged();
    }

    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedFile));
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>供窗口拖放事件调用。</summary>
    public Task AddPathsFromDropAsync(IEnumerable<string> paths) => AddPathsAsync(paths, clearFirst: false);

    public void OpenSelectedFile()
    {
        var item = SelectedFile;
        if (item is null)
        {
            return;
        }

        _fileOpen.OpenFile(item.FilePath);
    }

    /// <summary>应用退出时取消任务并保存配置。</summary>
    public void OnApplicationExit()
    {
        try
        {
            _scanCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        PersistSettings();
    }

    private void CreateCommands()
    {
        OpenFilesCommand = new AsyncRelayCommand(
            () => OpenFilesAsync(clearFirst: true),
            () => !IsScanning,
            HandleCommandError);
        AddFilesCommand = new AsyncRelayCommand(
            () => OpenFilesAsync(clearFirst: false),
            onError: HandleCommandError);
        OpenDirectoryCommand = new AsyncRelayCommand(
            () => OpenDirectoryAsync(clearFirst: true),
            () => !IsScanning,
            HandleCommandError);
        AddDirectoryCommand = new AsyncRelayCommand(
            () => OpenDirectoryAsync(clearFirst: false),
            onError: HandleCommandError);
        PastePathsCommand = new AsyncRelayCommand(PastePathsAsync, onError: HandleCommandError);
        ClearListCommand = new RelayCommand(ClearList, () => !IsScanning && Files.Count > 0);
        ExitCommand = new RelayCommand(() => Application.Current?.Shutdown());
        StartScanCommand = new AsyncRelayCommand(
            StartScanAsync,
            () => !IsScanning && Files.Count > 0,
            HandleCommandError);
        ScanActionCommand = new AsyncRelayCommand(
            ScanActionAsync,
            () => IsScanning || Files.Count > 0,
            HandleCommandError);
        CancelScanCommand = new RelayCommand(CancelScan, () => IsScanning);
        RescanSelectedCommand = new AsyncRelayCommand(
            RescanSelectedAsync,
            () => !IsScanning && Files.Count > 0,
            HandleCommandError);
        RemoveSelectedCommand = new RelayCommand(RemoveSelected, () => !IsScanning && HasSelection);
        SaveResultsCommand = new AsyncRelayCommand(SaveResultsAsync, () => Files.Count > 0, HandleCommandError);
        LoadResultsCommand = new AsyncRelayCommand(LoadResultsAsync, () => !IsScanning, HandleCommandError);
        ExportCommand = new AsyncRelayCommand(
            () => ExportAsync(filteredOnly: false),
            () => Files.Count > 0,
            HandleCommandError);
        ExportFilteredCommand = new AsyncRelayCommand(
            () => ExportAsync(filteredOnly: true),
            () => Files.Count > 0,
            HandleCommandError);
        CopySelectedCommand = new RelayCommand(CopySelectedRows, () => HasSelection);
        CopyPathCommand = new RelayCommand(() => CopySelected(item => item.FilePath), () => HasSelection);
        CopyNameCommand = new RelayCommand(() => CopySelected(item => item.FileName), () => HasSelection);
        CopySha1Command = new RelayCommand(() => CopySelected(item => item.Result.Sha1Text), () => HasSelection);
        CopySha256Command = new RelayCommand(() => CopySelected(item => item.Result.Sha256Text), () => HasSelection);
        CopySignatureCommand = new RelayCommand(() => CopySelected(BuildSignatureText), () => HasSelection);
        OpenFileCommand = new RelayCommand(OpenSelectedFile, () => HasSelection);
        OpenContainingFolderCommand = new RelayCommand(OpenContainingFolder, () => HasSelection);
        RevealInExplorerCommand = new RelayCommand(RevealInExplorer, () => HasSelection);
        ShowDetailsCommand = new RelayCommand(ShowDetails, () => HasSelection);
        ShowSettingsCommand = new RelayCommand(ShowSettings);
        SetThemeCommand = new RelayCommand<ThemeMode>(SetTheme);
        SetLanguageCommand = new RelayCommand<AppLanguage>(SetLanguage);
        OpenLogFolderCommand = new RelayCommand(() => _fileOpen.OpenLogFolder(_log.LogDirectory));
        ShowAboutCommand = new RelayCommand(_dialog.ShowAbout);
        ClearFiltersCommand = new RelayCommand(ClearFilters);
    }

    private bool FilterItem(object item) => item is FileItemViewModel viewModel && _filter.Matches(viewModel.Result);

    private void RestartFilterTimer()
    {
        _filterTimer.Stop();
        _filterTimer.Start();
    }

    private void OnFilterTimerTick(object? sender, EventArgs e)
    {
        _filterTimer.Stop();
        _filter.SearchText = _searchText;
        RefreshView();
    }

    private void ClearFilters()
    {
        _filterTimer.Stop();
        _searchText = string.Empty;
        _selectedFilter = _filterOptions[0];
        _filter = new FileFilter();
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(SelectedFilter));
        RefreshView();
    }

    private void RefreshView()
    {
        FilesView.Refresh();
        UpdateVisibleCount();
    }

    private void UpdateVisibleCount()
    {
        VisibleCount = _filter.IsActive
            ? FilesView.Cast<object>().Count()
            : Files.Count;
    }

    private async Task OpenFilesAsync(bool clearFirst)
    {
        var files = _dialog.OpenFiles(_settings.LastDirectory);
        if (files.Length == 0)
        {
            return;
        }

        UpdateLastDirectory(Path.GetDirectoryName(files[0]));
        await AddPathsAsync(files, clearFirst);
    }

    private async Task OpenDirectoryAsync(bool clearFirst)
    {
        var directory = _dialog.OpenFolder(_settings.LastDirectory);
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        UpdateLastDirectory(directory);
        await AddPathsAsync(new[] { directory }, clearFirst);
    }

    private async Task PastePathsAsync()
    {
        var text = _clipboard.GetText();
        var paths = FileDiscoveryService.SplitPastedPaths(text);
        if (paths.Count == 0)
        {
            SetStatus("Status.Paste.Empty");
            _dialog.ShowInfo(
                Loc.T("Dialog.Message.PasteEmpty"),
                Loc.T("Dialog.Title.PastePaths"));
            return;
        }

        await AddPathsAsync(paths, clearFirst: false);
    }

    private async Task AddPathsAsync(IEnumerable<string> paths, bool clearFirst)
    {
        try
        {
            if (clearFirst)
            {
                if (IsScanning)
                {
                    _dialog.ShowInfo(Loc.T("Status.Busy.Clear"), Loc.T("Menu.File.OpenFiles"));
                    return;
                }

                ClearListCore();
            }

            var normalizedInputs = paths
                .Select(FileDiscoveryService.NormalizeInputPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Select(path => path!)
                .ToList();

            var options = BuildScanOptions();
            var discovery = await Task.Run(
                () => _discovery.Discover(normalizedInputs, options, _registry));
            TrackInputs(normalizedInputs);
            var added = AddDiscovered(discovery.Files);
            EnqueueAddedItems(added);

            if (discovery.Errors.Count > 0)
            {
                _dialog.ShowError(
                    FormatIssues(Loc.T("Dialog.Message.PathsFailed"), discovery.Errors),
                    Loc.T("Dialog.Title.AddFiles"));
            }
            else if (discovery.Warnings.Count > 0)
            {
                _dialog.ShowWarning(
                    FormatIssues(Loc.T("Dialog.Message.Warnings"), discovery.Warnings),
                    Loc.T("Dialog.Title.ScanTip"));
            }

            SetStatus(
                added.Count == 0 ? "Status.Add.None" : "Status.Add.Added",
                added.Count == 0 ? Array.Empty<object?>() : new object?[] { added.Count });

            if (added.Count > 0)
            {
                UpdateLastDirectory(Path.GetDirectoryName(added[0].FilePath));
            }

            PersistSettings();
        }
        catch (Exception ex)
        {
            _log.Error("添加文件失败", ex);
            _dialog.ShowError(Loc.F("Status.Add.FailedDetail", ex.Message));
            SetStatus("Status.Add.Failed");
        }
    }

    private List<FileItemViewModel> AddDiscovered(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return new List<FileItemViewModel>();
        }

        var added = new List<FileItemViewModel>(paths.Count);
        foreach (var path in paths)
        {
            var item = new FileItemViewModel(
                _nextId++,
                Files.Count + added.Count + 1,
                FileScanResult.CreatePending(path));
            added.Add(item);
        }

        using (FilesView.DeferRefresh())
        {
            Files.AddRange(added);
        }

        foreach (var item in added)
        {
            _itemsById[item.Id] = item;
            _totalCount++;
            IncrementStatistics(item.Result);
        }

        NotifyStatistics();
        if (_filter.IsActive)
        {
            FilesView.Refresh();
        }

        UpdateVisibleCount();
        CommandManager.InvalidateRequerySuggested();
        return added;
    }

    private void EnqueueAddedItems(IReadOnlyList<FileItemViewModel> added)
    {
        if (!IsScanning || _session is null || added.Count == 0)
        {
            return;
        }

        foreach (var item in added)
        {
            if (_session.TryEnqueue(new ScanWorkItem(item.Id, item.FilePath)))
            {
                item.SetScanning(true);
            }
        }
    }

    private async Task ScanActionAsync()
    {
        if (IsScanning)
        {
            CancelScan();
            return;
        }

        await StartScanAsync();
    }

    private async Task StartScanAsync()
    {
        if (IsScanning)
        {
            return;
        }

        var targets = Files
            .Where(item => item.Result.ScanStatus is ScanStatus.Pending or ScanStatus.Failed)
            .ToList();

        if (targets.Count == 0)
        {
            SetStatus("Status.Scan.NoPending");
            _dialog.ShowInfo(
                Loc.T("Status.Scan.NoPendingDetail"),
                Loc.T("Menu.Scan.Start"));
            return;
        }

        await RunScanAsync(targets);
    }

    private async Task RescanSelectedAsync()
    {
        if (IsScanning)
        {
            return;
        }

        var options = BuildScanOptions();
        var roots = _inputTracker.Roots.ToList();

        // 已不存在的根不再参与后续枚举，避免每次重检都产生同样的路径错误。
        foreach (var missing in roots.Where(root => !File.Exists(root) && !Directory.Exists(root)).ToList())
        {
            _inputTracker.Remove(missing);
            roots.Remove(missing);
        }

        var existingPaths = Files.Select(item => item.FilePath).ToList();
        var selectedPaths = SelectedFiles.Select(item => item.FilePath).ToList();

        DiscoveryResult? discovery = null;
        if (roots.Count > 0)
        {
            discovery = await Task.Run(() => _discovery.Discover(roots, options));
        }

        var plan = RescanPlanner.Create(
            roots,
            discovery ?? new DiscoveryResult(),
            existingPaths,
            selectedPaths);

        if (discovery is { Warnings.Count: > 0 })
        {
            _dialog.ShowWarning(
                FormatIssues(Loc.T("Dialog.Message.Warnings"), discovery.Warnings),
                Loc.T("Dialog.Title.ScanTip"));
        }

        var removed = RemoveItemsCore(
            Files.Where(item => plan.RemovedPaths.Contains(item.FilePath, StringComparer.OrdinalIgnoreCase))
                .ToList());
        foreach (var path in plan.AddedPaths)
        {
            // 重新枚举时未使用注册表，新增路径需要补登记，避免后续重复添加。
            _registry.Add(path);
        }

        var added = AddDiscovered(plan.AddedPaths);

        if (removed > 0 || added.Count > 0)
        {
            SetStatus("Status.Rescan.Synced", removed, added.Count);
        }

        var rescanSet = new HashSet<string>(plan.RescanPaths, StringComparer.OrdinalIgnoreCase);
        var targets = Files.Where(item => rescanSet.Contains(item.FilePath)).ToList();
        if (targets.Count == 0)
        {
            SetStatus("Status.Rescan.None");
            _dialog.ShowInfo(Loc.T("Status.Rescan.None"), Loc.T("Dialog.Title.Rescan"));
            return;
        }

        SetStatus("Status.Rescan.InProgress");
        await RunScanAsync(targets);
    }

    private async Task RunScanAsync(IReadOnlyList<FileItemViewModel> targets)
    {
        if (IsScanning || targets.Count == 0)
        {
            return;
        }

        var options = BuildScanOptions();
        _scanCts = new CancellationTokenSource();
        _scanStartedAt = DateTime.UtcNow;

        try
        {
            foreach (var item in targets)
            {
                item.SetScanning(true);
            }

            _session = _coordinator.Start(
                options,
                targets.Select(item => new ScanWorkItem(item.Id, item.FilePath)),
                _scanCts.Token);

            IsScanning = true;
            SetStatus("Status.Scan.Starting");
            ProgressValue = 0;
            CurrentDirectory = string.Empty;
            CurrentFile = string.Empty;
            HashProgressText = string.Empty;
            ElapsedText = "00:00:00";
            _uiTimer.Start();
            _log.Info(
                $"开始扫描：{targets.Count} 个文件，并发 {options.MaxConcurrency}，" +
                $"模式 {StatusText.DescribeScanMode(options.ScanMode)}，吊销 {StatusText.DescribeRevocationMode(options.RevocationMode)}");

            await _session.WaitForCompletionAsync(_scanCts.Token);
            SetStatus("Status.Scan.Completed");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Status.Scan.Cancelled");
            _log.Info("用户取消扫描");
        }
        catch (Exception ex)
        {
            SetStatus("Status.Scan.Failed");
            _log.Error("扫描失败", ex);
            _dialog.ShowError(Loc.F("Status.Scan.FailedDetail", ex.Message));
        }
        finally
        {
            await FinishScanAsync();
        }
    }

    private async Task FinishScanAsync()
    {
        _uiTimer.Stop();

        var session = _session;
        var elapsed = DateTime.UtcNow - _scanStartedAt;

        if (session is not null)
        {
            try
            {
                await session.StopAsync();
            }
            catch (Exception ex)
            {
                _log.Warn($"停止扫描任务时发生异常：{ex.Message}");
            }

            while (true)
            {
                var batch = session.DrainResults(1000);
                ApplyResults(batch);
                if (batch.Count < 1000)
                {
                    break;
                }
            }

            var progress = session.GetProgress();
            elapsed = progress.Elapsed;
            ProgressValue = progress.Total > 0
                ? Math.Min(100, progress.Completed * 100.0 / progress.Total)
                : 0;
            ActiveCount = 0;

            await session.DisposeAsync();
            _session = null;
        }

        _scanCts?.Dispose();
        _scanCts = null;

        foreach (var item in Files)
        {
            if (item.IsScanning)
            {
                item.SetScanning(false);
            }
        }

        IsScanning = false;
        ElapsedText = FormatElapsed(elapsed);
        CurrentDirectory = string.Empty;
        CurrentFile = string.Empty;
        HashProgressText = string.Empty;

        if (_settings.AutoSortAfterScan)
        {
            ApplyAutoSort();
        }

        RefreshView();
        NotifyStatistics();
        PersistSettings();
        CommandManager.InvalidateRequerySuggested();
        _log.Info($"扫描结束，用时 {ElapsedText}");
    }

    private void OnUiTimerTick(object? sender, EventArgs e)
    {
        var session = _session;
        if (session is null)
        {
            return;
        }

        ApplyResults(session.DrainResults(500));

        var progress = session.GetProgress();
        ActiveCount = progress.Active;
        ProgressValue = progress.Total > 0
            ? Math.Min(100, progress.Completed * 100.0 / progress.Total)
            : 0;
        CurrentDirectory = progress.CurrentDirectory ?? string.Empty;
        CurrentFile = progress.CurrentFilePath ?? string.Empty;
        HashProgressText = progress.HashProgress is { } hash
            ? Loc.F("Status.HashProgressFile", Path.GetFileName(hash.FilePath), hash.Percentage.ToString("0"))
            : string.Empty;
        ElapsedText = FormatElapsed(progress.Elapsed);
    }

    private void ApplyResults(IReadOnlyList<ScanItemResult> batch)
    {
        if (batch.Count == 0)
        {
            return;
        }

        using (FilesView.DeferRefresh())
        {
            foreach (var entry in batch)
            {
                if (!_itemsById.TryGetValue(entry.Id, out var item))
                {
                    continue;
                }

                var oldResult = item.Result;
                item.SetResult(entry.Result);
                DecrementStatistics(oldResult);
                IncrementStatistics(entry.Result);
            }
        }

        NotifyStatistics();

        if (_filter.IsActive)
        {
            FilesView.Refresh();
        }

        UpdateVisibleCount();
    }

    private void CancelScan()
    {
        if (!IsScanning)
        {
            return;
        }

        SetStatus("Status.Scan.Cancelling");
        _session?.Resume();
        _scanCts?.Cancel();
    }

    private void ClearList()
    {
        if (IsScanning)
        {
            _dialog.ShowInfo(Loc.T("Status.Busy.Clear"), Loc.T("Dialog.Title.ClearList"));
            return;
        }

        if (Files.Count > 0 && !_dialog.Confirm(Loc.T("Status.Clear.Confirm"), Loc.T("Dialog.Title.ClearList")))
        {
            return;
        }

        ClearListCore();
    }

    private void ClearListCore()
    {
        _registry.Clear();
        _inputTracker.Clear();
        Files.ReplaceAll(Array.Empty<FileItemViewModel>());
        _itemsById.Clear();
        _selectedFiles.Clear();
        NotifySelectionChanged();
        _nextId = 0;
        _totalCount = 0;
        _pendingCount = 0;
        _completedCount = 0;
        _validCount = 0;
        _unsignedCount = 0;
        _invalidCount = 0;
        _unknownCount = 0;
        _failedCount = 0;
        VisibleCount = 0;
        ProgressValue = 0;
        NotifyStatistics();
        FilesView.Refresh();
        SetStatus("Status.Cleared");
        CommandManager.InvalidateRequerySuggested();
    }

    private void RemoveSelected()
    {
        if (IsScanning)
        {
            return;
        }

        var selected = SelectedFiles.ToList();
        if (selected.Count == 0)
        {
            return;
        }

        var removed = RemoveItemsCore(selected);
        if (removed > 0)
        {
            SetStatus("Status.Removed", removed);
        }
    }

    /// <summary>从列表、注册表与统计中移除指定行；返回实际移除数量。</summary>
    private int RemoveItemsCore(IReadOnlyCollection<FileItemViewModel> candidates)
    {
        var removed = new List<FileItemViewModel>();
        foreach (var item in candidates)
        {
            if (!_itemsById.Remove(item.Id))
            {
                continue;
            }

            removed.Add(item);
            _registry.Remove(item.FilePath);
            // 显式文件根被移除后不再参与重新枚举；目录根保持不变。
            _inputTracker.Remove(item.FilePath);
            DecrementStatistics(item.Result);
            _totalCount = Math.Max(0, _totalCount - 1);
        }

        if (removed.Count == 0)
        {
            return 0;
        }

        Files.RemoveRange(removed);
        _selectedFiles.RemoveAll(item => !_itemsById.ContainsKey(item.Id));
        NotifySelectionChanged();
        RenumberIndices();
        NotifyStatistics();
        RefreshView();
        CommandManager.InvalidateRequerySuggested();
        return removed.Count;
    }

    private void TrackInputs(IEnumerable<string> normalizedInputs)
    {
        var existing = normalizedInputs
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .ToList();
        _inputTracker.AddRange(existing);
    }

    private void RenumberIndices()
    {
        for (var index = 0; index < Files.Count; index++)
        {
            Files[index].Index = index + 1;
        }
    }

    private async Task SaveResultsAsync()
    {
        if (Files.Count == 0)
        {
            _dialog.ShowInfo(Loc.T("Dialog.Message.SaveEmpty"), Loc.T("Dialog.Title.SaveResults"));
            return;
        }

        var path = _dialog.SaveJsonFile(
            $"sigwalk-results-{DateTime.Now:yyyyMMdd-HHmmss}.json",
            _settings.LastDirectory);

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            var results = Files.Select(item => item.Result).ToList();
            await Task.Run(() => _serializer.Save(path, results));
            UpdateLastDirectory(Path.GetDirectoryName(path));
            PersistSettings();
            SetStatus("Status.Results.Saved", results.Count);
            _dialog.ShowInfo(
                Loc.F("Dialog.Message.SaveSuccess", results.Count),
                Loc.T("Dialog.Title.SaveResults"));
        }
        catch (Exception ex)
        {
            _log.Error("保存扫描结果失败", ex);
            _dialog.ShowError(
                Loc.F("Dialog.Message.SaveFailed", ex.Message),
                Loc.T("Dialog.Title.SaveResults"));
        }
    }

    private async Task LoadResultsAsync()
    {
        if (IsScanning)
        {
            _dialog.ShowInfo(Loc.T("Status.Busy.Import"), Loc.T("Dialog.Title.Import"));
            return;
        }

        var path = _dialog.OpenJsonFile(_settings.LastDirectory);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            var document = await Task.Run(() => _serializer.Load(path));
            ClearListCore();

            var loaded = new List<FileItemViewModel>();
            using (FilesView.DeferRefresh())
            {
                foreach (var result in document.Items)
                {
                    if (!_registry.TryAdd(result.FilePath))
                    {
                        continue;
                    }

                    var item = new FileItemViewModel(
                        _nextId++,
                        Files.Count + loaded.Count + 1,
                        result);
                    loaded.Add(item);
                    _itemsById[item.Id] = item;
                }

                Files.AddRange(loaded);
            }

            RecomputeStatistics();
            RefreshView();
            UpdateLastDirectory(Path.GetDirectoryName(path));
            PersistSettings();
            SetStatus("Status.Results.Loaded", loaded.Count);
        }
        catch (Exception ex)
        {
            _log.Error("加载扫描结果失败", ex);
            _dialog.ShowError(
                Loc.F("Dialog.Message.ImportFailed", ex.Message),
                Loc.T("Dialog.Title.Import"));
        }
    }

    private async Task ExportAsync(bool filteredOnly)
    {
        var results = filteredOnly
            ? FilesView.Cast<FileItemViewModel>().Select(item => item.Result).ToList()
            : Files.Select(item => item.Result).ToList();

        if (results.Count == 0)
        {
            _dialog.ShowInfo(Loc.T("Dialog.Message.NoExport"), Loc.T("Dialog.Title.Export"));
            return;
        }

        var format = _settings.ExportFormat;
        var path = _dialog.SaveExportFile(
            $"sigwalk-{DateTime.Now:yyyyMMdd-HHmmss}{ExportService.GetExtension(format)}",
            _settings.LastDirectory,
            format);

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            // 以最终扩展名优先，允许用户在保存对话框中改格式；未知扩展名仍使用设置格式。
            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension is ".csv" or ".json" or ".xlsx")
            {
                format = ExportService.DetectFormat(path);
            }

            var content = _settings.ExportContent;
            await Task.Run(() => _exporter.Export(path, format, results, content));
            UpdateLastDirectory(Path.GetDirectoryName(path));
            PersistSettings();
            SetStatus("Status.Results.Exported", results.Count, Path.GetFileName(path));
            _dialog.ShowInfo(
                Loc.F("Dialog.Message.ExportSuccess", results.Count, path),
                Loc.T("Dialog.Title.ExportDone"));
        }
        catch (Exception ex)
        {
            _log.Error("导出失败", ex);
            _dialog.ShowError(
                Loc.F("Dialog.Message.ExportFailed", ex.Message),
                Loc.T("Dialog.Title.Export"));
        }
    }

    private void OpenContainingFolder()
    {
        var item = SelectedFile;
        if (item is null)
        {
            return;
        }

        var directory = Path.GetDirectoryName(item.FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            _fileOpen.OpenFolder(directory);
        }
    }

    private void RevealInExplorer()
    {
        var item = SelectedFile;
        if (item is not null)
        {
            _fileOpen.RevealInExplorer(item.FilePath);
        }
    }

    private void ShowDetails()
    {
        var item = SelectedFile;
        if (item is null)
        {
            SetStatus("Status.SelectFileFirst");
            _log.Warn("查看文件详情：未选择文件");
            return;
        }

        _log.Info($"打开文件详情：{item.FilePath}");
        _dialog.ShowFileDetail(item.Result, _clipboard);
    }

    private void ShowSettings()
    {
        _log.Info("打开设置窗口");
        var viewModel = new SettingsViewModel(_settings);
        if (!_dialog.ShowSettings(viewModel))
        {
            _log.Info("设置窗口已取消");
            return;
        }

        if (!viewModel.TryCreateSettings(_settings, out var updated))
        {
            return;
        }

        ApplySettings(updated);
        SetStatus("Status.SettingsSaved");
        _log.Info("设置已保存");
    }

    private void ApplySettings(AppSettings updated)
    {
        _settings.Theme = updated.Theme;
        _settings.Language = updated.Language;
        _settings.Recursive = updated.Recursive;
        _settings.Extensions = updated.Extensions;
        _settings.MaxConcurrency = updated.MaxConcurrency;
        _settings.ScanMode = updated.ScanMode;
        _settings.RevocationMode = updated.RevocationMode;
        _settings.AutoSortAfterScan = updated.AutoSortAfterScan;
        _settings.ExportFormat = updated.ExportFormat;
        _settings.ExportContent = updated.ExportContent;
        _settings.HiddenColumns = new List<string>(updated.HiddenColumns);

        Loc.SetLanguage(_settings.Language);
        _themeService.Apply(_settings.Theme);
        PersistSettings();

        OnPropertyChanged(nameof(Recursive));
        OnPropertyChanged(nameof(ScanModeText));
        OnPropertyChanged(nameof(RevocationModeText));
        OnPropertyChanged(nameof(ConcurrencyText));
        OnPropertyChanged(nameof(IsAutoTheme));
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(IsChineseLanguage));
        OnPropertyChanged(nameof(IsEnglishLanguage));
        ColumnVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetTheme(ThemeMode theme)
    {
        if (_settings.Theme == theme)
        {
            return;
        }

        _settings.Theme = theme;
        _themeService.Apply(theme);
        PersistSettings();
        OnPropertyChanged(nameof(IsAutoTheme));
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(IsDarkTheme));
        SetStatus("Status.Theme.Changed", StatusText.DescribeThemeMode(theme));
    }

    private void SetLanguage(AppLanguage language)
    {
        if (_settings.Language == language)
        {
            return;
        }

        _settings.Language = language;
        Loc.SetLanguage(language);
        PersistSettings();
        SetStatus("Status.Language.Changed");
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        _filterOptions = BuildFilterOptions();
        _selectedFilter = _filterOptions.FirstOrDefault(option => option.Value == _filter.StatusFilter)
                          ?? _filterOptions[0];

        OnPropertyChanged(nameof(FilterOptions));
        OnPropertyChanged(nameof(SelectedFilter));
        OnPropertyChanged(nameof(ScanModeText));
        OnPropertyChanged(nameof(RevocationModeText));
        OnPropertyChanged(nameof(ConcurrencyText));
        OnPropertyChanged(nameof(ScanActionText));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(CurrentDirectoryText));
        OnPropertyChanged(nameof(CurrentFileText));
        OnPropertyChanged(nameof(ElapsedDisplayText));
        OnPropertyChanged(nameof(HashProgressText));
        OnPropertyChanged(nameof(IsChineseLanguage));
        OnPropertyChanged(nameof(IsEnglishLanguage));
        NotifyStatistics();
        UpdateVisibleCount();
        // UpdateVisibleCount 在数值未变化时不会触发通知，语言切换需要显式刷新。
        OnPropertyChanged(nameof(VisibleCountText));

        foreach (var item in _itemsById.Values)
        {
            item.RefreshLocalization();
        }

        CommandManager.InvalidateRequerySuggested();
    }

    public bool IsColumnVisible(string key) =>
        !_settings.HiddenColumns.Contains(key, StringComparer.OrdinalIgnoreCase);

    public void SetColumnVisible(string key, bool visible)
    {
        var hidden = _settings.HiddenColumns;
        var contains = hidden.Contains(key, StringComparer.OrdinalIgnoreCase);
        if (visible && contains)
        {
            hidden.RemoveAll(item => string.Equals(item, key, StringComparison.OrdinalIgnoreCase));
        }
        else if (!visible && !contains)
        {
            hidden.Add(key);
        }
        else
        {
            return;
        }

        PersistSettings();
        ColumnVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CopySelected(Func<FileItemViewModel, string?> selector)
    {
        var values = SelectedFiles
            .Select(selector)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.ReplaceLineEndings(" "))
            .ToList();

        if (values.Count == 0)
        {
            return;
        }

        SetStatus(_clipboard.TrySetText(string.Join(Environment.NewLine, values))
            ? "Status.Clipboard.Copied"
            : "Status.Clipboard.Failed");
    }

    private void CopySelectedRows()
    {
        if (SelectedFiles.Count == 0)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine(Loc.T("Copy.Header.Row"));
        foreach (var item in SelectedFiles)
        {
            builder.AppendLine(string.Join(
                '\t',
                Clean(item.FileName),
                Clean(item.FilePath),
                Clean(item.SizeText),
                Clean(item.FileType),
                Clean(item.LastWriteTimeText),
                Clean(item.SignatureStatusText),
                Clean(item.SignerNames),
                Clean(item.IssuerNames),
                Clean(item.Sha1Text),
                Clean(item.Sha256Text),
                Clean(item.ScanStatusText)));
        }

        if (_clipboard.TrySetText(builder.ToString()))
        {
            SetStatus("Status.Clipboard.Rows", SelectedFiles.Count);
        }
    }

    private static string BuildSignatureText(FileItemViewModel item)
    {
        if (item.Result.Signatures.Count == 0)
        {
            return $"{item.FileName}\t{item.SignatureStatusText}";
        }

        var builder = new StringBuilder();
        builder.AppendLine(item.FileName);
        foreach (var signature in item.Result.Signatures)
        {
            builder.AppendLine(
                $"{signature.DisplayName}\t{Loc.T("Copy.Label.Status")}={signature.StatusText}\t" +
                $"{Loc.T("Copy.Label.Signer")}={signature.SignerName}\t" +
                $"{Loc.T("Copy.Label.Issuer")}={signature.IssuerName}\t" +
                $"{Loc.T("Copy.Label.Validity")}={signature.ValidityText}\t" +
                $"{Loc.T("Copy.Label.Algorithm")}={signature.SignatureAlgorithm}/{signature.DigestAlgorithm}\t" +
                $"{Loc.T("Copy.Label.TimestampSigner")}={signature.Timestamp?.SignerName}\t" +
                $"{Loc.T("Copy.Label.TimestampTime")}={signature.Timestamp?.TimestampText}");
            if (!string.IsNullOrWhiteSpace(signature.ErrorMessage))
            {
                builder.AppendLine($"  {Loc.T("Copy.Label.Error")}: {signature.ErrorMessage}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string Clean(string? value) =>
        (value ?? string.Empty).Replace('\t', ' ').ReplaceLineEndings(" ");

    private void ApplyAutoSort()
    {
        using (FilesView.DeferRefresh())
        {
            FilesView.SortDescriptions.Clear();
            FilesView.SortDescriptions.Add(
                new SortDescription(nameof(FileItemViewModel.SignatureStatusOrder), ListSortDirection.Ascending));
            FilesView.SortDescriptions.Add(
                new SortDescription(nameof(FileItemViewModel.FileName), ListSortDirection.Ascending));
        }
    }

    private ScanOptions BuildScanOptions() => new()
    {
        Recursive = _settings.Recursive,
        Extensions = _settings.Extensions,
        MaxConcurrency = Math.Clamp(_settings.MaxConcurrency, 1, 32),
        ScanMode = _settings.ScanMode,
        RevocationMode = _settings.RevocationMode,
    };

    private void UpdateLastDirectory(string? directory)
    {
        if (!string.IsNullOrWhiteSpace(directory))
        {
            _settings.LastDirectory = directory;
        }
    }

    public void PersistSettings()
    {
        try
        {
            _settingsService.Save(_settings);
        }
        catch (Exception ex)
        {
            _log.Warn($"保存配置失败：{ex.Message}");
        }
    }

    private void SetStatus(string key, params object?[] args)
    {
        _statusKey = key;
        _statusArgs = args;
        OnPropertyChanged(nameof(StatusMessage));
    }

    private void HandleCommandError(Exception exception)
    {
        _log.Error("命令执行失败", exception);
        _dialog.ShowError(Loc.F("Status.OperationFailed", exception.Message));
    }

    private void NotifyStatistics()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(PendingCount));
        OnPropertyChanged(nameof(CompletedCount));
        OnPropertyChanged(nameof(ValidCount));
        OnPropertyChanged(nameof(UnsignedCount));
        OnPropertyChanged(nameof(InvalidCount));
        OnPropertyChanged(nameof(UnknownCount));
        OnPropertyChanged(nameof(FailedCount));
        OnPropertyChanged(nameof(TotalCountText));
        OnPropertyChanged(nameof(CompletedCountText));
        OnPropertyChanged(nameof(ValidCountText));
        OnPropertyChanged(nameof(UnsignedCountText));
        OnPropertyChanged(nameof(InvalidCountText));
        OnPropertyChanged(nameof(UnknownCountText));
        OnPropertyChanged(nameof(FailedCountText));
    }

    private void RecomputeStatistics()
    {
        _totalCount = Files.Count;
        _pendingCount = 0;
        _completedCount = 0;
        _validCount = 0;
        _unsignedCount = 0;
        _invalidCount = 0;
        _unknownCount = 0;
        _failedCount = 0;

        foreach (var item in Files)
        {
            IncrementStatistics(item.Result);
        }

        NotifyStatistics();
    }

    private void IncrementStatistics(FileScanResult result)
    {
        switch (result.ScanStatus)
        {
            case ScanStatus.Pending:
            case ScanStatus.Scanning:
                _pendingCount++;
                break;
            case ScanStatus.Failed:
                _failedCount++;
                break;
            case ScanStatus.Completed:
                _completedCount++;
                IncrementSignatureStatistics(result);
                break;
        }
    }

    private void DecrementStatistics(FileScanResult result)
    {
        switch (result.ScanStatus)
        {
            case ScanStatus.Pending:
            case ScanStatus.Scanning:
                _pendingCount = Math.Max(0, _pendingCount - 1);
                break;
            case ScanStatus.Failed:
                _failedCount = Math.Max(0, _failedCount - 1);
                break;
            case ScanStatus.Completed:
                _completedCount = Math.Max(0, _completedCount - 1);
                DecrementSignatureStatistics(result);
                break;
        }
    }

    private void IncrementSignatureStatistics(FileScanResult result)
    {
        if (!result.SignatureChecked)
        {
            return;
        }

        switch (result.SignatureStatus)
        {
            case SignatureStatus.Valid:
                _validCount++;
                break;
            case SignatureStatus.Unsigned:
                _unsignedCount++;
                break;
            case SignatureStatus.Invalid:
                _invalidCount++;
                break;
            default:
                _unknownCount++;
                break;
        }
    }

    private void DecrementSignatureStatistics(FileScanResult result)
    {
        if (!result.SignatureChecked)
        {
            return;
        }

        switch (result.SignatureStatus)
        {
            case SignatureStatus.Valid:
                _validCount = Math.Max(0, _validCount - 1);
                break;
            case SignatureStatus.Unsigned:
                _unsignedCount = Math.Max(0, _unsignedCount - 1);
                break;
            case SignatureStatus.Invalid:
                _invalidCount = Math.Max(0, _invalidCount - 1);
                break;
            default:
                _unknownCount = Math.Max(0, _unknownCount - 1);
                break;
        }
    }

    private static string FormatElapsed(TimeSpan elapsed) =>
        $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";

    private static string FormatIssues(string header, IReadOnlyList<string> issues)
    {
        const int maxLines = 10;
        var builder = new StringBuilder(header);
        builder.AppendLine();
        foreach (var issue in issues.Take(maxLines))
        {
            builder.AppendLine("• " + issue);
        }

        if (issues.Count > maxLines)
        {
            builder.AppendLine(Loc.F("Status.IssuesMore", issues.Count - maxLines));
        }

        return builder.ToString();
    }
}
