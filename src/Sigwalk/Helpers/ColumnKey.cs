using System.Windows;

namespace Sigwalk.Helpers;

/// <summary>DataGridColumn 没有 Tag 属性，用附加属性保存稳定的列键。</summary>
public static class ColumnKey
{
    public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached(
        "Key",
        typeof(string),
        typeof(ColumnKey),
        new PropertyMetadata(null));

    public static void SetKey(DependencyObject element, string? value) =>
        element.SetValue(KeyProperty, value);

    public static string? GetKey(DependencyObject element) =>
        (string?)element.GetValue(KeyProperty);
}
