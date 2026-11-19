using System.IO;
using System.Windows;
using Microsoft.Win32;
using Sigwalk.Core.Localization;
using Sigwalk.Core.Logging;
using Sigwalk.Core.Models;
using Sigwalk.Core.Services.Export;
using Sigwalk.ViewModels;
using Sigwalk.Views;

namespace Sigwalk.Services;

/// <summary>集中管理文件对话框与消息提示，避免 ViewModel 直接依赖 WPF 控件。</summary>
public sealed class DialogService
{
    private readonly ILogService? _log;

    public DialogService(ILogService? log = null)
    {
        _log = log;
    }

    public string[] OpenFiles(string? initialDirectory)
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.T("Dialog.Title.OpenFiles"),
            Filter = Loc.T("Dialog.Filter.ScanFiles"),
            Multiselect = true,
            CheckFileExists = true,
        };

        ApplyInitialDirectory(dialog, initialDirectory);
        return dialog.ShowDialog(GetOwner()) == true ? dialog.FileNames : Array.Empty<string>();
    }

    public string? OpenJsonFile(string? initialDirectory)
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.T("Dialog.Title.Import"),
            Filter = Loc.T("Dialog.Filter.Json"),
            Multiselect = false,
            CheckFileExists = true,
        };

        ApplyInitialDirectory(dialog, initialDirectory);
        return dialog.ShowDialog(GetOwner()) == true ? dialog.FileName : null;
    }

    public string? OpenFolder(string? initialDirectory)
    {
        var dialog = new OpenFolderDialog
        {
            Title = Loc.T("Dialog.Title.OpenFolder"),
            Multiselect = false,
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog(GetOwner()) == true ? dialog.FolderName : null;
    }

    public string? SaveJsonFile(string defaultFileName, string? initialDirectory)
    {
        return SaveFile(Loc.T("Dialog.Filter.Json"), "json", defaultFileName, initialDirectory);
    }

    /// <summary>按设置中的导出格式生成默认扩展名，并让该格式在过滤器中优先显示。</summary>
    public string? SaveExportFile(
        string defaultFileName,
        string? initialDirectory,
        ExportFormat format = ExportFormat.Csv)
    {
        var extension = ExportService.GetExtension(format).TrimStart('.');
        return SaveFile(BuildExportFilter(format), extension, defaultFileName, initialDirectory);
    }

    public string? SaveFile(string filter, string defaultExtension, string defaultFileName, string? initialDirectory)
    {
        var dialog = new SaveFileDialog
        {
            Title = Loc.T("Dialog.Title.Save"),
            Filter = filter,
            DefaultExt = defaultExtension,
            FileName = defaultFileName,
            AddExtension = true,
            OverwritePrompt = true,
        };

        ApplyInitialDirectory(dialog, initialDirectory);
        return dialog.ShowDialog(GetOwner()) == true ? dialog.FileName : null;
    }

    public void ShowInfo(string message, string? title = null) =>
        MessageBox.Show(
            GetOwner(),
            message,
            title ?? Loc.T("Dialog.Title.Info"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);

    public void ShowWarning(string message, string? title = null) =>
        MessageBox.Show(
            GetOwner(),
            message,
            title ?? Loc.T("Dialog.Title.Warning"),
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

    public void ShowError(string message, string? title = null) =>
        MessageBox.Show(
            GetOwner(),
            message,
            title ?? Loc.T("Dialog.Title.Error"),
            MessageBoxButton.OK,
            MessageBoxImage.Error);

    public bool Confirm(string message, string? title = null) =>
        MessageBox.Show(
            GetOwner(),
            message,
            title ?? Loc.T("Dialog.Title.Confirm"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;

    public bool ShowSettings(SettingsViewModel viewModel)
    {
        _log?.Info("SettingsWindow: creating");
        var window = new SettingsWindow
        {
            DataContext = viewModel,
            Owner = GetOwner(),
        };

        _log?.Info("SettingsWindow: showing");
        var result = window.ShowDialog();
        _log?.Info($"SettingsWindow: closed, result={result}");
        return result == true;
    }

    public void ShowFileDetail(FileScanResult result, ClipboardService clipboard)
    {
        _log?.Info("FileDetailWindow: creating");
        var window = new FileDetailWindow
        {
            DataContext = new FileDetailViewModel(result, clipboard),
            Owner = GetOwner(),
        };

        _log?.Info("FileDetailWindow: showing");
        window.ShowDialog();
        _log?.Info("FileDetailWindow: closed");
    }

    public void ShowAbout()
    {
        var version = typeof(DialogService).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        ShowInfo(Loc.F("About.Text", version), Loc.T("About.Title"));
    }

    private static string BuildExportFilter(ExportFormat format)
    {
        var pairs = ParseFilter(Loc.T("Dialog.Filter.Export"));
        if (pairs.Count == 0)
        {
            return Loc.T("Dialog.Filter.Export");
        }

        var extension = ExportService.GetExtension(format);
        var ordered = pairs
            .OrderByDescending(pair => pair.Pattern.Contains(extension, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return string.Join("|", ordered.SelectMany(pair => new[] { pair.Name, pair.Pattern }));
    }

    private static List<(string Name, string Pattern)> ParseFilter(string filter)
    {
        var parts = filter.Split('|');
        var pairs = new List<(string, string)>();
        for (var index = 0; index + 1 < parts.Length; index += 2)
        {
            pairs.Add((parts[index], parts[index + 1]));
        }

        return pairs;
    }

    private static void ApplyInitialDirectory(FileDialog dialog, string? initialDirectory)
    {
        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }
    }

    private static Window? GetOwner() => Application.Current?.MainWindow;
}
