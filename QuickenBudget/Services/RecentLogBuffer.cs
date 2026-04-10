/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System;
using System.Collections.Generic;
using System.Threading;

using QuickenBudget.Interfaces;

namespace QuickenBudget.Services;

public class RecentLogBuffer : IRecentLogBuffer
{
    private readonly Lock _sync = new();

    private readonly Dictionary<Guid, List<string>> _messages = [];

    public void Add(Guid scopeId, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }
        using (_sync.EnterScope())
        {
            if (!_messages.TryGetValue(scopeId, out List<string>? messagesForScope))
            {
                _messages[scopeId] = [message];
            }
            else
            {
                messagesForScope.Add(message);
            }
        }
    }

    public void Clear()
    {
        using (_sync.EnterScope())
        {
            _messages.Clear();
        }
    }

    public void Clear(Guid scopeId)
    {
        using (_sync.EnterScope())
        {
            _messages.Remove(scopeId);
        }
    }

    public IEnumerable<string> GetMessages(Guid scopeId)
    {
        using (_sync.EnterScope())
        {
            return !_messages.TryGetValue(scopeId, out List<string>? messagesForScope) ? [] : [.. messagesForScope];
        }
    }
}
