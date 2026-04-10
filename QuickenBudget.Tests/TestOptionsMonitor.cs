using System;
using System.Collections.Generic;

using Microsoft.Extensions.Options;

namespace QuickenBudget.Tests;

public class TestOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T>
{
    private readonly List<Action<T, string?>> _listeners = [];
    private T _currentValue = currentValue;

    public T CurrentValue => _currentValue;

    public T Get(string? name) => _currentValue;

    public IDisposable OnChange(Action<T, string?> listener)
    {
        _listeners.Add(listener);
        return new Subscription(_listeners, listener);
    }

    public void Update(T value, string? name = null)
    {
        _currentValue = value;
        foreach (var listener in _listeners.ToArray())
        {
            listener(value, name);
        }
    }

    private sealed class Subscription(List<Action<T, string?>> listeners, Action<T, string?> listener) : IDisposable
    {
        public void Dispose()
        {
            listeners.Remove(listener);
        }
    }
}
