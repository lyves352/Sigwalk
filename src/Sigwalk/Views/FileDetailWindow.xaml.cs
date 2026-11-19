using System.Windows;
using Sigwalk.Services;

namespace Sigwalk.Views;

public partial class FileDetailWindow : Window
{
    public FileDetailWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowThemeHelper.Apply(this);
        ContentRendered += (_, _) => WindowThemeHelper.Apply(this);
        Activated += (_, _) => WindowThemeHelper.Apply(this);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
