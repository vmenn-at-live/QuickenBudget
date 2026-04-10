using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Shouldly;
using Moq;

using QuickenBudget.Interfaces;
using QuickenBudget.Models;
using QuickenBudget.Pages;

namespace QuickenBudget.Tests;

[TestClass]
public class IndexModelTests : TestBase
{
    [TestMethod]
    public void OnGet_SetsYearsToYearSummariesFromTransactionData()
    {
        // Arrange
        var mockTransactionData = new Mock<ITransactionData>();
        var expectedYears = new List<YearTotals>
        {
            new(2023, 50000, 1000, 40000, 1200),
            new(2024, 55000, 2000, 45000, 1500)
        };
        mockTransactionData.Setup(td => td.GetYearSummaries()).Returns(expectedYears);
        mockTransactionData.Setup(td => td.Transactions).Returns([]);
        var model = new IndexModel(mockTransactionData.Object);

        // Act
        model.OnGet();

        // Assert
        model.Years.ShouldBe(expectedYears);
        mockTransactionData.Verify(td => td.GetYearSummaries(), Times.Once);
    }

    [TestMethod]
    public void Years_IsInitializedAsEmptyList()
    {
        // Arrange
        var mockTransactionData = new Mock<ITransactionData>();
        var model = new IndexModel(mockTransactionData.Object);

        // Act & Assert
        model.Years.ShouldNotBeNull();
        model.Years.ShouldBeEmpty();
    }

    [TestMethod]
    public void IncomeColor_DelegatesToTransactionData()
    {
        var mockTransactionData = new Mock<ITransactionData>();
        mockTransactionData.Setup(td => td.IncomeColor).Returns("#85cc66");
        var model = new IndexModel(mockTransactionData.Object);

        model.IncomeColor.ShouldBe("#85cc66");
    }

    [TestMethod]
    public void ExpenseColor_DelegatesToTransactionData()
    {
        var mockTransactionData = new Mock<ITransactionData>();
        mockTransactionData.Setup(td => td.ExpenseColor).Returns("#ff794d");
        var model = new IndexModel(mockTransactionData.Object);

        model.ExpenseColor.ShouldBe("#ff794d");
    }

    [TestMethod]
    public void OnGet_SetsRunningTotalFromTransactions()
    {
        var mockTransactionData = new Mock<ITransactionData>();
        mockTransactionData.Setup(td => td.GetYearSummaries()).Returns([]);
        mockTransactionData.Setup(td => td.Transactions).Returns(
        [
            new Transaction(new DateOnly(2023, 1, 1), 100m, "Income", true),
        ]);
        var model = new IndexModel(mockTransactionData.Object);

        model.OnGet();

        model.RunningTotal.ShouldNotBeNull();
        model.RunningTotal.ShouldNotBeEmpty();
    }

    #region ToRunningBalanceByDate

    // Anonymous types defined in another assembly are inaccessible via dynamic dispatch;
    // use reflection instead.
    private static T Prop<T>(object obj, string name) =>
        (T)obj.GetType().GetProperty(name)!.GetValue(obj)!;

    [TestMethod]
    public void ToRunningBalanceByDate_NoTransactions_ReturnsEmpty()
    {
        var mockTransactionData = new Mock<ITransactionData>();
        mockTransactionData.Setup(td => td.GetYearSummaries()).Returns([]);
        mockTransactionData.Setup(td => td.Transactions).Returns([]);
        var model = new IndexModel(mockTransactionData.Object);

        model.ToRunningBalanceByDate().ShouldBeEmpty();
    }

    [TestMethod]
    public void ToRunningBalanceByDate_MultipleTransactions_ReturnsCumulativeBalance()
    {
        var mockTransactionData = new Mock<ITransactionData>();
        mockTransactionData.Setup(td => td.Transactions).Returns(
        [
            new Transaction(new DateOnly(2023, 1, 1), 100m, "Income", true),
            new Transaction(new DateOnly(2023, 1, 3), -50m, "Food",   false),
            new Transaction(new DateOnly(2023, 1, 5), 200m, "Income", true),
        ]);
        var model = new IndexModel(mockTransactionData.Object);

        var entries = model.ToRunningBalanceByDate().ToList();

        entries.Count.ShouldBe(3);
        Prop<decimal>(entries[0], "balance").ShouldBe(100m);
        Prop<decimal>(entries[1], "balance").ShouldBe(50m);   // 100 - 50
        Prop<decimal>(entries[2], "balance").ShouldBe(250m);  // 50 + 200
    }

    [TestMethod]
    public void ToRunningBalanceByDate_SameDateTransactions_GroupedAndSummed()
    {
        var mockTransactionData = new Mock<ITransactionData>();
        mockTransactionData.Setup(td => td.Transactions).Returns(
        [
            new Transaction(new DateOnly(2023, 1, 1), 100m, "Income", true),
            new Transaction(new DateOnly(2023, 1, 1), -30m, "Food",   false),
            new Transaction(new DateOnly(2023, 1, 2),  50m, "Income", true),
        ]);
        var model = new IndexModel(mockTransactionData.Object);

        var entries = model.ToRunningBalanceByDate().ToList();

        entries.Count.ShouldBe(2); // two distinct dates
        Prop<decimal>(entries[0], "balance").ShouldBe(70m);   // 100 - 30
        Prop<decimal>(entries[1], "balance").ShouldBe(120m);  // 70 + 50
    }

    [TestMethod]
    public void ToRunningBalanceByDate_MonthIsZeroIndexedForJavaScript()
    {
        var mockTransactionData = new Mock<ITransactionData>();
        mockTransactionData.Setup(td => td.Transactions).Returns(
        [
            new Transaction(new DateOnly(2023, 3, 15), 100m, "Income", true),
        ]);
        var model = new IndexModel(mockTransactionData.Object);

        var entry = model.ToRunningBalanceByDate().Single();

        Prop<int>(entry, "year").ShouldBe(2023);
        Prop<int>(entry, "month").ShouldBe(2);  // March (3) is 0-indexed to 2
        Prop<int>(entry, "day").ShouldBe(15);
    }

    [TestMethod]
    public void ToRunningBalanceByDate_OrderedByDateAscending()
    {
        var mockTransactionData = new Mock<ITransactionData>();
        mockTransactionData.Setup(td => td.Transactions).Returns(
        [
            new Transaction(new DateOnly(2023, 3, 1),  50m, "Income", true),
            new Transaction(new DateOnly(2023, 1, 1), 100m, "Income", true),
            new Transaction(new DateOnly(2023, 2, 1), 200m, "Income", true),
        ]);
        var model = new IndexModel(mockTransactionData.Object);

        var entries = model.ToRunningBalanceByDate().ToList();

        Prop<int>(entries[0], "month").ShouldBe(0);  // January
        Prop<int>(entries[1], "month").ShouldBe(1);  // February
        Prop<int>(entries[2], "month").ShouldBe(2);  // March
    }

    #endregion
}
