using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Sigwalk.Cli;

/// <summary>
/// WinExe 进程默认没有控制台：从 cmd/PowerShell 启动时附加到父进程控制台，
/// 必要时创建控制台，并重新绑定 UTF-8 标准输出/错误。
/// </summary>
internal static class ConsoleHost
{
    private const uint AttachParentProcess = 0xFFFFFFFF;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int stdHandle);

    private const int StdOutputHandle = -11;

    public static void Ensure()
    {
        var attached = AttachConsole(AttachParentProcess);
        if (!attached)
        {
            // 无父控制台时：仅在没有有效的重定向输出句柄时创建新控制台。
            var handle = GetStdHandle(StdOutputHandle);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1))
            {
                AllocConsole();
            }
        }

        try
        {
            Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }
        catch (IOException)
        {
            // 某些宿主不允许修改控制台编码，继续使用默认编码。
        }

        // WinExe 在附加控制台前可能已经初始化了无效的 Console.Out，这里统一重新绑定；
        // 重定向场景下 OpenStandardOutput/Error 仍然返回原来的管道句柄。
        Console.SetOut(new StreamWriter(
            Console.OpenStandardOutput(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
        });
        Console.SetError(new StreamWriter(
            Console.OpenStandardError(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
        });
    }
}
