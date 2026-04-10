/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System.Collections.Generic;

using Microsoft.AspNetCore.Mvc.RazorPages;

using QuickenBudget.Interfaces;
using QuickenBudget.Models;

namespace QuickenBudget.Pages;

public class YearSummaryModel(ITransactionData transactionData) : PageModel
{
    public int Year { get; set; }
    public IReadOnlyList<GroupTotals> ExpensesGroups { get; set; } = [];
    public IReadOnlyList<GroupTotals> IncomeGroups { get; set; } = [];
    public decimal ExpensesTotal { get; set; }
    public decimal ExpensesAverage { get; set; }
    public string ExpensesColor { get; set; } = string.Empty;
    public decimal IncomeTotal { get; set; }
    public decimal IncomeAverage { get; set; }
    public string IncomeColor { get; set; } = string.Empty;

    public void OnGet(int year)
    {
        ExpensesGroups = transactionData.GetExpensesGroups(year);
        IncomeGroups = transactionData.GetIncomeGroups(year);
        var expenses = transactionData.GetExpensesGroupsTotal(year);
        ExpensesTotal = -expenses.TotalAmount;
        ExpensesAverage = -expenses.AvgPerMonth;
        ExpensesColor = expenses.HttpColor;
        var income = transactionData.GetIncomeGroupsTotal(year);
        IncomeTotal = income.TotalAmount;
        IncomeAverage = income.AvgPerMonth;
        IncomeColor = income.HttpColor;
        Year = year;
    }
}
