using System.Windows;
using System.Windows.Threading;
using Sigwalk.Cli;
using Sigwalk.Core.Localization;
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
    private ThemeService? _themeService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _log = new FileLogService();
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var settingsService = new SettingsService();
        var settings = settingsService.Load();
        _log.Info($"加载配置：主题={settings.Theme}，语言={settings.Language}，路径={settingsService.SettingsPath}");

        if (e.Args.Length > 0)
        {
            RunCommandLine(e.Args);
            return;
        }

        Loc.SetLanguage(settings.Language);

        _themeService = new ThemeService();
        _themeService.EffectiveThemeChanged += (_, _) => WindowThemeHelper.ApplyAll();
        _themeService.Apply(settings.Theme);

        var dialogService = new DialogService(_log);
        var clipboardService = new ClipboardService();
        var fileOpenService = new FileOpenService(dialogService, _log);
        _mainViewModel = new MainViewModel(
            dialogService,
            fileOpenService,
            clipboardService,
            settingsService,
            _log,
            _themeService,
            settings);

        var window = new MainWindow { DataContext = _mainViewModel };
        MainWindow = window;
        window.Show();
        _log.Info("Sigwalk 启动");
    }

    private void RunCommandLine(string[] args)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        ConsoleHost.Ensure();

        // 整个 CLI 在无同步上下文的线程池线程上运行，避免阻塞 WPF Dispatcher 造成死锁。
        var exitCode = Task.Run(() => new CliRunner(_log!).RunAsync(args))
            .GetAwaiter()
            .GetResult();

        _log?.Info($"命令行模式退出，退出码 {exitCode}");
        Shutdown(exitCode);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainViewModel?.OnApplicationExit();
        _themeService?.Dispose();
        _log?.Info("Sigwalk 退出");
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _log?.Error("未处理的界面异常", e.Exception);
        MessageBox.Show(
            Loc.F("Dialog.Message.Unhandled", e.Exception.Message),
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
