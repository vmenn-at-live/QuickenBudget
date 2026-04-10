/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System;
using System.Collections.Generic;

using QuickenBudget.Interfaces;
using QuickenBudget.Models;

namespace QuickenBudget.Services;

/// <summary>
/// Transient façade over a stable transaction snapshot captured from <see cref="ITransactionReloadStatus"/>.
/// </summary>
public class TransactionData(ITransactionReloadStatus status) : ITransactionData
{
    private readonly ITransactionData _snapshot = status.Snapshot;

    public DateTimeOffset CreationTime => _snapshot.CreationTime;

    public IReadOnlyList<int> AllYears => _snapshot.AllYears;

    public IReadOnlyList<Transaction> Transactions => _snapshot.Transactions;

    public string IncomeColor => _snapshot.IncomeColor;

    public string ExpenseColor => _snapshot.ExpenseColor;

    public IReadOnlyList<GroupTotals> GetExpensesGroups(int year) => _snapshot.GetExpensesGroups(year);

    public IReadOnlyList<GroupTotals> GetIncomeGroups(int year) => _snapshot.GetIncomeGroups(year);

    public GroupTotals GetIncomeGroupsTotal(int year) => _snapshot.GetIncomeGroupsTotal(year);

    public GroupTotals GetExpensesGroupsTotal(int year) => _snapshot.GetExpensesGroupsTotal(year);

    public IReadOnlyList<YearTotals> GetYearSummaries() => _snapshot.GetYearSummaries();
}
