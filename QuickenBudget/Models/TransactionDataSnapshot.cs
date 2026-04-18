/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using Microsoft.Extensions.Logging;

using QuickenBudget.Interfaces;

namespace QuickenBudget.Models;

/// <summary>
/// Immutable snapshot of derived transaction data used for consistent reads.
/// </summary>
public sealed class TransactionDataSnapshot : ITransactionData
{
    public const string IncomeGroupTotalName = "All Income";
    public const string ExpensesGroupTotalName = "All Expenses";
    public const string OtherGroupName = "Other";
    private const string DefaultIncomeColorValue = "#85cc66";
    private const string DefaultExpenseColorValue = "#ff794d";

    private readonly ReadOnlyCollection<Transaction> _transactions = [];
    private readonly ReadOnlyCollection<int> _allYears = [];
    private readonly Dictionary<int, GroupTotals> _incomeGroupsTotals = [];
    private readonly Dictionary<int, GroupTotals> _expensesGroupsTotals = [];
    private readonly Dictionary<int, ReadOnlyCollection<GroupTotals>> _expensesGroups = [];
    private readonly Dictionary<int, ReadOnlyCollection<GroupTotals>> _incomeGroups = [];
    private readonly ReadOnlyCollection<YearTotals> _yearlyIncomeTotals = [];

    /// <summary>
    /// Gets the collection of transactions associated with this instance.
    /// </summary>
    public IReadOnlyList<Transaction> Transactions => _transactions;

    /// <summary>
    /// Gets all years for which transactions are available, sorted in ascending order.
    /// </summary>
    public IReadOnlyList<int> AllYears => _allYears;

    public string IncomeColor => DefaultIncomeColorValue;

    public string ExpenseColor => DefaultExpenseColorValue;

    public IReadOnlyList<GroupTotals> GetExpensesGroups(int year) => _expensesGroups.TryGetValue(year, out var groups) ? groups : [];

    public IReadOnlyList<GroupTotals> GetIncomeGroups(int year) => _incomeGroups.TryGetValue(year, out var groups) ? groups : [];

    public GroupTotals GetIncomeGroupsTotal(int year) => _incomeGroupsTotals.TryGetValue(year, out GroupTotals? group) ?
        group : new GroupTotals(year, IncomeGroupTotalName, 0, 0, DefaultIncomeColorValue);

    public GroupTotals GetExpensesGroupsTotal(int year) => _expensesGroupsTotals.TryGetValue(year, out GroupTotals? group) ?
        group : new GroupTotals(year, ExpensesGroupTotalName, 0, 0, DefaultExpenseColorValue);

    public IReadOnlyList<YearTotals> GetYearSummaries() => _yearlyIncomeTotals;

    public DateTimeOffset CreationTime { get; init; } = DateTimeOffset.UtcNow;

    public TransactionDataSnapshot() {}

    public TransactionDataSnapshot(IReadOnlyList<Transaction> transactions, int currentYear)
    {
        _transactions = new ReadOnlyCollection<Transaction>([.. transactions]);
        _allYears = new ReadOnlyCollection<int>([.. transactions.Select(t => t.Date.Year).Distinct().OrderBy(y => y)]);

        var uniqueGroups = transactions
            .Select(t => t.GroupName)
            .Distinct()
            .OrderBy(g => g)
            .ToList();

        Dictionary<string, string> colorDictionary = [];

        if (uniqueGroups.Count != 0)
        {
            string[] colors = CreateColors(uniqueGroups.Count);
            if (colors.Length != uniqueGroups.Count)
            {
                throw new InvalidOperationException("The number of generated colors should match the number of unique groups.");
            }

            colorDictionary = uniqueGroups.Zip(colors).ToDictionary(x => x.First, x => x.Second, StringComparer.OrdinalIgnoreCase);
        }

        Dictionary<int, ReadOnlyCollection<GroupTotals>> groupsByYear(bool isIncome) =>
            transactions.Where(t => t.IsIncome == isIncome)
                .GroupBy(t => t.Date.Year)
                .ToDictionary(g => g.Key,
                    g => new ReadOnlyCollection<GroupTotals>(
                       [.. g.GroupBy(t => (name: t.GroupName, year: t.Date.Year))
                        .Select(group => new GroupTotals(
                            group.Key.year,
                            group.Key.name,
                            group.Sum(t => t.Amount),
                            group.Sum(t => t.Amount) / (group.Key.year != currentYear ? 12 : group.Max(t => t.Date.Month)),
                            colorDictionary.TryGetValue(group.Key.name, out var color) ? color : (isIncome ? DefaultIncomeColorValue : DefaultExpenseColorValue)))
                        .OrderBy(group => isIncome ? group.TotalAmount : -group.TotalAmount)]));

        _expensesGroups = groupsByYear(false);
        _incomeGroups = groupsByYear(true);

        Dictionary<int, GroupTotals> computeTotal(bool isIncome) =>
            (isIncome ? _incomeGroups : _expensesGroups)
                .ToDictionary(kvp => kvp.Key, kvp => new GroupTotals(
                    kvp.Key,
                    isIncome ? IncomeGroupTotalName : ExpensesGroupTotalName,
                    kvp.Value.Sum(group => group.TotalAmount),
                    kvp.Value.Sum(group => group.AvgPerMonth),
                    isIncome ? DefaultIncomeColorValue : DefaultExpenseColorValue));

        _incomeGroupsTotals = computeTotal(true);
        _expensesGroupsTotals = computeTotal(false);

        _yearlyIncomeTotals =
        [
            .. AllYears.Order()
                .Select(year =>
                {
                    GroupTotals incomeTotals = GetIncomeGroupsTotal(year);
                    IReadOnlyList<GroupTotals> yearExpensesGroups = GetExpensesGroups(year);
                    return new YearTotals(year, incomeTotals.TotalAmount, incomeTotals.AvgPerMonth, -yearExpensesGroups.Sum(group => group.TotalAmount), -yearExpensesGroups.Sum(group => group.AvgPerMonth));
                })
        ];
    }

    /// <summary>
    /// Generates an array of distinct color values in hexadecimal RGB format.
    /// </summary>
    /// <remarks>The generated colors are designed to be visually distinct by varying hue and brightness. The
    /// method avoids green and cyan hues to improve differentiation from the income-related colors, making the
    /// colors suitable for use in charts or visualizations where clear separation is important.</remarks>
    /// <param name="count">The number of colors to generate. Must be non-negative.</param>
    /// <returns>An array of strings, each representing a color in hexadecimal RGB format (e.g., "#FFAA00"). The array contains
    /// exactly the specified number of colors.</returns>
    private static string[] CreateColors(int count)
    {
        double[] values = [.45, .85];
        const double saturation = .75;
        int hueCount = (count + values.Length - 1) / values.Length;
        double hueStep = 300.0 / hueCount;
        double hue = 0;

        List<string> rgb = [];
        for (int i = 0, valueStep = 0; i < count; i++)
        {
            double value = values[valueStep];
            double sector = hue / 60;
            double c = value * saturation;
            double x = c * (1 - Math.Abs(sector % 2 - 1));
            double m = value - c;
            double r, g, b;

            switch (sector)
            {
                case < 1:
                    r = c; g = x; b = 0;
                    break;
                case < 2:
                    r = x; g = c; b = 0;
                    break;
                case < 3:
                    r = 0; g = c; b = x;
                    break;
                case < 4:
                    r = 0; g = x; b = c;
                    break;
                case < 5:
                    r = x; g = 0; b = c;
                    break;
                default:
                    r = c; g = 0; b = x;
                    break;
            }

            byte red = (byte)Math.Round((r + m) * 255);
            byte green = (byte)Math.Round((g + m) * 255);
            byte blue = (byte)Math.Round((b + m) * 255);

            rgb.Add($"#{red:X2}{green:X2}{blue:X2}");

            valueStep = (valueStep + 1) % values.Length;
            if (valueStep == 0)
            {
                hue += hueStep;
                if (hue >= 65 && hue < 180)
                {
                    hue = 180;
                }
            }
        }

        return [.. rgb];
    }

    public static TransactionDataSnapshot CreateSnapshot(ILogger logger, ITransactionReader reader, DateTimeOffset utcTime, int currentYear)
    {
        List<Transaction> transactions = reader.Ingest();
        if (transactions.Count == 0)
        {
            logger.LogWarning("No transactions were loaded from the file. Please check the file format and content.");
            throw new InvalidOperationException("No transactions were loaded from the file.");
        }

        return new TransactionDataSnapshot(transactions, currentYear)
        {
            CreationTime = utcTime
        };
    }
}
