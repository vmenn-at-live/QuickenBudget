/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
namespace QuickenBudget.Models;

public record YearTotals(int Year, decimal TotalIncome, decimal AverageIncome, decimal TotalExpenses, decimal AverageExpenses)
{
    public override string ToString() => $"{Year} - Income = {TotalIncome:C} (Average: {AverageIncome:C}), Expenses = {TotalExpenses:C} (Average: {AverageExpenses:C})";
}
