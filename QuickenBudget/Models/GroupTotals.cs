/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */

namespace QuickenBudget.Models;

public record GroupTotals(int Year, string GroupName, decimal TotalAmount, decimal AvgPerMonth, string HttpColor);
