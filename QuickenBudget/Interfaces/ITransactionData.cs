/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System;
using System.Collections.Generic;

using QuickenBudget.Models;

namespace QuickenBudget.Interfaces;

public interface ITransactionData
{
    /// <summary>
    /// Date and time of this object's creation.
    /// </summary>
    DateTimeOffset CreationTime { get; }


    /// <summary>
    /// All years for which there are transactions.
    /// </summary>
    IReadOnlyList<int> AllYears { get; }

    /// <summary>
    /// Get all transactions. This returns a list of all transactions, regardless of year or group. The transactions should be ordered by date.
    /// </summary>
    IReadOnlyList<Transaction> Transactions { get; }

    /// <summary>
    /// Information about expenses in a given year.
    /// </summary>
    /// <param name="year">The year for which to obtain expenses group information.</param>
    /// <returns>A list of <see cref="GroupTotals"/> objects containing the expenses group data for the specified year (can be an empty list).</returns>
    IReadOnlyList<GroupTotals> GetExpensesGroups(int year);

    /// <summary>
    /// Information about income in a given year.
    /// </summary>
    /// <param name="year">The year for which to obtain income group information.</param>
    /// <returns>A list of <see cref="GroupTotals"/> objects containing the income group data for the specified year (can be an empty list).</returns>
    IReadOnlyList<GroupTotals> GetIncomeGroups(int year);

    /// <summary>
    /// Retrieves the income group summary for the specified year.
    /// </summary>
    /// <param name="year">The year for which to obtain the income group summary. Must be a valid calendar year.</param>
    /// <returns>A <see cref="GroupTotals"/> object containing the income group data for the specified year (can be an empty list).</returns>
    GroupTotals GetIncomeGroupsTotal(int year);

    /// <summary>
    /// Retrieves the expenses group summary for the specified year.
    /// </summary>
    /// <param name="year">The year for which to obtain the expenses group summary. Must be a valid calendar year.</param>
    /// <returns>A <see cref="GroupTotals"/> object containing the expenses group data for the specified year (can be an empty list).</returns>
    GroupTotals GetExpensesGroupsTotal(int year);

    /// <summary>
    /// Retrieves a summary of transactions for each year.
    /// </summary>
    /// <returns>A list of <see cref="YearTotals"/> objects containing the yearly transaction summaries.</returns>
    IReadOnlyList<YearTotals> GetYearSummaries();

    /// <summary>
    /// Default color for income.
    /// </summary>
    string IncomeColor { get; }

    /// <summary>
    /// Default color for expenses.
    /// </summary>
    string ExpenseColor { get; }
}
