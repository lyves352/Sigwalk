using Sigwalk.Core.Services;

namespace Sigwalk.Core.Services.Scanning;

public sealed record RescanPlan(
    IReadOnlyList<string> RemovedPaths,
    IReadOnlyList<string> AddedPaths,
    IReadOnlyList<string> RescanPaths);

/// <summary>
/// 计算重新检测时需要移除/新增/重检的路径。
/// 纯逻辑、无 UI 依赖，便于单元测试。
/// </summary>
public static class RescanPlanner
{
    public static RescanPlan Create(
        IReadOnlyList<string> roots,
        DiscoveryResult discovery,
        IReadOnlyList<string> existingPaths,
        IReadOnlyList<string> selectedPaths,
        Func<string, bool>? fileExists = null)
    {
        fileExists ??= File.Exists;

        var normalizedRoots = roots
            .Select(PathRegistry.Normalize)
            .Where(path => path.Length > 0)
            .ToList();

        var discovered = discovery.Files
            .Select(PathRegistry.Normalize)
            .Where(path => path.Length > 0)
            .ToList();
        var discoveredSet = new HashSet<string>(discovered, StringComparer.OrdinalIgnoreCase);

        var existing = existingPaths
            .Select(PathRegistry.Normalize)
            .Where(path => path.Length > 0)
            .ToList();
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        var removed = new List<string>();
        var survivors = new List<string>();
        foreach (var path in existing)
        {
            var coveredByRoot = normalizedRoots.Any(root => IsUnderRoot(path, root));
            var keep = coveredByRoot ? discoveredSet.Contains(path) : fileExists(path);
            if (keep)
            {
                survivors.Add(path);
            }
            else
            {
                removed.Add(path);
            }
        }

        var added = discovered
            .Where(path => !existingSet.Contains(path))
            .ToList();

        var selected = selectedPaths
            .Select(PathRegistry.Normalize)
            .Where(path => path.Length > 0)
            .ToList();

        var rescan = selected.Count > 0
            ? survivors
                .Where(path => selected.Contains(path, StringComparer.OrdinalIgnoreCase))
                .ToList()
            : survivors.Concat(added).ToList();

        return new RescanPlan(removed, added, rescan);
    }

    private static bool IsUnderRoot(string path, string root)
    {
        if (string.Equals(path, root, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
