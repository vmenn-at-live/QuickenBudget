using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;
using Serilog.Events;
using Shouldly;

using QuickenBudget.Interfaces;
using QuickenBudget.Models;
using QuickenBudget.Services;


namespace QuickenBudget.Tests;

[TestClass]
public class TransactionDataTests : TestBase
{
    private readonly Mock<ITransactionReader> _mockReader = new();
    private readonly Mock<ITransactionReloadStatus> _mockStatus = new();

    public TransactionDataTests() : base(true, LogEventLevel.Information)
    {
    }

    private ITransactionData InitializeTransactionData(IReadOnlyList<Transaction> dataToLoad, int year = 2026)
    {
        TransactionDataSnapshot snapshot = new(dataToLoad, year);
        _mockStatus.Setup(s => s.Snapshot).Returns(snapshot);
        return new TransactionData(_mockStatus.Object);
    }

    /// <summary>
    /// Verifies that the GetIncomeGroup method returns the correct income group for a specified year.
    /// </summary>
    [TestMethod]
    public void GetIncomeGroup_ReturnsIncomeGroupForYear()
    {
        List<Transaction> transactions =
        [
            new (new DateOnly(2023, 1, 1), 1000, "Income", true),
            new (new DateOnly(2023, 2, 1), 500, "Income", true)
        ];

        var transactionData = InitializeTransactionData(transactions);

        var incomeGroup = transactionData.GetIncomeGroupsTotal(2023);
        incomeGroup.GroupName.ShouldBe("All Income");
        incomeGroup.TotalAmount.ShouldBe(1500m);
        incomeGroup.HttpColor.ShouldBe("#85cc66");
    }

    [TestMethod]
    public void GetIncomeGroup_ReturnsEmptyGroupForNonExistentYear()
    {
        List<Transaction> transactions = [new (new DateOnly(2023, 1, 1), 1000, "All Income", true)];

        var transactionData = InitializeTransactionData(transactions);

        var incomeGroup = transactionData.GetIncomeGroupsTotal(2024);

        incomeGroup.GroupName.ShouldBe("All Income");
        incomeGroup.TotalAmount.ShouldBe(0m);
        incomeGroup.HttpColor.ShouldBe("#85cc66");
    }

    /// <summary>
    /// Verifies that the GetExpensesGroups method returns expense groups for the specified year.
    /// </summary>
    [TestMethod]
    public void GetExpensesGroups_ReturnsExpensesGroupsForYear()
    {
        List<Transaction> transactions =
        [
            new (new DateOnly(2023, 1, 1), -200m, "Food", false),
            new (new DateOnly(2023, 2, 1), -150m, "Food", false),
            new (new DateOnly(2023, 3, 1), -100m, "Transport", false)
        ];

        var transactionData = InitializeTransactionData(transactions);

        var expensesGroups = transactionData.GetExpensesGroups(2023);
        expensesGroups.Count.ShouldBe(2);
        expensesGroups.ShouldContain(g => g.GroupName == "Food" && g.TotalAmount == -350m);
        expensesGroups.ShouldContain(g => g.GroupName == "Transport" && g.TotalAmount == -100m);
        expensesGroups.All(g => !string.IsNullOrEmpty(g.HttpColor)).ShouldBeTrue();
    }

    /// <summary>
    /// Verifies that the GetExpensesGroups method returns an empty list when called with a year for which no transactions exist.
    /// </summary>
    [TestMethod]
    public void GetExpensesGroups_ReturnsEmptyListForNonExistentYear()
    {
        List<Transaction> transactions = [new (new DateOnly(2023, 1, 1), -200m, "Food", false)];

        var transactionData = InitializeTransactionData(transactions);

        var expensesGroups = transactionData.GetExpensesGroups(2024);
        expensesGroups.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies that the GetYearSummaries method returns the correct yearly totals for income and expenses.
    /// </summary>
    [TestMethod]
    public void GetYearSummaries_ReturnsYearlyTotals()
    {
        List<Transaction> transactions =
        [
            new(new DateOnly(2023, 1, 1), 1000m, "Income", true),
            new(new DateOnly(2023, 2, 1), -200m, "Food", false),
            new(new DateOnly(2024, 1, 1), 1500m, "Income", true),
            new(new DateOnly(2024, 2, 1), -300m, "Transport", false)
        ];

        var transactionData = InitializeTransactionData(transactions);

        var yearSummaries = transactionData.GetYearSummaries();
        yearSummaries.Count.ShouldBe(2);
        yearSummaries.ShouldContain(y => y.Year == 2023 && y.TotalIncome == 1000 && y.TotalExpenses == 200);
        yearSummaries.ShouldContain(y => y.Year == 2024 && y.TotalIncome == 1500 && y.TotalExpenses == 300);
    }

    /// <summary>
    /// Verifies that the AllYears property returns a distinct, ordered list of years from the transaction data.
    /// </summary>
    [TestMethod]
    public void AllYears_ReturnsDistinctOrderedYears()
    {
        List<Transaction> transactions =
        [
            new (new DateOnly(2023, 1, 1), 1000m, "Income", true),
            new (new DateOnly(2024, 1, 1), -200m, "Food", false),
            new (new DateOnly(2023, 2, 1), -100m, "Transport", false)
        ];

        var transactionData = InitializeTransactionData(transactions);

        var allYears = transactionData.AllYears;

        allYears.Count.ShouldBe(2);
        allYears[0].ShouldBe(2023);
        allYears[1].ShouldBe(2024);
    }

    /// <summary>
    /// Tests the CreateColors method to ensure it generates an array of distinct color codes in hexadecimal format.
    /// </summary>
    [TestMethod]
    public void CreateColors_GeneratesDistinctColors()
    {
        string[]? colors = typeof(TransactionDataSnapshot)
            .GetMethod("CreateColors", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?
            .Invoke(null, [3]) as string[];

        colors.ShouldNotBeNull();
        colors!.Length.ShouldBe(3);
        colors.Distinct().Count().ShouldBe(3);
        colors.All(c => c.StartsWith('#') && c.Length == 7).ShouldBeTrue();
    }

    /// <summary>
    /// Verifies that created snapshot correctly calculates the average income for the current year using the maximum month.
    /// </summary>
    [TestMethod]
    public void Initialize_CurrentYearAverage_UsesMaxMonth()
    {
        int year = 2024;
        var transactions = new List<Transaction>
        {
            new(new DateOnly(year, 1, 1), 1200m, "Income", true),
            new(new DateOnly(year, 3, 1), 600m, "Income", true)
        };
        var transactionData = InitializeTransactionData(transactions, year);

        var incomeTotal = transactionData.GetIncomeGroupsTotal(year);
        incomeTotal.TotalAmount.ShouldBe(1800m);
        incomeTotal.AvgPerMonth.ShouldBe(600m);
    }


    [TestMethod]
    public void Constructor_CapturesSnapshotFromStatus()
    {
        var mockSnapshot = new Mock<ITransactionData>();
        mockSnapshot.Setup(s => s.AllYears).Returns([2024]);
        _mockStatus.Setup(s => s.Snapshot).Returns(mockSnapshot.Object);

        var data = new TransactionData(_mockStatus.Object);

        data.AllYears.ShouldBe([2024]);
        _mockStatus.Verify(s => s.Snapshot, Times.Once);
    }

    [TestMethod]
    public void Constructor_UsesSingleCapturedSnapshot()
    {
        var snapshot = new Mock<ITransactionData>();
        snapshot.Setup(s => s.GetYearSummaries()).Returns([new YearTotals(2023, 1, 1, 1, 1)]);

        _mockStatus.Setup(w => w.Snapshot).Returns(snapshot.Object);

        var data = new TransactionData(_mockStatus.Object);

        data.GetYearSummaries().Single().Year.ShouldBe(2023);
        _mockStatus.Verify(w => w.Snapshot, Times.Once);
    }

    #region GetIncomeGroups

    [TestMethod]
    public void GetIncomeGroups_ReturnsIncomeGroupsForYear()
    {
        var transactions = new List<Transaction>
        {
            new(new DateOnly(2023, 1, 1), 1000m, "Salary", true),
            new(new DateOnly(2023, 3, 1),  500m, "Salary", true),
            new(new DateOnly(2023, 6, 1),  300m, "Bonus",  true),
        };
        var transactionData = InitializeTransactionData(transactions);

        var groups = transactionData.GetIncomeGroups(2023);

        groups.Count.ShouldBe(2);
        groups.ShouldContain(g => g.GroupName == "Salary" && g.TotalAmount == 1500m);
        groups.ShouldContain(g => g.GroupName == "Bonus"  && g.TotalAmount == 300m);
    }

    [TestMethod]
    public void GetIncomeGroups_ReturnsEmptyForNonExistentYear()
    {
        var transactions = new List<Transaction>
        {
            new(new DateOnly(2023, 1, 1), 1000m, "Salary", true),
        };
        var transactionData = InitializeTransactionData(transactions);

        transactionData.GetIncomeGroups(2024).ShouldBeEmpty();
    }

    #endregion

    #region GetExpensesGroupsTotal

    [TestMethod]
    public void GetExpensesGroupsTotal_ReturnsSumOfAllExpenseGroups()
    {
        var transactions = new List<Transaction>
        {
            new(new DateOnly(2023, 1, 1), -200m, "Food",      false),
            new(new DateOnly(2023, 2, 1), -100m, "Transport", false),
            new(new DateOnly(2023, 3, 1), -150m, "Food",      false),
        };
        var transactionData = InitializeTransactionData(transactions);

        var total = transactionData.GetExpensesGroupsTotal(2023);

        total.GroupName.ShouldBe("All Expenses");
        total.TotalAmount.ShouldBe(-450m);
        total.HttpColor.ShouldNotBeNullOrEmpty();
    }

    [TestMethod]
    public void GetExpensesGroupsTotal_ReturnsZeroTotalsForNonExistentYear()
    {
        var transactions = new List<Transaction>
        {
            new(new DateOnly(2023, 1, 1), -200m, "Food", false),
        };
        var transactionData = InitializeTransactionData(transactions);

        var total = transactionData.GetExpensesGroupsTotal(2024);

        total.GroupName.ShouldBe("All Expenses");
        total.TotalAmount.ShouldBe(0m);
    }

    #endregion

    #region Transactions property

    [TestMethod]
    public void Transactions_ReturnsAllTransactionsFromSnapshot()
    {
        var transactions = new List<Transaction>
        {
            new(new DateOnly(2023, 1, 1), -200m, "Food",   false),
            new(new DateOnly(2023, 2, 1), 1000m, "Income", true),
            new(new DateOnly(2024, 1, 1),  500m, "Income", true),
        };
        var transactionData = InitializeTransactionData(transactions);

        var result = transactionData.Transactions;

        result.Count.ShouldBe(3);
        result.ShouldContain(t => t.GroupName == "Food"   && t.Amount == -200m);
        result.ShouldContain(t => t.GroupName == "Income" && t.Amount == 1000m);
    }

    [TestMethod]
    public void Transactions_ReturnsEmptyListForEmptySnapshot()
    {
        var transactionData = InitializeTransactionData([]);

        transactionData.Transactions.ShouldBeEmpty();
    }

    #endregion

    #region Empty snapshot

    [TestMethod]
    public void Constructor_EmptyTransactionList_ReturnsZeroTotalsAndEmptyCollections()
    {
        var transactionData = InitializeTransactionData([]);

        transactionData.AllYears.ShouldBeEmpty();
        transactionData.GetYearSummaries().ShouldBeEmpty();
        transactionData.GetExpensesGroups(2023).ShouldBeEmpty();
        transactionData.GetIncomeGroups(2023).ShouldBeEmpty();
        transactionData.GetIncomeGroupsTotal(2023).TotalAmount.ShouldBe(0m);
        transactionData.GetExpensesGroupsTotal(2023).TotalAmount.ShouldBe(0m);
    }

    #endregion

    #region Average calculations

    [TestMethod]
    public void Initialize_PreviousYearAverage_DividesByTwelve()
    {
        int currentYear = 2026;
        var transactions = new List<Transaction>
        {
            new(new DateOnly(2022, 1, 1), 1200m, "Income", true),
            new(new DateOnly(2022, 3, 1),  600m, "Income", true),
        };
        var transactionData = InitializeTransactionData(transactions, currentYear);

        var incomeTotal = transactionData.GetIncomeGroupsTotal(2022);

        incomeTotal.TotalAmount.ShouldBe(1800m);
        incomeTotal.AvgPerMonth.ShouldBe(150m); // 1800 / 12
    }

    [TestMethod]
    public void GetYearSummaries_ReturnsCorrectAverageIncomeAndExpenses()
    {
        int currentYear = 2026;
        var transactions = new List<Transaction>
        {
            new(new DateOnly(2024, 1, 1), 1200m,  "Income", true),
            new(new DateOnly(2024, 6, 1), -600m,  "Food",   false),
        };
        var transactionData = InitializeTransactionData(transactions, currentYear);

        var summary = transactionData.GetYearSummaries().ShouldHaveSingleItem();

        summary.Year.ShouldBe(2024);
        summary.TotalIncome.ShouldBe(1200m);
        summary.AverageIncome.ShouldBe(100m);     // 1200 / 12
        summary.TotalExpenses.ShouldBe(600m);     // -(-600)
        summary.AverageExpenses.ShouldBe(50m);    // 600 / 12
    }

    #endregion
}
