using System.Diagnostics;
using System.IO;
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
            _dialog.ShowWarning($"文件不存在：{path}");
            return;
        }

        Start(new ProcessStartInfo(path) { UseShellExecute = true }, "无法打开文件");
    }

    public void OpenFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            _dialog.ShowWarning($"目录不存在：{path}");
            return;
        }

        Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, Arguments = $"\"{path}\"" },
            "无法打开目录");
    }

    /// <summary>使用 Explorer 的 /select 参数定位文件。</summary>
    public void RevealInExplorer(string path)
    {
        if (!File.Exists(path))
        {
            _dialog.ShowWarning($"文件不存在：{path}");
            return;
        }

        Start(new ProcessStartInfo("explorer.exe")
        {
            UseShellExecute = true,
            Arguments = $"/select,\"{path}\"",
        }, "无法在资源管理器中定位文件");
    }

    public void OpenLogFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, Arguments = $"\"{path}\"" },
                "无法打开日志目录");
        }
        catch (Exception ex)
        {
            _log.Error("打开日志目录失败", ex);
            _dialog.ShowError($"无法打开日志目录：{ex.Message}");
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
            _dialog.ShowError($"{errorMessage}：{ex.Message}");
        }
    }
}
