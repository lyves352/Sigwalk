using System.Windows;
using Microsoft.Win32;
using Sigwalk.Core.Models;

namespace Sigwalk.Services;

/// <summary>
/// 切换浅色/深色主题资源字典；Auto 模式跟随系统并监听系统主题变化。
/// </summary>
public sealed class ThemeService : IDisposable
{
    private const string ThemePathFragment = "Resources/Themes/";
    private const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private ThemeMode _requested = ThemeMode.Auto;
    private ThemeMode _effective = ThemeMode.Light;
    private bool _applied;

    public ThemeService()
    {
        Instance = this;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public static ThemeService? Instance { get; private set; }

    /// <summary>用户选择的模式（可能为 Auto）。</summary>
    public ThemeMode Mode => _requested;

    /// <summary>实际生效的主题（只有 Light/Dark）。</summary>
    public ThemeMode Current => _effective;

    public event EventHandler? EffectiveThemeChanged;

    public void Apply(ThemeMode theme)
    {
        // 未定义的枚举值（手工编辑配置等）按自动处理，避免加载不存在的资源字典。
        if (theme is not (ThemeMode.Auto or ThemeMode.Light or ThemeMode.Dark))
        {
            theme = ThemeMode.Auto;
        }

        _requested = theme;
        var effective = theme == ThemeMode.Auto ? ResolveSystemTheme() : theme;
        var changed = !_applied || effective != _effective;
        _effective = effective;
        ApplyDictionary(effective);
        _applied = true;

        if (changed)
        {
            EffectiveThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        if (ReferenceEquals(Instance, this))
        {
            Instance = null;
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_requested != ThemeMode.Auto)
        {
            return;
        }

        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle))
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        dispatcher.BeginInvoke(() =>
        {
            if (_requested == ThemeMode.Auto)
            {
                Apply(ThemeMode.Auto);
            }
        });
    }

    private static ThemeMode ResolveSystemTheme()
    {
        try
        {
            var value = Registry.GetValue(
                $"HKEY_CURRENT_USER\\{PersonalizeKey}",
                "AppsUseLightTheme",
                1);
            return value is int light && light == 0 ? ThemeMode.Dark : ThemeMode.Light;
        }
        catch
        {
            // 注册表不可读取时按浅色处理，不影响启动。
            return ThemeMode.Light;
        }
    }

    private static void ApplyDictionary(ThemeMode effective)
    {
        var application = Application.Current;
        if (application is null)
        {
            return;
        }

        var dictionaries = application.Resources.MergedDictionaries;
        var source = new Uri($"Resources/Themes/{effective}.xaml", UriKind.Relative);
        var dictionary = new ResourceDictionary { Source = source };

        var existing = dictionaries.FirstOrDefault(item =>
            item.Source?.OriginalString.Contains(ThemePathFragment, StringComparison.OrdinalIgnoreCase) == true);

        if (existing is not null)
        {
            var index = dictionaries.IndexOf(existing);
            dictionaries[index] = dictionary;
        }
        else
        {
            dictionaries.Insert(0, dictionary);
        }
    }
}
