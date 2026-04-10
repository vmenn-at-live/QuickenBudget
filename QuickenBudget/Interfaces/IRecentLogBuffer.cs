/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System;
using System.Collections.Generic;

namespace QuickenBudget.Interfaces;

public interface IRecentLogBuffer
{
    void Add(Guid scopeId, string message);
    void Clear();
    void Clear(Guid scopeId);
    IEnumerable<string> GetMessages(Guid scopeId);
}
