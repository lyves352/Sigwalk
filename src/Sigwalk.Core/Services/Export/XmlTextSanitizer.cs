using System.Text;

namespace Sigwalk.Core.Services.Export;

/// <summary>清理 XML 1.0 非法字符并限制单元格长度。</summary>
public static class XmlTextSanitizer
{
    public const int MaxCellLength = 32767;

    public static string Clean(string? value, int maxLength = MaxCellLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(Math.Min(value.Length, maxLength));
        foreach (var rune in value.EnumerateRunes())
        {
            if (builder.Length >= maxLength)
            {
                break;
            }

            builder.Append(IsValidXmlChar(rune.Value) ? rune.ToString() : " ");
        }

        return builder.ToString();
    }

    private static bool IsValidXmlChar(int code) =>
        code is 0x9 or 0xA or 0xD ||
        (code >= 0x20 && code <= 0xD7FF) ||
        (code >= 0xE000 && code <= 0xFFFD) ||
        (code >= 0x10000 && code <= 0x10FFFF);
}
