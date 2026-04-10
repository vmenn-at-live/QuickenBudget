/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace QuickenBudget.Models;

/// <summary>
/// Specifies a filter to apply to transactions. The filter can be used to select transactions based
/// on a regex applied to a specified field, and/or based on the amount of the transaction using a
/// specified comparison operation. If both a regex filter and an amount filter are specified, a transaction
/// must match both filters to be selected. Mostly used to filter out transactions that should not be included in any group.
/// </summary>
public class TransactionFilter
{
    /// <summary>
    /// Specifies the field to filter on (e.g. "Category", "Memo", etc.). If null or empty, only do the Amount check.
    /// </summary>
    public string? Field { get; set; }

    /// <summary>
    /// Specifies the regex filter to apply to that field value. If null or empty, only do the Amount check.
    /// </summary>
    public string? Filter { get => _filter?.ToString(); set => _filter = TransactionRegex.CreateOrNull(value); }

    /// <summary>
    /// When equal to "Equal", "Less", "Greater", the amount in the transaction is compared to the Amount in this object (as specified by the operation).
    /// Any other value (including "None" or null) means that the amount in the transaction is not checked against the Amount in this object.
    /// </summary>
    public string? AmountOperation { get; set; } = "None";

    /// <summary>
    /// Used if AmountOperation is "Equal", "Less", "Greater". The value of "None" is used for clarity only, but any value can be used when AmountOperation is not
    // "Equal", "Less", or "Greater" since it will be ignored.
    /// </summary>
    public decimal Amount { get; set; } = 0;

    /// <summary>
    /// The compiled regex object created from the Filter string. This is used for efficient matching when filtering transactions.
    /// It will be null if the Filter string is null or empty.
    /// </summary>
    private Regex? _filter;

    /// <summary>
    /// Evaluates the specified fields against the configured filter.
    /// 1. Tries to match the regex to the field value (if specified).
    /// 2. Additional check if the regex filter was matched or not specified and AmountOperation field is not missing
    ///    a) Checks if the Amount field in the transaction is present and can be parsed as a decimal.
    ///    b) If AmountOperation is "Equal", "Less", or "Greater", compares the Amount fields of this object and the value of the Amount field in the dictionary.
    /// Returns true if 1 is matched and 2 is not specified. Otherwise returns the result of 2.
    /// </summary>
    /// <param name="fields">A read-only dictionary containing field names and their corresponding values to be evaluated.</param>
    /// <returns>True if matched (filter hit), false otherwise.</returns>
    public bool IsFiltered(IReadOnlyDictionary<string, string> fields)
    {
        // First check if we match the regex filter (if specified). If not, we can return false immediately.
        bool regexMatched = _filter != null &&
            !string.IsNullOrWhiteSpace(Field) &&
            fields.TryGetValue(Field, out string? fieldValue) &&
            fieldValue != null &&
            _filter.IsMatch(fieldValue.Trim());

        // If there is no AmountOperation (not specified or is equal to 'None') then regexMatched is the result
        if (string.IsNullOrWhiteSpace(AmountOperation) || AmountOperation.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return regexMatched;
        }

        // Do amount matching. At this point we know that the AmountOperation is specified. An invalid operation returns false.
        return (regexMatched || _filter == null) &&
            fields.TryGetValue("Amount", out string? amountStr) &&
            decimal.TryParse(amountStr, NumberStyles.Currency, CultureInfo.CurrentCulture, out decimal amount) &&
            AmountOperation.Trim().ToLowerInvariant() switch
            {
                "equal" => Amount == Math.Abs(amount),
                "less" => Amount > Math.Abs(amount),
                "greater" => Amount < Math.Abs(amount),
                _ => false
            };
    }

}
