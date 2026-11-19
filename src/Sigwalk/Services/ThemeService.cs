using System.Windows;
using Sigwalk.Core.Models;

namespace Sigwalk.Services;

/// <summary>切换浅色/深色主题资源字典。</summary>
public sealed class ThemeService
{
    private const string ThemePathFragment = "Resources/Themes/";

    public ThemeMode Current { get; private set; } = ThemeMode.Light;

    public void Apply(ThemeMode theme)
    {
        var application = Application.Current;
        if (application is null)
        {
            Current = theme;
            return;
        }

        var dictionaries = application.Resources.MergedDictionaries;
        var source = new Uri($"Resources/Themes/{theme}.xaml", UriKind.Relative);
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

        Current = theme;
    }
}
