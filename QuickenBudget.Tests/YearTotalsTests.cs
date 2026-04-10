/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using Microsoft.VisualStudio.TestTools.UnitTesting;

using QuickenBudget.Models;

using Shouldly;

namespace QuickenBudget.Tests;

[TestClass]
public class YearTotalsTests : TestBase
{
    [TestMethod]
    public void ToString_FormatsAllFieldsWithCurrencyAndYear()
    {
        var yt = new YearTotals(2023, 60000m, 5000m, 36000m, 3000m);

        var result = yt.ToString();

        result.ShouldBe($"2023 - Income = {60000m:C} (Average: {5000m:C}), Expenses = {36000m:C} (Average: {3000m:C})");
    }

    [TestMethod]
    public void ToString_ContainsYearAndKeyLabels()
    {
        var yt = new YearTotals(2022, 0m, 0m, 0m, 0m);

        var result = yt.ToString();

        result.ShouldContain("2022");
        result.ShouldContain("Income");
        result.ShouldContain("Average");
        result.ShouldContain("Expenses");
    }

    [TestMethod]
    public void RecordEquality_SameValues_AreEqual()
    {
        var a = new YearTotals(2023, 1000m, 100m, 500m, 50m);
        var b = new YearTotals(2023, 1000m, 100m, 500m, 50m);

        a.ShouldBe(b);
    }

    [TestMethod]
    public void RecordEquality_DifferentYear_AreNotEqual()
    {
        var a = new YearTotals(2022, 1000m, 100m, 500m, 50m);
        var b = new YearTotals(2023, 1000m, 100m, 500m, 50m);

        a.ShouldNotBe(b);
    }
}
