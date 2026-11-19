namespace Sigwalk.Core.Utilities;

/// <summary>将常见文件异常转换为用户可读的中文说明。</summary>
public static class ErrorText
{
    public static string Describe(Exception exception)
    {
        var message = exception.Message.Trim();
        return exception switch
        {
            FileNotFoundException => "文件不存在" + Suffix(message),
            DirectoryNotFoundException => "目录不存在" + Suffix(message),
            UnauthorizedAccessException => "没有读取权限" + Suffix(message),
            PathTooLongException => "文件路径过长" + Suffix(message),
            System.Security.SecurityException => "安全策略拒绝访问" + Suffix(message),
            IOException => "文件读取失败" + Suffix(message),
            _ => message.Length == 0 ? exception.GetType().Name : $"{exception.GetType().Name}: {message}",
        };
    }

    private static string Suffix(string message) =>
        string.IsNullOrWhiteSpace(message) ? string.Empty : $"（{message}）";
}
