using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Sigwalk.Core.Models;

namespace Sigwalk.Core.Localization;

/// <summary>
/// 轻量本地化服务：内置中英文嵌入式 JSON 字典，避免 satellite 程序集，
/// 兼容单文件发布；语言切换时通过索引器绑定通知 WPF 刷新。
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    private const string ResourcePrefix = "Sigwalk.Core.Localization.";
    private IReadOnlyDictionary<string, string> _table;
    private IReadOnlyDictionary<string, string> _fallback;

    private LocalizationService()
    {
        _fallback = LoadTable(AppLanguage.Chinese);
        _table = _fallback;
    }

    public static LocalizationService Current { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? LanguageChanged;

    public AppLanguage Language { get; private set; } = AppLanguage.Chinese;

    /// <summary>索引器供 XAML 绑定使用（WPF 监听 "Item[]" 通知）。</summary>
    public string this[string key] => Get(key);

    public string Get(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        if (_table.TryGetValue(key, out var value))
        {
            return value;
        }

        return _fallback.TryGetValue(key, out var fallback) ? fallback : key;
    }

    public string Format(string key, params object?[] args)
    {
        var format = Get(key);
        if (args.Length == 0)
        {
            return format;
        }

        try
        {
            return string.Format(CultureInfo.CurrentCulture, format, args);
        }
        catch (FormatException)
        {
            // 字符串表损坏时不要把异常抛到扫描或导出路径。
            return format;
        }
    }

    public void SetLanguage(AppLanguage language)
    {
        if (language == Language)
        {
            return;
        }

        Language = language;
        _table = LoadTable(language);

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    internal static IReadOnlyDictionary<string, string> LoadTable(AppLanguage language)
    {
        var fileName = language == AppLanguage.English ? "en-US.json" : "zh-Hans.json";
        try
        {
            using var stream = OpenResource(fileName);
            if (stream is null)
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            var table = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
            return table ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (Exception)
        {
            // 资源缺失或 JSON 损坏时返回空表，调用方会回退到 key 文本。
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static Stream? OpenResource(string fileName)
    {
        var assembly = typeof(LocalizationService).Assembly;
        return assembly.GetManifestResourceStream(ResourcePrefix + fileName)
               ?? assembly.GetManifestResourceStream(
                   assembly.GetManifestResourceNames()
                       .FirstOrDefault(name => name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                   ?? string.Empty);
    }
}

/// <summary>业务代码使用的本地化静态门面。</summary>
public static class Loc
{
    public static string T(string key) => LocalizationService.Current[key];

    public static string F(string key, params object?[] args) => LocalizationService.Current.Format(key, args);

    public static AppLanguage Language => LocalizationService.Current.Language;

    public static void SetLanguage(AppLanguage language) => LocalizationService.Current.SetLanguage(language);
}
