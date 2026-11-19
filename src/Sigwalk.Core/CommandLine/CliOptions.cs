using Sigwalk.Core.Localization;
using Sigwalk.Core.Models;

namespace Sigwalk.Core.CommandLine;

public enum CliExitCode
{
    Success = 0,
    UnexpectedError = 1,
    UsageError = 2,
    NoFilesFound = 3,
    ExportFailed = 4,
    IssuesFound = 5,
    Cancelled = 130,
}

public enum CliParseErrorCode
{
    None,
    UnknownOption,
    MissingValue,
    InvalidValue,
    NoPaths,
    OutputRequired,
}

public sealed record CliParseResult(CliOptions? Options, CliParseErrorCode ErrorCode, string? ErrorMessage)
{
    public bool IsSuccess => ErrorCode == CliParseErrorCode.None && Options is not null;
}

/// <summary>命令行参数。未指定的值由用户设置补全。</summary>
public sealed class CliOptions
{
    public List<string> Paths { get; } = new();

    /// <summary>null 表示未显式指定，由用户设置决定。</summary>
    public bool? Recursive { get; set; }
    public string? Extensions { get; set; }
    public int? Concurrency { get; set; }
    public ScanMode? ScanMode { get; set; }
    public RevocationMode? Revocation { get; set; }
    public ExportFormat? Format { get; set; }
    public ExportContentScope? Content { get; set; }
    public string? Output { get; set; }
    public AppLanguage? Language { get; set; }
    public bool Quiet { get; set; }
    public bool FailOnIssues { get; set; }
    public bool ShowHelp { get; set; }
    public bool ShowVersion { get; set; }

    public static CliParseResult Parse(IReadOnlyList<string> args)
    {
        var options = new CliOptions();
        var endOfOptions = false;

        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            if (endOfOptions || string.IsNullOrEmpty(arg) || arg[0] != '-' || arg == "-")
            {
                options.Paths.Add(arg);
                continue;
            }

            if (arg == "--")
            {
                endOfOptions = true;
                continue;
            }

            var (name, inlineValue) = SplitOption(arg);
            switch (name)
            {
                case "-h":
                case "--help":
                    options.ShowHelp = true;
                    break;
                case "-V":
                case "--version":
                    options.ShowVersion = true;
                    break;
                case "-r":
                case "--recursive":
                    options.Recursive = true;
                    break;
                case "--no-recursive":
                    options.Recursive = false;
                    break;
                case "-q":
                case "--quiet":
                    options.Quiet = true;
                    break;
                case "--fail-on-issues":
                    options.FailOnIssues = true;
                    break;
                case "-e":
                case "--ext":
                {
                    var value = ReadValue(args, ref index, name, inlineValue, out var error);
                    if (error is not null)
                    {
                        return error;
                    }

                    options.Extensions = value;
                    break;
                }
                case "-j":
                case "--concurrency":
                {
                    var value = ReadValue(args, ref index, name, inlineValue, out var error);
                    if (error is not null)
                    {
                        return error;
                    }

                    if (!int.TryParse(value, out var concurrency) || concurrency is < 1 or > 32)
                    {
                        return Invalid(name, value);
                    }

                    options.Concurrency = concurrency;
                    break;
                }
                case "-m":
                case "--mode":
                {
                    var value = ReadValue(args, ref index, name, inlineValue, out var error);
                    if (error is not null)
                    {
                        return error;
                    }

                    options.ScanMode = value.ToLowerInvariant() switch
                    {
                        "full" => Models.ScanMode.Full,
                        "signature" or "signatureonly" or "signature-only" => Models.ScanMode.SignatureOnly,
                        "hash" or "hashonly" or "hash-only" => Models.ScanMode.HashOnly,
                        _ => null,
                    };

                    if (options.ScanMode is null)
                    {
                        return Invalid(name, value);
                    }

                    break;
                }
                case "--revocation":
                {
                    var value = ReadValue(args, ref index, name, inlineValue, out var error);
                    if (error is not null)
                    {
                        return error;
                    }

                    options.Revocation = value.ToLowerInvariant() switch
                    {
                        "none" or "offline" => Models.RevocationMode.None,
                        "cache" or "cacheonly" or "cache-only" => Models.RevocationMode.CacheOnly,
                        "online" => Models.RevocationMode.Online,
                        _ => null,
                    };

                    if (options.Revocation is null)
                    {
                        return Invalid(name, value);
                    }

                    break;
                }
                case "-f":
                case "--format":
                {
                    var value = ReadValue(args, ref index, name, inlineValue, out var error);
                    if (error is not null)
                    {
                        return error;
                    }

                    options.Format = value.ToLowerInvariant() switch
                    {
                        "csv" => Models.ExportFormat.Csv,
                        "json" => Models.ExportFormat.Json,
                        "xlsx" or "excel" => Models.ExportFormat.Xlsx,
                        _ => null,
                    };

                    if (options.Format is null)
                    {
                        return Invalid(name, value);
                    }

                    break;
                }
                case "-c":
                case "--content":
                {
                    var value = ReadValue(args, ref index, name, inlineValue, out var error);
                    if (error is not null)
                    {
                        return error;
                    }

                    options.Content = value.ToLowerInvariant() switch
                    {
                        "all" => Models.ExportContentScope.All,
                        "compact" or "problems" => Models.ExportContentScope.Compact,
                        _ => null,
                    };

                    if (options.Content is null)
                    {
                        return Invalid(name, value);
                    }

                    break;
                }
                case "-o":
                case "--output":
                {
                    var value = ReadValue(args, ref index, name, inlineValue, out var error);
                    if (error is not null)
                    {
                        return error;
                    }

                    options.Output = value;
                    break;
                }
                case "--lang":
                {
                    var value = ReadValue(args, ref index, name, inlineValue, out var error);
                    if (error is not null)
                    {
                        return error;
                    }

                    options.Language = value.ToLowerInvariant() switch
                    {
                        "zh" or "zh-cn" or "zh-hans" or "chinese" => AppLanguage.Chinese,
                        "en" or "en-us" or "english" => AppLanguage.English,
                        _ => null,
                    };

                    if (options.Language is null)
                    {
                        return Invalid(name, value);
                    }

                    break;
                }
                default:
                    return new CliParseResult(
                        null,
                        CliParseErrorCode.UnknownOption,
                        Loc.F("Cli.Error.UnknownOption", arg));
            }
        }

        if (!options.ShowHelp && !options.ShowVersion)
        {
            if (options.Paths.Count == 0)
            {
                return new CliParseResult(null, CliParseErrorCode.NoPaths, Loc.T("Cli.Error.NoPaths"));
            }

            if ((options.Format is not null || options.Content is not null) && options.Output is null)
            {
                return new CliParseResult(
                    null,
                    CliParseErrorCode.OutputRequired,
                    Loc.T("Cli.Error.OutputRequired"));
            }
        }

        return new CliParseResult(options, CliParseErrorCode.None, null);
    }

    private static (string Name, string? Value) SplitOption(string arg)
    {
        var separator = arg.IndexOf('=');
        return separator < 0
            ? (arg, null)
            : (arg[..separator], arg[(separator + 1)..]);
    }

    private static string ReadValue(
        IReadOnlyList<string> args,
        ref int index,
        string option,
        string? inlineValue,
        out CliParseResult? error)
    {
        error = null;
        if (!string.IsNullOrEmpty(inlineValue))
        {
            return inlineValue;
        }

        if (index + 1 >= args.Count || args[index + 1].StartsWith("-", StringComparison.Ordinal))
        {
            error = new CliParseResult(
                null,
                CliParseErrorCode.MissingValue,
                Loc.F("Cli.Error.MissingValue", option));
            return string.Empty;
        }

        index++;
        return args[index];
    }

    private static CliParseResult Invalid(string option, string value) =>
        new(null, CliParseErrorCode.InvalidValue, Loc.F("Cli.Error.InvalidValue", option, value));
}

/// <summary>命令行用法文本（随语言变化）。</summary>
public static class CliUsage
{
    public static string Text => Loc.T("Cli.Usage");
}
