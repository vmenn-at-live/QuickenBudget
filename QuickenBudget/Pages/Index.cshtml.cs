/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System.Collections.Generic;
using System.Linq;

using Microsoft.AspNetCore.Mvc.RazorPages;

using QuickenBudget.Interfaces;
using QuickenBudget.Models;

namespace QuickenBudget.Pages;

public class IndexModel(ITransactionData transactionData) : PageModel
{
    public IReadOnlyList<YearTotals> Years { get; set; } = [];
    public IEnumerable<object> RunningTotal { get; set; } = [];
    public string IncomeColor => transactionData.IncomeColor;
    public string ExpenseColor => transactionData.ExpenseColor;

    public void OnGet()
    {
        Years = transactionData.GetYearSummaries();
        RunningTotal = ToRunningBalanceByDate();
    }

    /// <summary>
    /// Produces a per-day running balance: a single entry per date where Balance is the sum of all transactions
    /// up to and including that date.
    /// </summary>
    public IEnumerable<object> ToRunningBalanceByDate()
    {
        decimal cumulative = 0m;
        var grouped = transactionData.Transactions
            .GroupBy(t => t.Date)
            .OrderBy(g => g.Key);

        foreach (var dayGroup in grouped)
        {
            var dayTotal = dayGroup.Sum(t => t.Amount);
            cumulative += dayTotal;
            yield return new {year = dayGroup.Key.Year, month = dayGroup.Key.Month - 1, day = dayGroup.Key.Day, balance = cumulative};
        }
    }

}
