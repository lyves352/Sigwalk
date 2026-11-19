using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
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
    private FilterOption _selectedFilter;
    private string _searchText = string.Empty;
    private string _extensionFilter = string.Empty;
    private bool _isScanning;
    private bool _isPaused;
    private string _statusMessage = "就绪";
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

    public IReadOnlyList<FilterOption> FilterOptions { get; } = Enum.GetValues<SignatureFilter>()
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
    public ICommand PauseResumeCommand { get; private set; } = null!;
    public ICommand CancelScanCommand { get; private set; } = null!;
    public ICommand RescanSelectedCommand { get; private set; } = null!;
    public ICommand RemoveSelectedCommand { get; private set; } = null!;
    public ICommand SaveResultsCommand { get; private set; } = null!;
    public ICommand LoadResultsCommand { get; private set; } = null!;
    public ICommand ExportAllCommand { get; private set; } = null!;
    public ICommand ExportFilteredCommand { get; private set; } = null!;
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
    public ICommand ToggleThemeCommand { get; private set; } = null!;
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
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsIdle => !_isScanning;

    public bool IsPaused
    {
        get => _isPaused;
        private set
        {
            if (SetProperty(ref _isPaused, value))
            {
                OnPropertyChanged(nameof(PauseResumeText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string PauseResumeText => _isPaused ? "继续" : "暂停";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

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
    public string ThemeText => StatusText.DescribeThemeMode(_settings.Theme);
    public string ConcurrencyText => $"并发 {Math.Clamp(_settings.MaxConcurrency, 1, 32)}";

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

    public string ExtensionFilter
    {
        get => _extensionFilter;
        set
        {
            if (SetProperty(ref _extensionFilter, value))
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
        private set => SetProperty(ref _currentDirectory, value);
    }

    public string CurrentFile
    {
        get => _currentFile;
        private set => SetProperty(ref _currentFile, value);
    }

    public string HashProgressText
    {
        get => _hashProgressText;
        private set => SetProperty(ref _hashProgressText, value);
    }

    public string ElapsedText
    {
        get => _elapsedText;
        private set => SetProperty(ref _elapsedText, value);
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
        private set => SetProperty(ref _visibleCount, value);
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
        PauseResumeCommand = new RelayCommand(TogglePause, () => IsScanning);
        CancelScanCommand = new RelayCommand(CancelScan, () => IsScanning);
        RescanSelectedCommand = new AsyncRelayCommand(
            RescanSelectedAsync,
            () => !IsScanning && HasSelection,
            HandleCommandError);
        RemoveSelectedCommand = new RelayCommand(RemoveSelected, () => !IsScanning && HasSelection);
        SaveResultsCommand = new AsyncRelayCommand(SaveResultsAsync, () => Files.Count > 0, HandleCommandError);
        LoadResultsCommand = new AsyncRelayCommand(LoadResultsAsync, () => !IsScanning, HandleCommandError);
        ExportAllCommand = new AsyncRelayCommand(
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
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
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
        _filter.ExtensionFilter = _extensionFilter;
        RefreshView();
    }

    private void ClearFilters()
    {
        _filterTimer.Stop();
        _searchText = string.Empty;
        _extensionFilter = string.Empty;
        _selectedFilter = FilterOptions[0];
        _filter = new FileFilter();
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(ExtensionFilter));
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
            StatusMessage = "剪贴板中没有有效路径";
            _dialog.ShowInfo("剪贴板中没有可识别的文件或目录路径。每行粘贴一个路径。", "粘贴路径");
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
                    _dialog.ShowInfo("扫描进行中，无法清空列表。请先取消扫描。", "打开文件");
                    return;
                }

                ClearListCore();
            }

            var options = BuildScanOptions();
            var discovery = await Task.Run(() => _discovery.Discover(paths, options, _registry));
            var added = AddDiscovered(discovery.Files);
            EnqueueAddedItems(added);

            if (discovery.Errors.Count > 0)
            {
                _dialog.ShowError(FormatIssues("以下路径无法添加：", discovery.Errors), "添加文件");
            }
            else if (discovery.Warnings.Count > 0)
            {
                _dialog.ShowWarning(FormatIssues("部分目录无法完整读取：", discovery.Warnings), "扫描提示");
            }

            StatusMessage = added.Count == 0
                ? "没有添加新文件（可能全部重复或不符合过滤条件）"
                : $"已添加 {added.Count} 个文件";

            if (added.Count > 0)
            {
                UpdateLastDirectory(Path.GetDirectoryName(added[0].FilePath));
            }

            PersistSettings();
        }
        catch (Exception ex)
        {
            _log.Error("添加文件失败", ex);
            _dialog.ShowError($"添加文件失败：{ex.Message}");
            StatusMessage = "添加文件失败";
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
            StatusMessage = "没有待扫描的文件";
            _dialog.ShowInfo("没有待扫描的文件。若要重新检测已完成的文件，请选中后使用“重新检测选中”。", "开始扫描");
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

        var targets = SelectedFiles.ToList();
        if (targets.Count == 0)
        {
            return;
        }

        await RunScanAsync(targets);
    }

    private async Task RunScanAsync(IReadOnlyList<FileItemViewModel> targets)
    {
        if (IsScanning || targets.Count == 0)
        {
            return;
        }

        var options = BuildScanOptions();
        foreach (var item in targets)
        {
            item.SetScanning(true);
        }

        _scanCts = new CancellationTokenSource();
        _session = _coordinator.Start(
            options,
            targets.Select(item => new ScanWorkItem(item.Id, item.FilePath)),
            _scanCts.Token);

        IsScanning = true;
        IsPaused = false;
        StatusMessage = "正在扫描…";
        _scanStartedAt = DateTime.UtcNow;
        ProgressValue = 0;
        CurrentDirectory = string.Empty;
        CurrentFile = string.Empty;
        HashProgressText = string.Empty;
        ElapsedText = "00:00:00";
        _uiTimer.Start();
        _log.Info(
            $"开始扫描：{targets.Count} 个文件，并发 {options.MaxConcurrency}，" +
            $"模式 {StatusText.DescribeScanMode(options.ScanMode)}，吊销 {StatusText.DescribeRevocationMode(options.RevocationMode)}");

        try
        {
            await _session.WaitForCompletionAsync(_scanCts.Token);
            StatusMessage = "扫描完成";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "扫描已取消";
            _log.Info("用户取消扫描");
        }
        catch (Exception ex)
        {
            StatusMessage = "扫描失败";
            _log.Error("扫描失败", ex);
            _dialog.ShowError($"扫描失败：{ex.Message}");
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
        IsPaused = false;
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
            ? $"{Path.GetFileName(hash.FilePath)}：{hash.Percentage:0}%"
            : string.Empty;
        ElapsedText = FormatElapsed(progress.Elapsed);

        if (progress.IsPaused != IsPaused)
        {
            IsPaused = progress.IsPaused;
        }
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

    private void TogglePause()
    {
        if (_session is null)
        {
            return;
        }

        if (IsPaused)
        {
            _session.Resume();
            IsPaused = false;
            StatusMessage = "正在扫描…";
        }
        else
        {
            _session.Pause();
            IsPaused = true;
            StatusMessage = "已暂停";
        }
    }

    private void CancelScan()
    {
        if (!IsScanning)
        {
            return;
        }

        StatusMessage = "正在取消…";
        _session?.Resume();
        _scanCts?.Cancel();
    }

    private void ClearList()
    {
        if (IsScanning)
        {
            _dialog.ShowInfo("扫描进行中，无法清空列表。请先取消扫描。", "清空列表");
            return;
        }

        if (Files.Count > 0 && !_dialog.Confirm("确定清空当前列表？扫描结果将全部丢失。", "清空列表"))
        {
            return;
        }

        ClearListCore();
    }

    private void ClearListCore()
    {
        _registry.Clear();
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
        StatusMessage = "列表已清空";
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

        var removed = new List<FileItemViewModel>();
        foreach (var item in selected)
        {
            if (!_itemsById.Remove(item.Id))
            {
                continue;
            }

            removed.Add(item);
            _registry.Remove(item.FilePath);
            DecrementStatistics(item.Result);
            _totalCount = Math.Max(0, _totalCount - 1);
        }

        if (removed.Count == 0)
        {
            return;
        }

        Files.RemoveRange(removed);
        _selectedFiles.Clear();
        NotifySelectionChanged();
        RenumberIndices();
        NotifyStatistics();
        RefreshView();
        StatusMessage = $"已移除 {removed.Count} 个文件";
        CommandManager.InvalidateRequerySuggested();
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
            _dialog.ShowInfo("列表为空，没有可保存的结果。", "保存结果");
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
            StatusMessage = $"已保存 {results.Count} 条结果";
            _dialog.ShowInfo($"已保存 {results.Count} 条扫描结果。", "保存结果");
        }
        catch (Exception ex)
        {
            _log.Error("保存扫描结果失败", ex);
            _dialog.ShowError($"保存失败：{ex.Message}", "保存结果");
        }
    }

    private async Task LoadResultsAsync()
    {
        if (IsScanning)
        {
            _dialog.ShowInfo("扫描进行中，无法加载结果。", "加载结果");
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
            StatusMessage = $"已加载 {loaded.Count} 条扫描结果";
        }
        catch (Exception ex)
        {
            _log.Error("加载扫描结果失败", ex);
            _dialog.ShowError($"加载失败：{ex.Message}", "加载结果");
        }
    }

    private async Task ExportAsync(bool filteredOnly)
    {
        var results = filteredOnly
            ? FilesView.Cast<FileItemViewModel>().Select(item => item.Result).ToList()
            : Files.Select(item => item.Result).ToList();

        if (results.Count == 0)
        {
            _dialog.ShowInfo("没有可导出的结果。", "导出");
            return;
        }

        var path = _dialog.SaveExportFile(
            $"sigwalk-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            _settings.LastDirectory);

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            var format = ExportService.DetectFormat(path);
            await Task.Run(() => _exporter.Export(path, format, results));
            UpdateLastDirectory(Path.GetDirectoryName(path));
            PersistSettings();
            StatusMessage = $"已导出 {results.Count} 条结果到 {Path.GetFileName(path)}";
            _dialog.ShowInfo($"已导出 {results.Count} 条结果：\n{path}", "导出完成");
        }
        catch (Exception ex)
        {
            _log.Error("导出失败", ex);
            _dialog.ShowError($"导出失败：{ex.Message}", "导出");
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
            StatusMessage = "请先选择文件";
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
        StatusMessage = "设置已保存";
        _log.Info("设置已保存");
    }

    private void ApplySettings(AppSettings updated)
    {
        _settings.Theme = updated.Theme;
        _settings.Recursive = updated.Recursive;
        _settings.Extensions = updated.Extensions;
        _settings.MaxConcurrency = updated.MaxConcurrency;
        _settings.ScanMode = updated.ScanMode;
        _settings.RevocationMode = updated.RevocationMode;
        _settings.AutoSortAfterScan = updated.AutoSortAfterScan;
        _themeService.Apply(_settings.Theme);
        PersistSettings();
        OnPropertyChanged(nameof(Recursive));
        OnPropertyChanged(nameof(ScanModeText));
        OnPropertyChanged(nameof(RevocationModeText));
        OnPropertyChanged(nameof(ThemeText));
        OnPropertyChanged(nameof(ConcurrencyText));
    }

    private void ToggleTheme()
    {
        _settings.Theme = _settings.Theme == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
        _themeService.Apply(_settings.Theme);
        PersistSettings();
        OnPropertyChanged(nameof(ThemeText));
        StatusMessage = $"已切换到{StatusText.DescribeThemeMode(_settings.Theme)}主题";
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

        if (_clipboard.TrySetText(string.Join(Environment.NewLine, values)))
        {
            StatusMessage = "已复制到剪贴板";
        }
        else
        {
            StatusMessage = "复制失败：剪贴板被占用";
        }
    }

    private void CopySelectedRows()
    {
        if (SelectedFiles.Count == 0)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("文件名称\t文件路径\t文件大小\t文件类型\t修改时间\t签名状态\t签名者\t证书颁发者\tSHA1\tSHA256\t扫描状态");
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
            StatusMessage = $"已复制 {SelectedFiles.Count} 行";
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
                $"{signature.DisplayName}\t状态={signature.StatusText}\t签名者={signature.SignerName}\t" +
                $"颁发者={signature.IssuerName}\t有效期={signature.ValidityText}\t" +
                $"算法={signature.SignatureAlgorithm}/{signature.DigestAlgorithm}\t" +
                $"时间戳签名者={signature.Timestamp?.SignerName}\t时间戳时间={signature.Timestamp?.TimestampText}");
            if (!string.IsNullOrWhiteSpace(signature.ErrorMessage))
            {
                builder.AppendLine($"  错误：{signature.ErrorMessage}");
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

    private void HandleCommandError(Exception exception)
    {
        _log.Error("命令执行失败", exception);
        _dialog.ShowError($"操作失败：{exception.Message}");
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
            builder.AppendLine($"… 其余 {issues.Count - maxLines} 条已写入日志");
        }

        return builder.ToString();
    }
}
