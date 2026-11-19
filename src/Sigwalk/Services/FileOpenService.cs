using System.Diagnostics;
using System.IO;
using Sigwalk.Core.Localization;
using Sigwalk.Core.Logging;

namespace Sigwalk.Services;

/// <summary>打开文件、目录，或在资源管理器中定位文件。</summary>
public sealed class FileOpenService
{
    private readonly DialogService _dialog;
    private readonly ILogService _log;

    public FileOpenService(DialogService dialog, ILogService log)
    {
        _dialog = dialog;
        _log = log;
    }

    public void OpenFile(string path)
    {
        if (!File.Exists(path))
        {
            _dialog.ShowWarning(Loc.F("Error.FileNotFoundDetail", path));
            return;
        }

        Start(new ProcessStartInfo(path) { UseShellExecute = true }, Loc.T("Error.OpenFile"));
    }

    public void OpenFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            _dialog.ShowWarning(Loc.F("Error.DirectoryNotFoundDetail", path));
            return;
        }

        Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, Arguments = $"\"{path}\"" },
            Loc.T("Error.OpenDirectory"));
    }

    /// <summary>使用 Explorer 的 /select 参数定位文件。</summary>
    public void RevealInExplorer(string path)
    {
        if (!File.Exists(path))
        {
            _dialog.ShowWarning(Loc.F("Error.FileNotFoundDetail", path));
            return;
        }

        Start(new ProcessStartInfo("explorer.exe")
        {
            UseShellExecute = true,
            Arguments = $"/select,\"{path}\"",
        }, Loc.T("Error.RevealInExplorer"));
    }

    public void OpenLogFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, Arguments = $"\"{path}\"" },
                Loc.T("Error.OpenLogFolder"));
        }
        catch (Exception ex)
        {
            _log.Error("打开日志目录失败", ex);
            _dialog.ShowError(Loc.F("Error.OpenLogFolderDetail", ex.Message));
        }
    }

    private void Start(ProcessStartInfo startInfo, string errorMessage)
    {
        try
        {
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            _log.Error($"{errorMessage}：{startInfo.FileName}", ex);
            _dialog.ShowError(Loc.F("Error.ActionFailed", errorMessage, ex.Message));
        }
    }
}
