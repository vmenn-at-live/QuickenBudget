/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.AspNetCore.Mvc.RazorPages;

using QuickenBudget.Interfaces;
using QuickenBudget.Models;

namespace QuickenBudget.Pages;

/// <summary>
/// Data for the page that displays all transactions for a given year and group. The transactions are ordered by date.
/// </summary>
/// <param name="transactionData"></param>
public class GroupTransactionsModel(ITransactionData transactionData) : PageModel
{
    public int Year { get; set; }
    public string Group { get; set; } = "";
    public List<Transaction> Transactions { get; set; } = [];
    public List<string> AdditionalColumns { get; set; } = [];

    public void OnGet(string type, int year, string? group)
    {
        Year = year;
        bool isIncome = type.Equals("income", StringComparison.OrdinalIgnoreCase);
        if (group == null)
        {
            Transactions = [.. transactionData.Transactions
            .Where(t => t.Date.Year == year && t.IsIncome == isIncome)
            .OrderBy(t => t.Date)];

            Group = isIncome ? "All Income" : "All Expenses";
        }
        else
        {
            Transactions = [.. transactionData.Transactions
            .Where(t => t.Date.Year == year && t.IsIncome == isIncome && t.GroupName.Equals(group, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Date)];

            Group = group;
        }

        AdditionalColumns = [.. Transactions.SelectMany(t => t.OtherFields.Keys).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(k => k)];
    }
}
