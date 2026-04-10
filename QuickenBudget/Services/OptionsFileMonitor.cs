/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

using QuickenBudget.Interfaces;

#pragma warning disable CA1873

namespace QuickenBudget.Services;

public class OptionsFileMonitor(ILogger<OptionsFileMonitor> logger, IServiceProvider serviceProvider) : IOptionsFileMonitor, IDisposable
{
    // Keep track of the IOptionsChangeTokenSource objects for the monitored options, so that we can get change tokens from them.
    private readonly Dictionary<Type, Object> _monitoredOptions = [];

    // Keep track of the file paths to monitor and their corresponding file providers.
    private readonly Dictionary<string, string> _monitoredFilePaths = [];

    // We need the providers to get the change tokens, and we want to reuse providers for files in the same directory.
    private Dictionary<string, PhysicalFileProvider> _fileProviders = [];

    // Change token that combines all the monitored options and file paths tokens.
    private CompositeChangeToken? _changeToken;

    // The disposable returned when registering the callback on the change token. Keep track of it so that we can dispose of it when we want to stop monitoring.
    private IDisposable? _registeredCallback;

    // Debounce interval for scheduling the action on change. This is to prevent multiple rapid changes from triggering the action multiple times in quick succession.
    // The action will be scheduled to run after this interval has passed since the last change.
    private static readonly TimeSpan ReloadDebounce = TimeSpan.FromMilliseconds(300);

    // And the timer that triggers the action after the debounce interval.
    private Timer? _scheduledDebounceTimer;

    private readonly Lock _lock = new();

    private CancellationTokenSource _stopMonitoringTokenSource = new();
    private bool _disposed;
    private bool IsDisposed => Volatile.Read(ref _disposed);
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <summary>
    /// Add another options monitor. Note that we will log a warning if the monitor is not registered, but we won't throw, since it's possible that the monitor is registered after this method is called.
    /// </summary>
    /// <typeparam name="T">The type of the options to monitor.</typeparam>
    public void AddOptionsMonitor<T>()
    {
        ThrowIfDisposed();
        Type t = typeof(IOptionsChangeTokenSource<T>);
        if (serviceProvider.GetService(t) is not IOptionsChangeTokenSource<T> o)
        {
            logger.LogWarning("The monitor for {Type} is not registered. The type will not be monitored.", typeof(T).Name);
        }
        else
        {
            using (_lock.EnterScope())
            {
                _monitoredOptions[t] = o;
            }
        }
    }

    /// <summary>
    /// Add a file path to be monitored. Note that we will log a warning if the file path cannot be processed, but we won't throw, since it's possible that the file path becomes valid later.
    /// </summary>
    /// <param name="filePath"></param>
    public void AddFilePath(string filePath)
    {
        ThrowIfDisposed();
        string? normalizedPath = null;
        // Try to normalize path. Note that GetFullPath may throw, in which case we don't want that path.
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            try
            {
                normalizedPath = Path.GetFullPath(filePath);
                string? directory = Path.GetDirectoryName(normalizedPath);

                // If the directory for the file doesn't exist, don't watch. We would
                // still watch for non-existent files in case they show up later in case the directory exists.
                if (directory == null || !Directory.Exists(directory))
                {
                    normalizedPath = null;
                }
            }
            catch
            {
                normalizedPath = null;
            }
        }

        if (normalizedPath != null)
        {
            using (_lock.EnterScope())
            {
                _monitoredFilePaths[filePath] = normalizedPath;
            }
        }
        else
        {
            logger.LogWarning("File path {FilePath} cannot be processed. The path will not be monitored.", filePath);

        }
    }


    /// <summary>
    /// Remove the monitor from the list. No need to throw if the object is disposed, we can still remove it, and if it's not in the list, we can just ignore it.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public void RemoveOptionsMonitor<T>()
    {
        using (_lock.EnterScope())
        {
            _monitoredOptions.Remove(typeof(IOptionsChangeTokenSource<T>));
        }
    }

    /// <summary>
    /// Remove file from the list. No need to throw if the object is disposed, we can still remove it, and if it's not in the list, we can just ignore it.
    /// </summary>
    /// <param name="filePath"></param>
    public void RemoveFilePath(string filePath)
    {
        using (_lock.EnterScope())
        {
            _monitoredFilePaths.Remove(filePath);
        }
    }


    /// <summary>
    /// Schedules the specified action to execute after a debounce interval, resetting the timer if called again before
    /// the interval elapses.
    /// </summary>
    /// <remarks>Use this method to prevent an action from being executed too frequently in response to rapid
    /// or repeated triggers. The action is executed only once after the specified debounce interval, even if
    /// the method is called multiple times during that period.</remarks>
    /// <param name="action">The action to execute after the debounce period has passed without further scheduling requests. Cannot be null.</param>
    private void ScheduleActionWithDebounce(Action action)
    {

        if (action is not null)
        {
            using(_lock.EnterScope())
            {
                if (!IsDisposed)
                {
                    // If an execution is already pending (timer's not null), just change the timer to delay the action. This part handles debouncing and
                    // avoids calling the action multiple times in quick succession. We catch ObjectDisposedException just in case, but it shouldn't happen
                    // since we always set the timer to null after disposing under lock.
                    if (_scheduledDebounceTimer != null)
                    {
                        try
                        {
                            _scheduledDebounceTimer.Change(ReloadDebounce, Timeout.InfiniteTimeSpan);
                            return;
                        }
                        catch (ObjectDisposedException)
                        {
                            logger.LogWarning("Debounce timer was unexpectedly disposed. This may cause the action to not be executed as expected. A new timer will be created on the next change.");
                            _scheduledDebounceTimer = null;
                        }
                    }

                    // If timer is null, create (or re-create) it.
                    _scheduledDebounceTimer ??= new Timer(_ =>
                        {
                            action();
                            using (_lock.EnterScope())
                            {
                                _scheduledDebounceTimer!.Dispose();
                                _scheduledDebounceTimer = null;
                            }
                        }, null, ReloadDebounce, Timeout.InfiniteTimeSpan
                    );
                }
            }
        }
    }

    /// <summary>
    /// Run action on change. We collect tokens from all options monitors and file watchers, and register a callback on the composite token.
    /// When the callback is triggered, we schedule the specified action and re-register the callback since change tokens are one-time use.
    /// </summary>
    /// <param name="callback"></param>
    public void OnChange(Action callback, CancellationToken cancellationToken = default)
    {
        // We don't want null actions
        ArgumentNullException.ThrowIfNull(callback);

        // We're starting to change the state, so lock us up.
        using (_lock.EnterScope())
        {

            ThrowIfDisposed();

            List<IChangeToken> changeTokens = [];

            changeTokens.AddRange(CollectFileWatchTokens());
            changeTokens.AddRange(CollectMonitoredOptionsTokens());


            // We may have received a cancellation token to stop monitoring. If so, add it to the list of tokens we are monitoring,
            // so that when it's cancelled, our callback will be triggered and we can stop monitoring.
            if (cancellationToken != default)
            {
                changeTokens.Add(new CancellationChangeToken(cancellationToken));
            }


            // We have our own cancellation token source to stop monitoring when this object is disposed. It is also used when we decide to stop monitoring manually (to change file paths, for example).
            changeTokens.Add(new CancellationChangeToken(_stopMonitoringTokenSource.Token));
            _changeToken = new CompositeChangeToken(changeTokens);

            // Kill old callback if exists, otherwise we may end up with multiple callbacks running concurrently, which is not ideal but better than missing changes.
            _registeredCallback?.Dispose();
            _registeredCallback = _changeToken.RegisterChangeCallback(_ =>
            {
                if (!IsDisposed)
                {
                    // See if we stopped because of cancellation, in which case we don't want to run the action or restart the monitoring.
                    if (!_stopMonitoringTokenSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                    {
                        logger.LogDebug("Change detected.");
                        ScheduleActionWithDebounce(callback);

                        // This callback is modifying the state, so 
                        using (_lock.EnterScope())
                        {
                            // Re-register callback since change tokens are one-time use.
                            _registeredCallback?.Dispose();
                            _registeredCallback = null;
                            _changeToken = null;
                        }
                        OnChange(callback, cancellationToken);
                    }
                    else
                    {
                        logger.LogDebug("Stop monitoring due to cancellation.");
                    }
                }
            }, null);
        }
    }

    public void StopMonitoring()
    {
        using( _lock.EnterScope()) {
            if (_registeredCallback != null)
            {
                _scheduledDebounceTimer?.Dispose();
                _scheduledDebounceTimer = null;

                _registeredCallback.Dispose();
                _registeredCallback = null;

                var oldTokenSource = _stopMonitoringTokenSource;
                oldTokenSource.Cancel();

                // Create a new cancellation token source for future monitoring.
                _stopMonitoringTokenSource = new CancellationTokenSource();

                // Cancel the old cancellation token to trigger the callback and stop monitoring.
                oldTokenSource.Dispose();
            }

        }
    }

    /// <summary>
    /// Retrieves change tokens that can be used to monitor the set of configured IOption objects for changes.
    /// We have to use reflection since there is no common interface for the monitors and we want to support any type of options.
    /// </summary>
    /// <returns>An enumerable collection of <see cref="IChangeToken"/> instances, each representing a change token for a
    /// monitored IOption object. The collection may be empty if no valid IOption objects are configured.</returns>
    private IEnumerable<IChangeToken> CollectMonitoredOptionsTokens()
    {
        foreach (var o in _monitoredOptions)
        {
            MethodInfo? method = o.Key.GetMethod("GetChangeToken", Type.EmptyTypes);
            if (method != null)
            {
                yield return method.Invoke(o.Value, null) as IChangeToken ?? throw new InvalidOperationException("GetChangeToken did not return an IChangeToken");
            }
            else
            {
                logger.LogWarning("GetChangeToken method not found for options monitor of type {Type}. This monitor will not be included.", o.Key.Name);
            }
        }
    }

    /// <summary>
    /// Retrieves change tokens that can be used to monitor the set of configured file paths for changes.
    /// </summary>
    /// <remarks>Each change token in the returned collection can be used to detect changes to its
    /// corresponding file. If a file path is invalid or cannot be monitored, it is skipped and a warning is logged. The
    /// method ensures that only valid and accessible file paths are included in the monitoring process.</remarks>
    /// <returns>An enumerable collection of <see cref="IChangeToken"/> instances, each representing a change token for a
    /// monitored file path. The collection may be empty if no valid file paths are configured.</returns>
    private List<IChangeToken> CollectFileWatchTokens()
    {
        Dictionary<string, PhysicalFileProvider> newFileProviders = [];
        List<IChangeToken> changeTokens = [];
        // Get file monitoring tokens.
        foreach (var filePath in _monitoredFilePaths.Values)
        {
            try
            {

                // Get directory and file name. We need both to create the file provider and the watch.
                string? directory = Path.GetDirectoryName(filePath);
                string? fileName = Path.GetFileName(filePath);

                // If we can't get either, skip this file and log a warning.
                if (string.IsNullOrEmpty(directory))
                {
                    logger.LogWarning("Could not get directory for file path {FilePath}. This file will not be monitored.", filePath);
                    continue;
                }
                if (string.IsNullOrEmpty(fileName))
                {
                    logger.LogWarning("Could not get file name for file path {FilePath}. This file will not be monitored.", filePath);
                    continue;
                }

                // See if we have a provider created already (we may have multiple files in the same directory,
                // and we don't want to create multiple providers for the same directory).
                if (!newFileProviders.TryGetValue(directory, out PhysicalFileProvider? provider))
                {
                    // Not the same directory, see if we have a provider for that directory already from the previous run. If so, we can reuse it, otherwise create a new one.
                    if (_fileProviders.TryGetValue(directory, out provider))
                    {
                        // We can reuse the existing provider, so just we'll move it to the new dictionary.
                        _fileProviders.Remove(directory);
                    }
                    else
                    {
                        // We need to create a new provider for this directory.
                        provider = new PhysicalFileProvider(directory);
                    }

                    newFileProviders[directory] = provider;
                }

                // Add the watch to the provider.
                changeTokens.Add(provider.Watch(Path.GetFileName(filePath)));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not create a file watcher for {FilePath}: {Message}. This file will not be monitored.", filePath, ex.Message);
                logger.LogDebug(ex, "Exception details for failure to create file watcher for {FilePath}", filePath);
            }
        }

        // Kill unused providers. If there are no new providers, we'll remove all old providers - there are no files to watch in those
        // directories anymore. Since we moved all relevant ones over to the new dictionary, only unused ones are left.
        foreach (var p in _fileProviders.Values)
        {
            p.Dispose();
        }

        // Replace with new providers (new list may be empty).
        _fileProviders = newFileProviders;

        return changeTokens;
    }

    protected virtual void Dispose(bool disposing)
    {
        using (_lock.EnterScope())
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose of the callback - it will stop listening for changes,.
                    _registeredCallback?.Dispose();
                    _registeredCallback = null;

                    // Stop the timer if it's still running.
                    _scheduledDebounceTimer?.Dispose();
                    _scheduledDebounceTimer = null;

                    _stopMonitoringTokenSource.Dispose();
                    _stopMonitoringTokenSource = null!;

                    // Finally, dispose of the providers, clear the list.
                    foreach (var provider in _fileProviders.Values)
                    {
                        provider.Dispose();
                    }
                    _fileProviders.Clear();
                }

                _disposed = true;
            }
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
