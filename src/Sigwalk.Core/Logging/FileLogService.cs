using System.Text;

namespace Sigwalk.Core.Logging;

/// <summary>按天追加写入日志文件，线程安全；日志失败不会影响主流程。</summary>
public sealed class FileLogService : ILogService
{
    private static readonly object SyncRoot = new();
    private readonly string _logDirectory;

    public FileLogService(string? logDirectory = null)
    {
        _logDirectory = string.IsNullOrWhiteSpace(logDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Sigwalk",
                "logs")
            : logDirectory;
    }

    public string LogDirectory => _logDirectory;

    public string CurrentLogFilePath => Path.Combine(_logDirectory, $"sigwalk-{DateTime.Now:yyyyMMdd}.log");

    public void Info(string message) => Write("INFO", message, null);

    public void Warn(string message) => Write("WARN", message, null);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        var line = new StringBuilder()
            .Append('[')
            .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
            .Append("] [")
            .Append(level)
            .Append("] ")
            .Append(message);

        if (exception is not null)
        {
            line.AppendLine();
            line.Append(exception);
        }

        try
        {
            lock (SyncRoot)
            {
                Directory.CreateDirectory(_logDirectory);
                File.AppendAllText(CurrentLogFilePath, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // 日志写入失败不应影响扫描或界面。
        }
    }
}
