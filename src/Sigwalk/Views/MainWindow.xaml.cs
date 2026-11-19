using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Sigwalk.ViewModels;

namespace Sigwalk.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // ContextMenu 不在可视树中，显式赋予 DataContext，避免绑定找不到命令。
        if (FilesGrid.ContextMenu is { } menu)
        {
            menu.DataContext = ViewModel;
        }
    }

    private void FilesGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (FilesGrid.ContextMenu is { } menu)
        {
            menu.DataContext = ViewModel;
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
        {
            e.Handled = true;
            var viewModel = ViewModel;
            if (viewModel is not null)
            {
                await viewModel.AddPathsFromDropAsync(paths);
            }
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel?.IsScanning != true)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            "扫描正在进行，退出将取消扫描。是否退出？",
            "退出 Sigwalk",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            return;
        }

        viewModel.OnApplicationExit();
    }

    private void FilesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel?.SetSelection(FilesGrid.SelectedItems.Cast<FileItemViewModel>());
    }

    private void FilesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(FilesGrid, e.OriginalSource as DependencyObject) is DataGridRow)
        {
            ViewModel?.OpenSelectedFile();
        }
    }

    private void FilesGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(FilesGrid, e.OriginalSource as DependencyObject) is DataGridRow row)
        {
            if (!row.IsSelected)
            {
                FilesGrid.SelectedItems.Clear();
                row.IsSelected = true;
            }

            // 某些输入路径下 SelectionChanged 可能稍后触发，这里显式同步一次，
            // 确保右键菜单命令立即获得正确的选中项。
            if (row.Item is FileItemViewModel item)
            {
                var selection = FilesGrid.SelectedItems.Cast<FileItemViewModel>().ToList();
                if (selection.Count == 0)
                {
                    selection.Add(item);
                }

                ViewModel?.SetSelection(selection);
            }
        }
    }

    /// <summary>列标题右键菜单：显示/隐藏指定列（纯视图行为）。</summary>
    private void ColumnHeader_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var menu = new ContextMenu
        {
            PlacementTarget = sender as UIElement ?? FilesGrid,
            Placement = PlacementMode.Bottom,
        };

        foreach (var column in FilesGrid.Columns)
        {
            var target = column;
            var item = new MenuItem
            {
                Header = column.Header?.ToString() ?? "列",
                IsCheckable = true,
                IsChecked = column.Visibility == Visibility.Visible,
                StaysOpenOnClick = true,
            };
            item.Click += (_, _) =>
            {
                target.Visibility = target.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            };
            menu.Items.Add(item);
        }

        menu.IsOpen = true;
        e.Handled = true;
    }
}
