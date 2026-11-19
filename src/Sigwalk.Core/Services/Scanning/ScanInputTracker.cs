namespace Sigwalk.Core.Services.Scanning;

/// <summary>
/// 记录用户导入的文件/目录根，用于重新检测时按当前磁盘状态重新枚举。
/// 路径统一规范化并以不区分大小写去重。
/// </summary>
public sealed class ScanInputTracker
{
    private readonly List<string> _roots = new();
    private readonly HashSet<string> _set = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Roots => _roots;

    public void AddRange(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            var normalized = PathRegistry.Normalize(path);
            if (string.IsNullOrEmpty(normalized))
            {
                continue;
            }

            if (_set.Add(normalized))
            {
                _roots.Add(normalized);
            }
        }
    }

    public bool Remove(string path)
    {
        var normalized = PathRegistry.Normalize(path);
        if (!_set.Remove(normalized))
        {
            return false;
        }

        _roots.RemoveAll(root => string.Equals(root, normalized, StringComparison.OrdinalIgnoreCase));
        return true;
    }

    public bool Contains(string path) => _set.Contains(PathRegistry.Normalize(path));

    public void Clear()
    {
        _roots.Clear();
        _set.Clear();
    }
}
