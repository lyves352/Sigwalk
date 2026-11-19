using System.Windows;
using System.Windows.Threading;
using Sigwalk.Core.Logging;
using Sigwalk.Core.Services;
using Sigwalk.Services;
using Sigwalk.ViewModels;
using Sigwalk.Views;

namespace Sigwalk;

public partial class App : Application
{
    private FileLogService? _log;
    private MainViewModel? _mainViewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _log = new FileLogService();
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var settingsService = new SettingsService();
        var settings = settingsService.Load();

        var themeService = new ThemeService();
        themeService.Apply(settings.Theme);

        var dialogService = new DialogService(_log);
        var clipboardService = new ClipboardService();
        var fileOpenService = new FileOpenService(dialogService, _log);
        _mainViewModel = new MainViewModel(
            dialogService,
            fileOpenService,
            clipboardService,
            settingsService,
            _log,
            themeService,
            settings);

        var window = new MainWindow { DataContext = _mainViewModel };
        MainWindow = window;
        window.Show();
        _log.Info("Sigwalk 启动");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainViewModel?.OnApplicationExit();
        _log?.Info("Sigwalk 退出");
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _log?.Error("未处理的界面异常", e.Exception);
        MessageBox.Show(
            $"发生未处理的异常：\n{e.Exception.Message}\n\n详细信息已写入日志。",
            "Sigwalk",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        _log?.Error("未处理的应用程序异常", e.ExceptionObject as Exception);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _log?.Error("未观察的任务异常", e.Exception);
        e.SetObserved();
    }
}
