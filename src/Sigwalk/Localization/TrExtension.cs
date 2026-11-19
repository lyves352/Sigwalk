using System.Windows.Data;
using System.Windows.Markup;
using Sigwalk.Core.Localization;

namespace Sigwalk.Localization;

/// <summary>
/// XAML 本地化标记扩展：绑定到 LocalizationService 的索引器，
/// 语言切换时 WPF 会自动刷新所有使用该扩展的 DependencyProperty。
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension()
    {
    }

    public TrExtension(string key)
    {
        Key = key;
    }

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = LocalizationService.Current,
            Mode = BindingMode.OneWay,
        };

        return binding.ProvideValue(serviceProvider);
    }
}
