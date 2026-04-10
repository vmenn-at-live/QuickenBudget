/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System;
using System.Collections.Generic;
using System.Globalization;

namespace QuickenBudget.Models;

/// <summary>
/// Describes a financial transaction, which can be either an income or an expense. It contains the date, amount, group name, and any other fields that may be present in the transaction file.
/// The class also includes a factory method to create a Transaction object from a dictionary of field names and values, applying any necessary selectors to determine if the transaction should
/// be ignored or which group it belongs to.
/// This class cannot be modified after creation.
/// </summary>
/// <param name="date">Date of transaction</param>
/// <param name="amount">The monetary amount of transaction</param>
/// <param name="groupName">Name of the group the transaction belongs to</param>
/// <param name="isIncome">Indicates if the transaction is an income</param>
/// <param name="otherFields">Optional additional fields associated with the transaction</param>
public class Transaction(DateOnly date, decimal amount, string groupName, bool isIncome, Dictionary<string, string>? otherFields = null)
{
    // Keep this dictionary as a private field to ensure that it's not modified after the transaction is created.
    private readonly Dictionary<string, string> _otherFields = otherFields == null ? [] : new(otherFields);
    public DateOnly Date { get; init; } = date;
    public decimal Amount { get; init; } = amount;
    public string GroupName { get; init; } = groupName;
    public bool IsIncome { get; init; } = isIncome;
    public IReadOnlyDictionary<string, string> OtherFields { get => _otherFields; }

    /// <summary>
    /// Transaction factory method.
    /// First, checks if the transaction is to be ignored and returns no object if so.
    /// Second, determines if the transaction is an income or an expense and which group it belongs to, using the provided selectors.
    /// Finally, maps field values from the input dictionary to the corresponding property in the Transaction object
    /// or saves an entry in the OtherFields dictionary (currently, the only valid properties are Date and Amount).
    /// </summary>
    /// <param name="fieldsAndValues">A dictionary of field names and values</param>
    /// <param name="defaultDate">The default date to use if the transaction doesn't have one</param>
    /// <param name="selectors">An optional set of selectors to determine if the transaction should be ignored or which group it belongs to</param>
    /// <returns>A tuple containing the date to be used for the next transaction and the created transaction, or null if the transaction is invalid or ignored.</returns>
    public static (DateOnly, Transaction?) CreateTransaction(Dictionary<string, string> fieldsAndValues, DateOnly defaultDate, TransactionSelectors? selectors)
    {
        // Check if transaction is to be ignored.
        if (selectors != null && selectors.IsFilteredOut(fieldsAndValues))
        {
            // We may need to propagate the date to the following transactions that don't have one.
            if (fieldsAndValues.TryGetValue("Date", out string? dateStr) && DateOnly.TryParse(dateStr, out DateOnly date))
            {
                return (date, null);
            }

            // No date there, return the one provided by parameter.
            return (defaultDate, null);
        }


        bool isIncome = false;
        string? groupName = null;

        // Determine if the transaction is an income or an expense and which group it belongs to.
        if (selectors != null)
        {
            (isIncome, groupName) = selectors.SelectGroup(fieldsAndValues);
        }

        bool valid = false;
        bool haveAmount = false;
        DateOnly dateValue = defaultDate;
        decimal amountValue = 0;
        Dictionary<string, string> otherFields = [];
        foreach (var entry in fieldsAndValues)
        {
            if (entry.Key.Equals("Date", StringComparison.OrdinalIgnoreCase))
            {
                valid = ConvertDate(entry.Value, defaultDate, ref dateValue);
            }
            else if (entry.Key.Equals("Amount", StringComparison.OrdinalIgnoreCase))
            {
                // We want to convert the amount field first, so that if it's invalid we can skip the transaction without worrying about the date propagation.
                valid = decimal.TryParse(entry.Value, NumberStyles.Currency, CultureInfo.CurrentCulture, out amountValue);
                haveAmount = valid;
            }
            else
            {
                otherFields[entry.Key] = entry.Value;
                valid = true;
            }
            if (!valid)
            {
                break;
            }
        }

        if (valid && haveAmount)
        {
            if (groupName == null)
            {
                isIncome = amountValue > 0;
                groupName = "Other";
            }
            return (dateValue, new Transaction(dateValue, amountValue, groupName, isIncome, otherFields));
        }

        return (dateValue, null);
    }

    private static bool ConvertDate(string value, DateOnly defaultDate, ref DateOnly result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = defaultDate;
        }
        else if (DateOnly.TryParse(value, out DateOnly date))
        {
            result = date;
        }
        else
        {
            result = defaultDate;
            return false;
        }

        return true;
    }
}
