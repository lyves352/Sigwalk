using System.IO;
using Sigwalk.Core.CommandLine;
using Sigwalk.Core.Localization;
using Sigwalk.Core.Logging;
using Sigwalk.Core.Models;
using Sigwalk.Core.Services;
using Sigwalk.Core.Services.Export;
using Sigwalk.Core.Services.Scanning;

namespace Sigwalk.Cli;

/// <summary>无界面命令行扫描器：复用 Core 的发现、扫描、签名验证与导出实现。</summary>
public sealed class CliRunner
{
    private readonly ILogService _log;

    public CliRunner(ILogService log)
    {
        _log = log;
    }

    public async Task<int> RunAsync(IReadOnlyList<string> args)
    {
        try
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Load();

            // 参数解析失败时也需要使用请求的语言输出错误，因此先做一次轻量 --lang 预扫描。
            Loc.SetLanguage(FindLanguage(args) ?? settings.Language);

            var parse = CliOptions.Parse(args);
            if (parse.Options is null)
            {
                Console.Error.WriteLine(parse.ErrorMessage);
                Console.Error.WriteLine();
                Console.Error.WriteLine(CliUsage.Text);
                return (int)CliExitCode.UsageError;
            }

            var options = parse.Options;
            Loc.SetLanguage(options.Language ?? settings.Language);

            if (options.ShowHelp)
            {
                Console.WriteLine(CliUsage.Text);
                return (int)CliExitCode.Success;
            }

            if (options.ShowVersion)
            {
                var version = typeof(CliRunner).Assembly.GetName().Version?.ToString() ?? "1.0.0";
                Console.WriteLine(Loc.F("Cli.Version", version));
                return (int)CliExitCode.Success;
            }

            var scanOptions = new ScanOptions
            {
                Recursive = options.Recursive ?? settings.Recursive,
                Extensions = options.Extensions ?? settings.Extensions,
                MaxConcurrency = Math.Clamp(options.Concurrency ?? settings.MaxConcurrency, 1, 32),
                ScanMode = options.ScanMode ?? settings.ScanMode,
                RevocationMode = options.Revocation ?? settings.RevocationMode,
            };

            var discovery = await Task.Run(() => new FileDiscoveryService().Discover(options.Paths, scanOptions));
            foreach (var error in discovery.Errors)
            {
                Console.Error.WriteLine(error);
            }

            foreach (var warning in discovery.Warnings)
            {
                Console.Error.WriteLine(warning);
            }

            if (discovery.Files.Count == 0)
            {
                Console.Error.WriteLine(Loc.T("Cli.Error.NoFiles"));
                return (int)CliExitCode.NoFilesFound;
            }

            Console.WriteLine(Loc.F("Cli.Message.Scanning", discovery.Files.Count));

            using var cts = new CancellationTokenSource();
            ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cts.Cancel();
            };

            Console.CancelKeyPress += cancelHandler;
            try
            {
                var results = await ScanAsync(discovery.Files, scanOptions, options.Quiet, cts.Token);
                if (results is null)
                {
                    Console.Error.WriteLine(Loc.T("Cli.Message.Cancelled"));
                    return (int)CliExitCode.Cancelled;
                }

                var content = options.Content ?? settings.ExportContent;
                var document = ExportDocumentBuilder.Build(results, content);
                PrintSummary(document.Summary);

                if (options.Output is not null)
                {
                    var format = ResolveFormat(options.Output, options.Format, settings.ExportFormat);
                    try
                    {
                        var output = Path.GetFullPath(options.Output);
                        await Task.Run(() => new ExportService().Export(output, format, results, content));
                        Console.WriteLine(Loc.F("Cli.Message.Exported", output));
                    }
                    catch (Exception ex)
                    {
                        _log.Error("命令行导出失败", ex);
                        Console.Error.WriteLine(Loc.F("Cli.Error.ExportFailed", ex.Message));
                        return (int)CliExitCode.ExportFailed;
                    }
                }

                if (options.FailOnIssues && document.Summary.Result == ExportVerdict.IssuesFound)
                {
                    Console.Error.WriteLine(Loc.T("Cli.Error.IssuesFound"));
                    return (int)CliExitCode.IssuesFound;
                }

                return (int)CliExitCode.Success;
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }
        catch (Exception ex)
        {
            _log.Error("命令行执行失败", ex);
            Console.Error.WriteLine(Loc.F("Cli.Error.Unexpected", ex.Message));
            return (int)CliExitCode.UnexpectedError;
        }
    }

    private async Task<List<FileScanResult>?> ScanAsync(
        IReadOnlyList<string> files,
        ScanOptions options,
        bool quiet,
        CancellationToken cancellationToken)
    {
        var items = files.Select((path, index) => new ScanWorkItem(index, path)).ToList();
        var coordinator = new ScanCoordinator(new FileScanProcessor(log: _log), _log);
        var session = coordinator.Start(options, items, cancellationToken);
        var results = new List<FileScanResult>(items.Count);

        try
        {
            var completion = session.WaitForCompletionAsync(cancellationToken);
            while (!completion.IsCompleted)
            {
                Drain(session, results, items.Count, quiet);
                await Task.Delay(120, CancellationToken.None).ConfigureAwait(false);
            }

            await completion.ConfigureAwait(false);
            Drain(session, results, items.Count, quiet);
            return results;
        }
        catch (OperationCanceledException)
        {
            try
            {
                await session.StopAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // 取消路径不影响退出码。
            }

            Drain(session, results, items.Count, quiet);
            return null;
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static void Drain(
        ScanSession session,
        List<FileScanResult> results,
        int total,
        bool quiet)
    {
        foreach (var entry in session.DrainResults(500))
        {
            results.Add(entry.Result);
            if (!quiet)
            {
                var status = entry.Result.ScanStatus == ScanStatus.Failed
                    ? entry.Result.ScanStatusText
                    : entry.Result.SignatureChecked
                        ? entry.Result.SignatureStatusText
                        : Loc.T("Common.NotChecked");
                Console.WriteLine(Loc.F(
                    "Cli.Message.ResultLine",
                    results.Count,
                    total,
                    status,
                    entry.Result.FilePath));
            }
        }
    }

    private static void PrintSummary(ExportSummary summary)
    {
        Console.WriteLine();
        Console.WriteLine($"{Loc.T("Export.Summary.Result")}: {summary.ResultText}");
        Console.WriteLine($"{Loc.T("Export.Summary.Total")}: {summary.Total}");
        Console.WriteLine($"{Loc.T("Export.Summary.Completed")}: {summary.Completed}");
        Console.WriteLine($"{Loc.T("Export.Summary.Valid")}: {summary.Valid}");
        Console.WriteLine($"{Loc.T("Export.Summary.Unsigned")}: {summary.Unsigned}");
        Console.WriteLine($"{Loc.T("Export.Summary.Invalid")}: {summary.Invalid}");
        Console.WriteLine($"{Loc.T("Export.Summary.Unknown")}: {summary.Unknown}");
        Console.WriteLine($"{Loc.T("Export.Summary.Failed")}: {summary.Failed}");
    }

    private static AppLanguage? FindLanguage(IReadOnlyList<string> args)
    {
        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            string? value = null;
            if (arg.Equals("--lang", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                value = args[index + 1];
            }
            else if (arg.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase))
            {
                value = arg["--lang=".Length..];
            }

            if (value is not null)
            {
                return value.ToLowerInvariant() switch
                {
                    "zh" or "zh-cn" or "zh-hans" or "chinese" => AppLanguage.Chinese,
                    "en" or "en-us" or "english" => AppLanguage.English,
                    _ => null,
                };
            }
        }

        return null;
    }

    private static ExportFormat ResolveFormat(string output, ExportFormat? requested, ExportFormat fallback)
    {
        if (requested is not null)
        {
            return requested.Value;
        }

        var extension = Path.GetExtension(output).ToLowerInvariant();
        return extension switch
        {
            ".csv" => ExportFormat.Csv,
            ".json" => ExportFormat.Json,
            ".xlsx" => ExportFormat.Xlsx,
            _ => fallback,
        };
    }
}
