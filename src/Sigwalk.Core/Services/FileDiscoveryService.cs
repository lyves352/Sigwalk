using Sigwalk.Core.Models;
using Sigwalk.Core.Utilities;

namespace Sigwalk.Core.Services;

/// <summary>路径展开结果。</summary>
public sealed class DiscoveryResult
{
    public List<string> Files { get; } = new();
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
}

/// <summary>
/// 文件与目录发现服务：展开拖放/粘贴/对话框输入的路径，
/// 处理递归、扩展名过滤、无权限目录和重解析点。
/// </summary>
public sealed class FileDiscoveryService
{
    /// <summary>展开一组输入路径（文件或目录）。</summary>
    public DiscoveryResult Discover(
        IEnumerable<string> inputs,
        ScanOptions options,
        PathRegistry? registry = null,
        CancellationToken cancellationToken = default)
    {
        var result = new DiscoveryResult();
        var localSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seen = registry is null ? localSeen : null;

        foreach (var raw in inputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var input = NormalizeInputPath(raw);
            if (string.IsNullOrEmpty(input))
            {
                continue;
            }

            if (File.Exists(input))
            {
                // 用户显式添加的文件不受目录扩展名过滤限制。
                AddFile(input, result, registry, seen);
                continue;
            }

            if (Directory.Exists(input))
            {
                DiscoverDirectory(input, options, result, registry, seen, cancellationToken);
                continue;
            }

            result.Errors.Add($"路径不存在：{input}");
        }

        result.Files.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    /// <summary>把剪贴板文本按行拆分为路径（每行一个路径，保留空格）。</summary>
    public static List<string> SplitPastedPaths(string? text)
    {
        var paths = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return paths;
        }

        foreach (var line in text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
        {
            var value = NormalizeInputPath(line);
            if (!string.IsNullOrEmpty(value))
            {
                paths.Add(value);
            }
        }

        return paths;
    }

    /// <summary>清理输入路径：去空白、去成对引号、展开环境变量、转完整路径。</summary>
    public static string? NormalizeInputPath(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim();
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
        {
            value = value[1..^1].Trim();
        }

        if (value.Length == 0)
        {
            return null;
        }

        value = Environment.ExpandEnvironmentVariables(value);
        try
        {
            return Path.GetFullPath(value);
        }
        catch
        {
            return value;
        }
    }

    private static void DiscoverDirectory(
        string root,
        ScanOptions options,
        DiscoveryResult result,
        PathRegistry? registry,
        HashSet<string>? seen,
        CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();

            try
            {
                foreach (var file in Directory.GetFiles(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (ScanOptions.IsExtensionIncluded(file, options.Extensions))
                    {
                        AddFile(file, result, registry, seen);
                    }
                }
            }
            catch (Exception ex) when (IsRecoverable(ex))
            {
                result.Warnings.Add($"无法枚举文件：{directory}（{ErrorText.Describe(ex)}）");
            }

            if (!options.Recursive)
            {
                continue;
            }

            try
            {
                foreach (var child in Directory.GetDirectories(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (IsReparsePoint(child, result))
                    {
                        continue;
                    }

                    pending.Push(child);
                }
            }
            catch (Exception ex) when (IsRecoverable(ex))
            {
                result.Warnings.Add($"无法枚举子目录：{directory}（{ErrorText.Describe(ex)}）");
            }
        }
    }

    private static bool IsReparsePoint(string directory, DiscoveryResult result)
    {
        try
        {
            var attributes = File.GetAttributes(directory);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                result.Warnings.Add($"已跳过重解析点目录：{directory}");
                return true;
            }
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            result.Warnings.Add($"无法读取目录属性：{directory}（{ErrorText.Describe(ex)}）");
            return true;
        }

        return false;
    }

    private static void AddFile(string file, DiscoveryResult result, PathRegistry? registry, HashSet<string>? seen)
    {
        if (registry is not null)
        {
            if (registry.TryAdd(file))
            {
                result.Files.Add(file);
            }
            return;
        }

        if (seen is not null && seen.Add(PathRegistry.Normalize(file)))
        {
            result.Files.Add(file);
        }
    }

    private static bool IsRecoverable(Exception ex) =>
        ex is UnauthorizedAccessException or IOException or PathTooLongException or System.Security.SecurityException;
}
