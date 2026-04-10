/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System;
using System.Collections.Generic;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using QuickenBudget.Interfaces;
using QuickenBudget.Models;
using QuickenBudget.Tools;

using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using QuickenBudget.Services;

const int defaultLogLimit = 4096;

// Just in case builder creation fails, set up a basic logger to log the error
Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console(theme: AnsiConsoleTheme.Code)
            .CreateBootstrapLogger();

try
{
    CommandLineOptions commandOptions = CommandLineOptions.Parse(args, "Quicken Transaction Server");
    if (!commandOptions.Continue)
    {
        // If parsing the command line options resulted in Continue being false, it means we should exit (e.g., after showing help or version info)
        return;
    }

    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory
    });

    RecentLogBuffer recentLogBuffer = new();

    // Add any additional configuration files specified in the command line options
    if (commandOptions.ConfigFiles.Length > 0) 
    {
        foreach (var configFile in commandOptions.ConfigFiles)
        {
            builder.Configuration.AddJsonFile(configFile.FullName, optional: false, reloadOnChange: true);
            Log.Information("Added configuration from file: {ConfigFile}", configFile.FullName);
        }
    }

    // If the command line argument for the report file is provided, add it to the configuration.
    // This will override any value from appsettings.json or environment variables, but that's
    // intentional since command line arguments should take precedence.
    if (commandOptions.ReportFile != null)
    {
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TransactionReader:QuickenReportFile"] = commandOptions.ReportFile.FullName
        });

        Log.Information("Quicken Report File from configuration: {ReportFile}", commandOptions.ReportFile.FullName);
    }

    // If a log directory was specified on the command line, redirect the File sink to that directory.
    if (commandOptions.LogDirectory != null)
    {
        builder.Configuration.ApplyLogDirectory(commandOptions.LogDirectory);
    }

    // See if configuration has the TransactionReader section. If not, report an error and exit
    // since we won't be able to run without it.
    var transactionReaderSection = builder.Configuration.GetSection("TransactionReader");
    if (transactionReaderSection == null || !transactionReaderSection.Exists() || string.IsNullOrWhiteSpace(transactionReaderSection.GetValue<string>("QuickenReportFile")))
    {
        Log.Fatal("Please provide the path to the CSV file as a command line argument or in appsettings.json.");
        return;
    }

    builder.Services
        // Configure strongly typed settings objects
        .Configure<TransactionSelectors>(builder.Configuration.GetSection("TransactionSelectors"))
        .Configure<TransactionReaderSettings>(builder.Configuration.GetSection("TransactionReader"))

        // Register TimeProvider so consumers (TransactionData) can receive a testable time source.
        .AddSingleton(TimeProvider.System)
        .AddSingleton<IRecentLogBuffer>(recentLogBuffer)

        // Singleton that maintains the latest immutable transaction snapshot.
        .AddSingleton<ITransactionReloadStatus, TransactionReloadStatus>()
        .AddSingleton(provider => (provider.GetRequiredService<ITransactionReloadStatus>() as ILogEventSink)!)

        // Singleton that monitors the transaction file for changes and triggers snapshot reloads.
        //.AddSingleton<IOptionsFileMonitor, OptionsFileMonitor>()

        // Scoped façade that captures a stable snapshot for each resolve/request.
        .AddScoped<ITransactionData, TransactionData>()

        // Add options file monitor
        .AddScoped<ITransactionReader, TransactionReader>()

        // Add pages.
        .AddRazorPages();

    builder.Services
        .AddHostedService<Watcher>()

        // Add Serilog as the logging provider
        .AddSerilog((services, lc) =>
            lc
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
        )
        .AddHttpLogging(
             logging =>
             {
                 logging.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;
                 logging.RequestBodyLogLimit = defaultLogLimit;
                 logging.ResponseBodyLogLimit = defaultLogLimit;
             });

    if (!builder.Configuration.TryResolvePort(commandOptions.Port, out int port))
    {
        Log.Fatal(
            "Invalid port value. The port supplied via '--port' ({CommandLinePort}) or the 'Port' configuration setting ({ConfigurationPort}) must be an integer between 1 and 65535.",
            commandOptions.Port,
            builder.Configuration["Port"]);
        return;
    }

    var app = builder.Build();

    app.UseStaticFiles();

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
    }

    app.UseRouting()
       .UseHttpLogging();

    app.MapStaticAssets();

    app.MapGet("/api/status", (long? since, ITransactionReloadStatus reloadStatus) =>
    {
        if (!since.HasValue)
        {
            return Results.BadRequest("Missing required query parameter 'since'.");
        }

        try
        {
            return Results.Text(reloadStatus.GetStatusSince(DateTimeOffset.FromUnixTimeMilliseconds(since.Value)), "text/plain");
        }
        catch (ArgumentOutOfRangeException)
        {
            return Results.BadRequest("Query parameter 'since' must be a valid Unix time in milliseconds.");
        }
    });

    app.MapGet("/api/recentMessages", (ITransactionReloadStatus reloadStatus) => Results.Json(reloadStatus.LatestMessageList()));

    app.MapRazorPages()
       .WithStaticAssets();

    Log.Information("Starting Quicken Transaction Server on port {Port}", port);
    app.Run($"http://0.0.0.0:{port}");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application failed to start");
}
finally
{
    Log.CloseAndFlush();
}
