namespace Sigwalk.Core.Services;

/// <summary>文件路径去重注册表（Windows 路径不区分大小写）。</summary>
public sealed class PathRegistry
{
    private readonly HashSet<string> _paths;
    private readonly object _sync = new();

    public PathRegistry()
        : this(StringComparer.OrdinalIgnoreCase)
    {
    }

    public PathRegistry(IEqualityComparer<string> comparer)
    {
        _paths = new HashSet<string>(comparer);
    }

    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _paths.Count;
            }
        }
    }

    public bool TryAdd(string path)
    {
        var normalized = Normalize(path);
        lock (_sync)
        {
            return _paths.Add(normalized);
        }
    }

    public bool Add(string path)
    {
        var normalized = Normalize(path);
        lock (_sync)
        {
            return _paths.Add(normalized);
        }
    }

    public bool Remove(string path)
    {
        var normalized = Normalize(path);
        lock (_sync)
        {
            return _paths.Remove(normalized);
        }
    }

    public bool Contains(string path)
    {
        var normalized = Normalize(path);
        lock (_sync)
        {
            return _paths.Contains(normalized);
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _paths.Clear();
        }
    }

    /// <summary>统一为完整路径；无效路径原样返回。</summary>
    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var value = path.Trim().Trim('"');
        try
        {
            return Path.GetFullPath(value);
        }
        catch
        {
            return value;
        }
    }
}
