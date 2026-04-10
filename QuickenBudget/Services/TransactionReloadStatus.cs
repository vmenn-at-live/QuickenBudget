/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using QuickenBudget.Interfaces;
using QuickenBudget.Models;

using Serilog.Core;
using Serilog.Events;

using System;
using System.Collections.Generic;
using System.Threading;

namespace QuickenBudget.Services;

/// <summary>
/// Tracks the most recent successful snapshot and the status of the transaction reload process, which can be "success", "reload" or "errors".
/// The status is derived from the currently loaded snapshot and whether the most recent reload attempt succeeded.
/// A reload that produces a newer snapshot and completes successfully results in "success"; a failed reload results in "errors" until a newer successful snapshot is loaded.
/// </summary>
public class TransactionReloadStatus(IRecentLogBuffer recentMessages) : ITransactionReloadStatus, ILogEventSink
{
    private readonly Lock _snapshotSync = new();
    private Guid _snapshotId = Guid.Empty;
    private ITransactionData _snapshot = new TransactionDataSnapshot();
    private Guid _failedSnapshotId = Guid.Empty;
    private bool _lastReloadSucceeded = true;

    public ITransactionData Snapshot
    {
        get
        {
            return Volatile.Read(ref _snapshot);
        }
    }

    /// <summary>
    /// A new snapshot was successfully loaded. If the snapshot is newer than the current one, replace it and return true. Otherwise, do nothing and return false.
    /// </summary>
    /// <param name="snapshot"></param>
    /// <returns></returns>
    public bool UpdateSnapshot(Guid snapshotId, ITransactionData snapshot)
    {
        using (_snapshotSync.EnterScope())
        {
            // See if current snapshot was modified after we started the new snapshot (that is its data is newer). Do nothing if so.
            if (_snapshot.CreationTime < snapshot.CreationTime)
            {
                // Replace the snapshot. Part of the operation is deleting message from the message buffer.
                if (!_lastReloadSucceeded)
                {
                    recentMessages.Clear(_failedSnapshotId);
                }
                recentMessages.Clear(_snapshotId);

                Volatile.Write(ref _snapshot, snapshot);
                _snapshotId = snapshotId;
                _lastReloadSucceeded = true;
                _failedSnapshotId = Guid.Empty;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The attempt to reload failed.
    /// </summary>
    public void LastReloadFailed(Guid snapshotId, DateTimeOffset timeOfAttempt)
    {
        using (_snapshotSync.EnterScope())
        {
            recentMessages.Clear(_failedSnapshotId);
            _failedSnapshotId = snapshotId;

            // See if snapshot was modified after we started (that is its data is newer). Do nothing if so.
            if (_snapshot.Transactions.Count == 0 || _snapshot.CreationTime < timeOfAttempt)
            {
                _lastReloadSucceeded = false;
            }
        }
    }

    /// <summary>
    /// Retrieves a collection of the latest messages from the current transaction snapshot or the last failed reload attempt.
    /// </summary>
    /// <remarks>
    /// Returns messages associated with the most recent reload operation. If the last reload failed and its data is still relevant,
    /// messages from that failed attempt are returned; otherwise, messages from the current snapshot are returned. The returned
    /// collection may be empty if no messages are available.
    /// </remarks>
    /// <returns>An enumerable of strings - messages</returns>
    public IEnumerable<string> LatestMessageList()
    {
        using (_snapshotSync.EnterScope())
        {
            if (!_lastReloadSucceeded)
            {
                return recentMessages.GetMessages(_failedSnapshotId);
            }
            else
            {
                return recentMessages.GetMessages(_snapshotId);
            }
        }
    }

    /// <summary>
    /// Disposed object returns success status. For normal objects, if the creation time is before the check time,
    /// the return is "success" or failure, depending on whether the last reload succeeded. If the check time is before
    /// the creation time,the return is "reload".
    ///</summary>
    public string GetStatusSince(DateTimeOffset since)
    {
        using (_snapshotSync.EnterScope())
        {
            if (_snapshot.CreationTime <= since)
            {
                return _lastReloadSucceeded ? "success" : "errors";
            }
            else
            {
                return "reload";
            }
        }
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent is null)
        {
            return;
        }

        if (logEvent.Properties.TryGetValue("SnapshotId", out LogEventPropertyValue? snapshotProperty) &&
            TryGetSnapshotId(snapshotProperty, out Guid snapshotId))
        {
            string message = $"[{logEvent.Level}] {logEvent.RenderMessage()}";
            if (logEvent.Exception != null)
            {
                message = message + Environment.NewLine + logEvent.Exception.Message;
            }

            recentMessages.Add(snapshotId, message);
        }
    }

    private static bool TryGetSnapshotId(LogEventPropertyValue propertyValue, out Guid snapshotId)
    {
        snapshotId = Guid.Empty;
        if (propertyValue is ScalarValue scalar && scalar.Value is not null)
        {
            switch (scalar.Value)
            {
                case Guid guidValue:
                    snapshotId = guidValue;
                    return true;
                case string stringValue when Guid.TryParse(stringValue, out var parsedGuid):
                    snapshotId = parsedGuid;
                    return true;
            }
        }
        return false;
    }

}
