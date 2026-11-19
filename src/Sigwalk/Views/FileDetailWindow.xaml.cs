using System.Windows;

namespace Sigwalk.Views;

public partial class FileDetailWindow : Window
{
    public FileDetailWindow()
    {
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
