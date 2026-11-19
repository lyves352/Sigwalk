using System.Globalization;

namespace Sigwalk.Core.Utilities;

/// <summary>文件大小格式化（自动选择 B/KB/MB/GB/TB）。</summary>
public static class FileSizeFormatter
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB", "PB" };

    public static string Format(long bytes)
    {
        if (bytes < 0)
        {
            return "—";
        }

        if (bytes < 1024)
        {
            return bytes.ToString(CultureInfo.InvariantCulture) + " B";
        }

        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return value.ToString("0.##", CultureInfo.InvariantCulture) + " " + Units[unit];
    }
}
