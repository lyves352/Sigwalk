using System.Runtime.InteropServices;

namespace Sigwalk.Interop;

/// <summary>DWM 深色标题栏与窗口重绘。</summary>
internal static class DwmApi
{
    internal const int DwmwaUseImmersiveDarkMode = 20;
    internal const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;

    // Windows 11 允许直接指定标题栏/边框/文字颜色；用于在系统深色模式下强制浅色标题栏。
    internal const int DwmwaBorderColor = 34;
    internal const int DwmwaCaptionColor = 35;
    internal const int DwmwaTextColor = 36;

    internal const uint SwpNoSize = 0x0001;
    internal const uint SwpNoMove = 0x0002;
    internal const uint SwpNoZOrder = 0x0004;
    internal const uint SwpNoActivate = 0x0010;
    internal const uint SwpFrameChanged = 0x0020;

    [DllImport("dwmapi.dll")]
    internal static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int value,
        int valueSize);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetWindowPos(
        IntPtr hwnd,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
