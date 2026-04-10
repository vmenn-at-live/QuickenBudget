/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System.Collections.Generic;
using System.Linq;

namespace QuickenBudget.Models;

/// <summary>
/// Model used to import configuration data for grouping or filtering transactions.
/// </summary>
public class TransactionSelectors
{
    /// <summary>
    /// Group selectors for expenses transactions. Arrays are evaluated in reverse declaration order, while rules inside
    /// each array are evaluated top-to-bottom.
    /// </summary>
    public OrderedDictionary<string, List<GroupSelector>> ExpensesGroupSelectors { get; set; } = [];
    /// <summary>
    /// Group selectors for income transactions. Arrays are evaluated in reverse declaration order, while rules inside
    /// each array are evaluated top-to-bottom.
    /// </summary>
    public OrderedDictionary<string, List<GroupSelector>> IncomeGroupSelectors { get; set; } = [];
    /// <summary>
    /// Gets or sets the collection of transaction filters, organized by their associated keys.
    /// </summary>
    /// <remarks>Each key in the collection represents a specific category of transaction filters, allowing
    /// for flexible filtering of transactions based on defined criteria. The value is a list of filters that can be
    /// applied to transactions to refine the results based on user-defined conditions.</remarks>
    public OrderedDictionary<string, List<TransactionFilter>> TransactionFilters { get; set; } = [];

    /// <summary>
    /// Goes through transaction filter arrays in reverse declaration order and checks whether any filter in each array
    /// matches the provided fields. Within a matching array, rules are evaluated in their declared order.
    /// </summary>
    /// <param name="fields">A read-only dictionary containing field names and their corresponding values to be evaluated.</param>
    /// <returns>True if the transaction is filtered out, false otherwise.</returns>
    public bool IsFilteredOut(IReadOnlyDictionary<string, string> fields)
    {
        // Reverse array order means later arrays in configuration override earlier ones.
        for (int i = TransactionFilters.Count - 1; i >= 0; i--)
        {
            // Look for any transaction that matches a filter
            if (TransactionFilters.ElementAt(i).Value.Any(filter => filter.IsFiltered(fields)))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Selects a group based on the provided field selectors and returns a tuple indicating where the match came from
    /// and the name of the selected group.
    /// </summary>
    /// <remarks>This method first attempts to match the provided fields against the expense group selectors.
    /// If no match is found, it then checks against the income group selectors.</remarks>
    /// <param name="fields">A read-only dictionary containing fields used to determine the matching group. Cannot be <see langword="null"/>.</param>
    /// <returns>A tuple where the first item is <see langword="true"/> if a group was not found in expenses selectors; otherwise,
    /// <see langword="false"/>. The second item is the name of the selected group, or <see langword="null"/> if no group was matched.
    /// When the first item is <see langword="true"/> and the second item is non-<see langword="null"/>, the group name comes from
    /// the income group selectors; when the first item is <see langword="false"/> and the second item is non-<see langword="null"/>,
    /// the group name comes from the expense group selectors.
    /// </returns>
    public (bool IsIncome, string? GroupName) SelectGroup(IReadOnlyDictionary<string, string> fields)
    {
        string? group = MatchGroupSelectors(ExpensesGroupSelectors, fields);
        return (group == null, group ?? MatchGroupSelectors(IncomeGroupSelectors, fields));
    }

    /// <summary>
    /// Support function that tries to find a group in the dictionary based on the selectors it gets.
    /// </summary>
    /// <param name="selectors">GroupSelector object used to find a group</param>
    /// <param name="fields"> read-only dictionary containing field names and their corresponding values to be evaluated.</param>
    /// <returns>A group if found, null otherwise.</returns>
    private static string? MatchGroupSelectors(OrderedDictionary<string, List<GroupSelector>> selectors, IReadOnlyDictionary<string, string> fields)
    {
        // Reverse array order means later arrays in configuration override earlier ones.
        for (int i = selectors.Count - 1; i >= 0; i--)
        {
            foreach (var selector in selectors.ElementAt(i).Value)
            {
                string? group = selector.SelectGroup(fields);
                if (group != null)
                {
                    return group;
                }
            }
        }

        return null;
    }
}
