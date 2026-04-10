/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

using Serilog;

namespace QuickenBudget.Tools;

public static class ConfigurationHelpers
{
    private const string DefaultLogFileName    = "log-.txt";
    private const string DefaultRollingInterval     = "Day";
    private const string DefaultRollOnFileSizeLimit = "True";
    private const string DefaultFileSizeLimitBytes  = "1000000";
    public const int DefaultPort = 8080;

    /// <summary>
    /// Updates (or adds) the Serilog File sink path to use <paramref name="logDirectory"/>.
    /// If the configuration already has a File sink, only the directory portion of its path is
    /// replaced, preserving the original file name. If no File sink is configured, one is added
    /// with the default file name "log-.txt" and standard rolling defaults. When adding a new
    /// sink, "Serilog.Sinks.File" is also inserted into the Serilog:Using list if not already present.
    /// </summary>
    public static void ApplyLogDirectory(this IConfigurationManager configuration, string logDirectory)
    {
        var writeToSection = configuration.GetSection("Serilog:WriteTo");
        int? fileIndex = null;

        foreach (var child in writeToSection.GetChildren()
                     .Where(c => string.Equals(c["Name"], "File", StringComparison.OrdinalIgnoreCase)))
        {
            if (int.TryParse(child.Key, out int idx))
                fileIndex = idx;
            break;
        }

        string existingFileName = fileIndex.HasValue
            ? Path.GetFileName(configuration[$"Serilog:WriteTo:{fileIndex}:Args:path"] ?? string.Empty)
            : string.Empty;

        string fileName = string.IsNullOrEmpty(existingFileName) ? DefaultLogFileName : existingFileName;
        string newPath = Path.Combine(logDirectory, fileName);
        var updates = new Dictionary<string, string?>();

        if (fileIndex.HasValue)
        {
            updates[$"Serilog:WriteTo:{fileIndex}:Args:path"] = newPath;
        }
        else
        {
            int nextIndex = writeToSection.GetChildren().Count();
            updates[$"Serilog:WriteTo:{nextIndex}:Name"]                    = "File";
            updates[$"Serilog:WriteTo:{nextIndex}:Args:path"]               = newPath;
            updates[$"Serilog:WriteTo:{nextIndex}:Args:rollOnFileSizeLimit"] = DefaultRollOnFileSizeLimit;
            updates[$"Serilog:WriteTo:{nextIndex}:Args:rollingInterval"]    = DefaultRollingInterval;
            updates[$"Serilog:WriteTo:{nextIndex}:Args:fileSizeLimitBytes"] = DefaultFileSizeLimitBytes;

            // Ensure "Serilog.Sinks.File" is listed in the Using array so Serilog can locate the sink.
            var usingSection = configuration.GetSection("Serilog:Using");
            bool hasFileSink = usingSection.GetChildren()
                .Any(c => string.Equals(c.Value, "Serilog.Sinks.File", StringComparison.OrdinalIgnoreCase));

            if (!hasFileSink)
            {
                int nextUsingIndex = usingSection.GetChildren().Count();
                updates[$"Serilog:Using:{nextUsingIndex}"] = "Serilog.Sinks.File";
            }
        }

        configuration.AddInMemoryCollection(updates);
    }

    /// <summary>
    /// Attempts to resolve the port number to use from the specified command-line argument or configuration settings.
    /// </summary>
    /// <remarks>The method prioritizes the command-line port if specified. If not, it attempts to parse the
    /// port from the configuration using the key "Port". If neither is provided, the default port is used. The resolved
    /// port must be in the range 1 to 65535.</remarks>
    /// <param name="config">The configuration source used to retrieve the port value if not provided via the command line.</param>
    /// <param name="commandLinePort">An optional port number specified via the command line. If provided, this value is used.</param>
    /// <param name="port">When this method returns, contains the resolved port number if successful; otherwise, contains the default port
    /// value.</param>
    /// <returns>true if a valid port number was resolved from the command-line argument or configuration; otherwise, false (in which case port is not nessasarily set).</returns>
    public static bool TryResolvePort(this IConfiguration config, int? commandLinePort, out int port)
    {
        bool isValid = true;

        if (commandLinePort.HasValue)
        {
            port = commandLinePort.Value;
        }
        else
        {
            string? configuredPortValue = config["Port"];
            if (string.IsNullOrWhiteSpace(configuredPortValue))
            {
                port = DefaultPort;
            }
            else
            {
                isValid = int.TryParse(configuredPortValue, out port) ;
            }
        }

        return isValid && port >= 1 && port <= 65535;
    }
}
