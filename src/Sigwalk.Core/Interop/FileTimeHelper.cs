namespace Sigwalk.Core.Interop;

/// <summary>FILETIME 与 DateTime 转换。</summary>
internal static class FileTimeHelper
{
    /// <summary>把 FILETIME 转为 UTC DateTime；零值或越界返回 null。</summary>
    public static DateTime? ToDateTime(WinFileTime value)
    {
        var ticks = ((long)value.High << 32) | value.Low;
        if (ticks <= 0)
        {
            return null;
        }

        try
        {
            return DateTime.FromFileTimeUtc(ticks);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
