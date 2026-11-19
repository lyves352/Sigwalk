using Sigwalk.Core.Localization;

namespace Sigwalk.Core.Utilities;

/// <summary>将常见文件异常转换为当前语言的用户可读说明。</summary>
public static class ErrorText
{
    public static string Describe(Exception exception)
    {
        var message = exception.Message.Trim();
        return exception switch
        {
            FileNotFoundException => Loc.T("Error.FileNotFound") + Suffix(message),
            DirectoryNotFoundException => Loc.T("Error.DirectoryNotFound") + Suffix(message),
            UnauthorizedAccessException => Loc.T("Error.AccessDenied") + Suffix(message),
            PathTooLongException => Loc.T("Error.PathTooLong") + Suffix(message),
            System.Security.SecurityException => Loc.T("Error.SecurityDenied") + Suffix(message),
            IOException => Loc.T("Error.ReadFailed") + Suffix(message),
            _ => message.Length == 0 ? exception.GetType().Name : $"{exception.GetType().Name}: {message}",
        };
    }

    private static string Suffix(string message) =>
        string.IsNullOrWhiteSpace(message) ? string.Empty : Loc.F("Error.Detail", message);
}
