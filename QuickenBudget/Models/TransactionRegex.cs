/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System;
using System.Text.RegularExpressions;

namespace QuickenBudget.Models;

/// <summary>
/// Shared regex settings for transaction selectors and filters.
/// </summary>
internal static class TransactionRegex
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

    public static Regex? CreateOrNull(string? pattern) =>
        string.IsNullOrEmpty(pattern)
            ? null
            : new Regex(pattern, RegexOptions.Compiled, MatchTimeout);
}
