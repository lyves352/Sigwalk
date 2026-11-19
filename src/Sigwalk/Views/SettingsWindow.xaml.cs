using System.Windows;
using Sigwalk.Services;
using Sigwalk.ViewModels;

namespace Sigwalk.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowThemeHelper.Apply(this);
        ContentRendered += (_, _) => WindowThemeHelper.Apply(this);
        Activated += (_, _) => WindowThemeHelper.Apply(this);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel && viewModel.Validate())
        {
            DialogResult = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
