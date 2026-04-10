/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;

namespace QuickenBudget.Tools;

/// <summary>
/// Parses command line arguments:
/// - Optional positional file path argument: ReportFile
/// - Optional repeated option: --config / -c <file>   => collected into a FileInfo[]
/// - Optional option: --port / -p <port>
/// </summary>
public sealed class CommandLineOptions
{
    /// <summary>Optional positional file path argument.</summary>
    public FileInfo? ReportFile { get; private set; }

    /// <summary>All values passed via --config/-c (may be empty).</summary>
    public FileInfo[] ConfigFiles { get; private set; } = [];

    /// <summary>Optional port passed via --port/-p.</summary>
    public int? Port { get; private set; }

    /// <summary>Optional log directory passed via --logDirectory/-ld.</summary>
    public string? LogDirectory { get; private set; }

    public bool Continue { get; private set; } = false;

    // Option/Argument definitions - description: "Optional transaction file path."
    private static readonly Argument<FileInfo?> ReportFileArgument = CreateReportFileArgument();
    private static readonly Option<FileInfo[]> ConfigOption = CreateConfigOption();
    private static readonly Option<int?> PortOption = CreatePortOption();
    private static readonly Option<string> LogDirectoryOption = CreateLogDirectoryOption();

    private readonly string? CommandDescription;
    private readonly RootCommand Root;

    private CommandLineOptions(string? commandDescription = null)
    {
        CommandDescription = commandDescription;
        Root = CreateRootCommand();
    }

    private static Argument<FileInfo?> CreateReportFileArgument()
    {
        var arg = new Argument<FileInfo?>("ReportFile")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Description = "Optional transaction file path."
        };

        arg.Validators.Add(r =>
        {
            var file = r.GetValueOrDefault<FileInfo?>();
            if (file != null && !file.Exists)
            {
                r.AddError($"Report file does not exist: '{file.FullName}'.");
            }
        });

        return arg;
    }

    private static Option<FileInfo[]> CreateConfigOption()
    {
        // NOTE: per 2.0.3: name is required. Aliases are additional params.
        var opt = new Option<FileInfo[]>("--config", "-c")
        {
            Arity = ArgumentArity.OneOrMore,
            AllowMultipleArgumentsPerToken = true,
            Description = "Configuration file(s). May be specified multiple times."
        };

        // Aggregate missing file validation
        opt.Validators.Add(r =>
        {
            FileInfo[] files = r.GetValueOrDefault<FileInfo[]>() ?? [];

            var missing = files
                .Where(f => f is not null && !f.Exists)
                .Select(f => f.FullName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (missing.Count == 0)
                return;

            r.AddError($"The following --config file(s) do not exist:{Environment.NewLine}{string.Join(Environment.NewLine, missing.Select(p => $"  - {p}"))}");
        });

        return opt;
    }

    private static Option<string> CreateLogDirectoryOption()
    {
        var opt = new Option<string>("--logDirectory", "-ld")
        {
            Arity = ArgumentArity.ZeroOrOne,
            AllowMultipleArgumentsPerToken = false,
            Description = "Directory for the log files."
        };

        opt.Validators.Add(r =>
        {
            string? di = r.GetValueOrDefault<string>();
            if (!IsValidPath(di))
            {
                r.AddError("Invalid log directory.");
            }
        });

        return opt;
    }

    private static Option<int?> CreatePortOption()
    {
        var opt = new Option<int?>("--port", "-p")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Description = "Port to listen on."
        };

        opt.Validators.Add(ctx =>
        {
            if (ctx.GetValueOrDefault<int?>() is not null and (< 1 or > 65535))
            {
                ctx.AddError("Port must be between 1 and 65535.");
            }
        });

        return opt;
    }

    private RootCommand CreateRootCommand()
    {
        RootCommand root = new(CommandDescription ?? "QuickenBudget command line")
        {
                ReportFileArgument,
                ConfigOption,
                PortOption,
                LogDirectoryOption
        };

        root.SetAction(parseResult =>
        {
            ReportFile = parseResult.GetValue(ReportFileArgument);
            Port = parseResult.GetValue(PortOption);
            LogDirectory = parseResult.GetValue(LogDirectoryOption);

            // If option is not present, System.CommandLine returns null for array options.
            FileInfo[] files = parseResult.GetValue(ConfigOption) ?? [];

            // Keep only distinct files (case-insensitive) in the order they were provided.
            ConfigFiles = [.. files
                .GroupBy(f => f.FullName,
                    RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())];

            Continue = true;

            return 0;
        });

        return root;
    }

    /// <summary>
    /// Parse args into a <see cref="CommandLineOptions"/> instance.
    /// Throws <see cref="ArgumentException"/> if parsing fails.
    /// </summary>
    public static CommandLineOptions Parse(string[] args, string? commandDescription = null)
    {
        var opt = new CommandLineOptions(commandDescription);
        ParseResult result = opt.Root.Parse(args);

        if (result.Errors.Count > 0)
        {
            throw new ArgumentException(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        }

        result.Invoke();

        return opt;
    }

    public static bool IsValidPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        try
        {
            // The core of the validation. Path.GetFullPath will throw an exception
            // if the path string contains invalid characters or is in an invalid format.
            Path.GetFullPath(path);
            return true;
        }
        catch (Exception ex) when (
            ex is ArgumentException ||
            ex is SecurityException ||
            ex is NotSupportedException ||
            ex is PathTooLongException)
        {
            // These exceptions indicate that the path is invalid for the current OS.
            return false;
        }
    }
}