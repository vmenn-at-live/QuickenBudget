/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Threading;

using QuickenBudget.Interfaces;
using QuickenBudget.Models;

#pragma warning disable CA1873

namespace QuickenBudget.Services;

public class Watcher : BackgroundService
{
    // Get these from the DI.
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITransactionReloadStatus _snapshotStatus;
    private readonly IOptionsMonitor<TransactionReaderSettings> _readerSettings;
    private readonly TimeProvider _timeProvider;

    private readonly ILogger<Watcher> _logger;

    // Monitor for changes to the transaction file and relevant settings. When a change is detected, the current snapshot will be reloaded.
    private OptionsFileMonitor _changeMonitor;

    // Watcher sets this when change is detected.
    private readonly AsyncAutoResetEvent _changeDetectedSignal;

    // Save file path we are monitoring here (it comes from the _readerSettings).
    private string _monitoredFilePath;

    // Tells us when _changeMonitor is not set up yet.
    private bool _isMonitoringStarted;

    public Watcher(ILoggerFactory loggerFactory, IServiceProvider serviceProvider, ITransactionReloadStatus snapshotStatus, IOptionsMonitor<TransactionReaderSettings> readerSettings, TimeProvider timeProvider)
    {
        _loggerFactory = loggerFactory;
        _serviceProvider = serviceProvider;
        _logger = loggerFactory.CreateLogger<Watcher>();
        _snapshotStatus = snapshotStatus;
        _readerSettings = readerSettings;
        _timeProvider = timeProvider;
        _changeMonitor = new OptionsFileMonitor(_loggerFactory.CreateLogger<OptionsFileMonitor>(), serviceProvider);
        _changeDetectedSignal = new();
        _monitoredFilePath = string.Empty;
        _isMonitoringStarted = false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Watcher service is executing.");

        // Make sure a snapshot is created on startup.
        using (var scope = _serviceProvider.CreateScope())
        {
            ITransactionReader reader = scope.ServiceProvider.GetRequiredService<ITransactionReader>();
            IRecentLogBuffer logBuffer = scope.ServiceProvider.GetRequiredService<IRecentLogBuffer>();
            UpdateSnapshot(logBuffer, reader);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            // Make sure we're waiting for some change to happen
            SetupMonitor(stoppingToken);
            try
            {
                await _changeDetectedSignal.WaitAsync(stoppingToken);
            }
            // This may be ignored as we're going to exit the loop anyway.
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {}

            if (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Watcher detected a change, reinitializing transaction data...");
                    using var scope = _serviceProvider.CreateScope();
                    ITransactionReader reader = scope.ServiceProvider.GetRequiredService<ITransactionReader>();
                    IRecentLogBuffer logBuffer = scope.ServiceProvider.GetRequiredService<IRecentLogBuffer>();
                    UpdateSnapshot(logBuffer, reader);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while reinitializing transaction data after change was detected.");
                }
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Watcher is stopping at: {time}", DateTimeOffset.Now);
        await base.StopAsync(cancellationToken); // Call base to ensure proper shutdown

        _changeMonitor.StopMonitoring();
        _changeMonitor.Dispose();
    }

    private void SetupMonitor(CancellationToken stoppingToken)
    {
        // Potentially new file name to monitor.
        string newFilePath = _readerSettings.CurrentValue.QuickenReportFile;

        // If we are not monitoring, make sure the monitor is set up.
        if (!_isMonitoringStarted)
        {
            _changeMonitor.AddOptionsMonitor<TransactionSelectors>();
            _changeMonitor.AddOptionsMonitor<TransactionReaderSettings>();
            if (!string.IsNullOrEmpty(newFilePath))
            {
                _logger.LogInformation("Setting up file monitor for the first time with file path: '{FilePath}'", newFilePath);
                _changeMonitor.AddFilePath(newFilePath);
            }
            else
            {
                _logger.LogInformation("Setting up file monitor without an input file");
            }

            _isMonitoringStarted = true;
        }
        else
        {
            // Add file to monitor if the new file name is not empty and the file path has changed.
            var pathComparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (!string.Equals(newFilePath, _monitoredFilePath, pathComparison))
            {
                _changeMonitor.StopMonitoring();
                _changeMonitor.RemoveFilePath(_monitoredFilePath);
                if (!string.IsNullOrEmpty(newFilePath))
                {
                    _logger.LogInformation("Monitored file path changed from '{OldPath}' to '{NewPath}', updating file monitor...", _monitoredFilePath, newFilePath);
                    _changeMonitor.AddFilePath(newFilePath);
                }
                else
                {
                    _logger.LogInformation("Monitored file path was '{OldPath}'; no file is being watched, updating file monitor...", _monitoredFilePath);
                }
            }
        }

        _monitoredFilePath = newFilePath;
        _changeMonitor.OnChange(_changeDetectedSignal.Set, stoppingToken);
    }

    private void UpdateSnapshot(IRecentLogBuffer logBuffer, ITransactionReader reader)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        Guid snapshotId = Guid.NewGuid();
        var scopeProps = new Dictionary<string, object>
        {
            { "SnapshotId", snapshotId}
        };
        var logger = _loggerFactory.CreateLogger<TransactionReader>();

        using (logger.BeginScope(scopeProps))
        {
            try
            {
                logBuffer.Clear();
                TransactionDataSnapshot newSnapshot =
                    TransactionDataSnapshot.CreateSnapshot(
                                            _loggerFactory.CreateLogger<TransactionReader>(),
                                            reader,
                                            _timeProvider.GetUtcNow(),
                                            _timeProvider.GetLocalNow().Year);

                _snapshotStatus.UpdateSnapshot(snapshotId, newSnapshot);
            }
            catch (Exception ex)
            {
                Exception exToLog = ex is ParsingException parsingEx && parsingEx.InnerException != null ? parsingEx.InnerException : ex;
                logger.LogError(exToLog, "{Message}\nKeeping old data as transactions failed to refresh in {ElapsedMilliseconds} ms.", ex.Message, stopwatch.ElapsedMilliseconds);
                _snapshotStatus.LastReloadFailed(snapshotId, _timeProvider.GetUtcNow());
            }
        }
    }
}
