using System.IO;
using System.Windows;
using Microsoft.Win32;
using Sigwalk.Core.Logging;
using Sigwalk.Core.Models;
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
    public const string ScanFileFilter =
        "可执行文件 (*.exe;*.dll;*.sys;*.msi;*.ocx;*.cpl;*.scr;*.com)|*.exe;*.dll;*.sys;*.msi;*.ocx;*.cpl;*.scr;*.com|所有文件 (*.*)|*.*";

    public const string JsonFileFilter = "JSON 文件 (*.json)|*.json";

    public string[] OpenFiles(string? initialDirectory)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择要检测的文件",
            Filter = ScanFileFilter,
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
            Title = "加载扫描结果",
            Filter = JsonFileFilter,
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
            Title = "选择要扫描的目录",
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
        return SaveFile(JsonFileFilter, "json", defaultFileName, initialDirectory);
    }

    public string? SaveExportFile(string defaultFileName, string? initialDirectory)
    {
        return SaveFile(ExportFilter, "csv", defaultFileName, initialDirectory);
    }

    public const string ExportFilter =
        "CSV 文件 (*.csv)|*.csv|Excel 工作簿 (*.xlsx)|*.xlsx|JSON 文件 (*.json)|*.json";

    public string? SaveFile(string filter, string defaultExtension, string defaultFileName, string? initialDirectory)
    {
        var dialog = new SaveFileDialog
        {
            Title = "保存",
            Filter = filter,
            DefaultExt = defaultExtension,
            FileName = defaultFileName,
            AddExtension = true,
            OverwritePrompt = true,
        };

        ApplyInitialDirectory(dialog, initialDirectory);
        return dialog.ShowDialog(GetOwner()) == true ? dialog.FileName : null;
    }

    public void ShowInfo(string message, string title = "提示") =>
        MessageBox.Show(GetOwner(), message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowWarning(string message, string title = "警告") =>
        MessageBox.Show(GetOwner(), message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void ShowError(string message, string title = "错误") =>
        MessageBox.Show(GetOwner(), message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public bool Confirm(string message, string title = "确认") =>
        MessageBox.Show(GetOwner(), message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) ==
        MessageBoxResult.Yes;

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
        ShowInfo(
            $"Sigwalk 文件数字签名检测工具\n版本：{version}\n\n" +
            "基于 Windows Authenticode（WinVerifyTrust）验证嵌入式数字签名，\n" +
            "支持 SHA1/SHA256 哈希、多重签名、时间戳与证书链信息展示。\n\n" +
            "注意：当前版本不检测 Windows 目录签名（Catalog）。",
            "关于 Sigwalk");
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
