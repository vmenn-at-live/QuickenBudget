/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System;
using System.Collections.Generic;

using QuickenBudget.Models;

namespace QuickenBudget.Interfaces;

public interface ITransactionReloadStatus
{
    ITransactionData Snapshot { get; }
    string GetStatusSince(DateTimeOffset since);
    void LastReloadFailed(Guid snapshotId, DateTimeOffset timeOfAttempt);
    bool UpdateSnapshot(Guid snapshotId, ITransactionData snapshot);
    IEnumerable<string> LatestMessageList();

}
