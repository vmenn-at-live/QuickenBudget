/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System;
using System.Threading;

namespace QuickenBudget.Interfaces;

public interface IOptionsFileMonitor
{
    void AddFilePath(string filePath);
    void AddOptionsMonitor<T>();
    void OnChange(Action callback, CancellationToken cancellationToken = default);
    void StopMonitoring();
    void RemoveFilePath(string filePath);
    void RemoveOptionsMonitor<T>();
}
