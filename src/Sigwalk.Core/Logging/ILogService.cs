namespace Sigwalk.Core.Logging;

/// <summary>日志服务抽象，便于测试替换。</summary>
public interface ILogService
{
    string LogDirectory { get; }
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? exception = null);
}

/// <summary>空日志实现。</summary>
public sealed class NullLogService : ILogService
{
    public static readonly NullLogService Instance = new();

    public string LogDirectory => string.Empty;

    public void Info(string message)
    {
    }

    public void Warn(string message)
    {
    }

    public void Error(string message, Exception? exception = null)
    {
    }
}
