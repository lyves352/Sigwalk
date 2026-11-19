using System.Windows;
using System.Windows.Interop;
using Sigwalk.Core.Models;
using Sigwalk.Interop;

namespace Sigwalk.Services;

/// <summary>让原生标题栏跟随应用主题（DWM immersive dark mode）。</summary>
public static class WindowThemeHelper
{
    public static void Apply(Window window)
    {
        var dark = ThemeService.Instance?.Current == ThemeMode.Dark;
        Apply(window, dark);
    }

    public static void Apply(Window window, bool dark)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var value = dark ? 1 : 0;
        var result = DwmApi.DwmSetWindowAttribute(
            handle,
            DwmApi.DwmwaUseImmersiveDarkMode,
            ref value,
            sizeof(int));

        if (result != 0)
        {
            DwmApi.DwmSetWindowAttribute(
                handle,
                DwmApi.DwmwaUseImmersiveDarkModeBefore20H1,
                ref value,
                sizeof(int));
        }

        // 在 Windows 11 上，系统深色模式下仅设置沉浸式深色模式为 0 不一定能强制浅色标题栏，
        // 因此直接指定标题栏、边框与文字颜色，使其与应用主题一致。
        var caption = dark ? 0x001F1C1B : 0x00F8F5F3; // COLORREF: 0x00BBGGRR
        var text = dark ? 0x00EBE8E6 : 0x0028231F;
        var border = dark ? 0x00443D3A : 0x00DED7D0;
        DwmApi.DwmSetWindowAttribute(handle, DwmApi.DwmwaCaptionColor, ref caption, sizeof(int));
        DwmApi.DwmSetWindowAttribute(handle, DwmApi.DwmwaTextColor, ref text, sizeof(int));
        DwmApi.DwmSetWindowAttribute(handle, DwmApi.DwmwaBorderColor, ref border, sizeof(int));

        // 某些系统需要非客户区重绘后才会立即切换标题栏颜色。
        DwmApi.SetWindowPos(
            handle,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            DwmApi.SwpNoMove | DwmApi.SwpNoSize | DwmApi.SwpNoZOrder |
            DwmApi.SwpNoActivate | DwmApi.SwpFrameChanged);
    }

    public static void ApplyAll()
    {
        var application = Application.Current;
        if (application is null)
        {
            return;
        }

        foreach (Window window in application.Windows)
        {
            Apply(window);
        }
    }
}
