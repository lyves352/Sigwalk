using System.Runtime.InteropServices;
using System.Windows;

namespace Sigwalk.Services;

/// <summary>剪贴板读写，带少量重试以应对其他程序临时占用剪贴板。</summary>
public sealed class ClipboardService
{
    public bool TrySetText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch (COMException)
            {
                Thread.Sleep(50);
            }
            catch (Exception)
            {
                return false;
            }
        }

        return false;
    }

    public string? GetText()
    {
        try
        {
            return Clipboard.ContainsText() ? Clipboard.GetText() : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
