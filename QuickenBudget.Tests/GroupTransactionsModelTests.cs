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
public class GroupTransactionsModelTests : TestBase
{
    [TestMethod]
    public void OnGet_WithNullGroup_SetsGroupToAllExpensesAndFiltersTransactions()
    {
        // Arrange
        var mockTransactionData = new Mock<ITransactionData>();
        List<Transaction> transactions = [
            new(new DateOnly(2023, 1, 1), 1000, "Income", true),
            new(new DateOnly(2023, 2, 1), -200, "Food", false),
            new(new DateOnly(2023, 3, 1), -100, "Transport", false),
            new(new DateOnly(2024, 1, 1), -150, "Food", false)
        ];
        mockTransactionData.Setup(td => td.Transactions).Returns(transactions);
        var model = new GroupTransactionsModel(mockTransactionData.Object);
        int year = 2023;

        // Act
        model.OnGet("expense", year, null);

        // Assert
        model.Year.ShouldBe(year);
        model.Group.ShouldBe("All Expenses");
        // Ensure non null, then it should include Food and Transport for 2023,
        // but exclude 2024 transactions
        model.Transactions.ShouldNotBeNull().Count.ShouldBe(2);
        model.Transactions.All(t => t.Date.Year == year && !t.IsIncome).ShouldBeTrue();
        model.Transactions.SequenceEqual(model.Transactions.OrderBy(t => t.Date)).ShouldBeTrue(); // Ordered by date
    }

    [TestMethod]
    public void OnGet_WithSpecificGroup_SetsGroupAndFiltersTransactions()
    {
        // Arrange
        var mockTransactionData = new Mock<ITransactionData>();
        List<Transaction> transactions = [
            new(new DateOnly(2023, 1, 1), -200, "Food", false),
            new(new DateOnly(2023, 2, 1), -150, "Food", false),
            new(new DateOnly(2023, 3, 1), -100, "Transport", false),
            new(new DateOnly(2024, 1, 1), -300, "Food", false)
        ];
        mockTransactionData.Setup(td => td.Transactions).Returns(transactions);
        var model = new GroupTransactionsModel(mockTransactionData.Object);
        int year = 2023;
        string group = "Food";

        // Act
        model.OnGet("expense", year, group);

        // Assert
        model.Year.ShouldBe(year);
        model.Group.ShouldBe(group);
        model.Transactions.Count.ShouldBe(2); // Only Food for 2023
        model.Transactions.All(t => t.Date.Year == year && t.GroupName.Equals(group, StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
        model.Transactions.SequenceEqual(model.Transactions.OrderBy(t => t.Date)).ShouldBeTrue(); // Ordered by date
    }

    [TestMethod]
    public void Transactions_IsInitializedAsEmptyList()
    {
        // Arrange
        var mockTransactionData = new Mock<ITransactionData>();
        var model = new Pages.GroupTransactionsModel(mockTransactionData.Object);

        // Act & Assert
        model.Transactions.ShouldNotBeNull();
        model.Transactions.ShouldBeEmpty();
    }

    #region Income type

    [TestMethod]
    public void OnGet_WithIncomeType_FiltersToIncomeAndSetsGroupToAllIncome()
    {
        var mockTransactionData = new Mock<ITransactionData>();
        mockTransactionData.Setup(td => td.Transactions).Returns(
        [
            new Transaction(new DateOnly(2023, 2, 1), 1000m, "Salary", true),
            new Transaction(new DateOnly(2023, 1, 1),  500m, "Bonus",  true),
            new Transaction(new DateOnly(2023, 3, 1), -200m, "Food",   false),
        ]);
        var model = new GroupTransactionsModel(mockTransactionData.Object);

        model.OnGet("income", 2023, null);

        model.Group.ShouldBe("All Income");
        model.Transactions.Count.ShouldBe(2);
        model.Transactions.All(t => t.IsIncome).ShouldBeTrue();
        model.Transactions.SequenceEqual(model.Transactions.OrderBy(t => t.Date)).ShouldBeTrue();
    }

    [TestMethod]
    public void OnGet_WithIncomeTypeAndSpecificGroup_FiltersToThatGroup()
    {
        var mockTransactionData = new Mock<ITransactionData>();
        mockTransactionData.Setup(td => td.Transactions).Returns(
        [
            new Transaction(new DateOnly(2023, 1, 1), 1000m, "Salary", true),
            new Transaction(new DateOnly(2023, 2, 1),  500m, "Bonus",  true),
        ]);
        var model = new GroupTransactionsModel(mockTransactionData.Object);

        model.OnGet("income", 2023, "Salary");

        model.Group.ShouldBe("Salary");
        model.Transactions.Count.ShouldBe(1);
        model.Transactions[0].GroupName.ShouldBe("Salary");
    }

    #endregion

    #region AdditionalColumns

    [TestMethod]
    public void OnGet_WithOtherFields_PopulatesDistinctSortedAdditionalColumns()
    {
        var mockTransactionData = new Mock<ITransactionData>();
        mockTransactionData.Setup(td => td.Transactions).Returns(
        [
            new Transaction(new DateOnly(2023, 1, 1), -100m, "Food", false,
                new Dictionary<string, string> { ["Memo"] = "Groceries", ["Account"] = "Checking" }),
            new Transaction(new DateOnly(2023, 2, 1),  -50m, "Food", false,
                new Dictionary<string, string> { ["Memo"] = "Lunch", ["Tag"] = "Work" }),
        ]);
        var model = new GroupTransactionsModel(mockTransactionData.Object);

        model.OnGet("expense", 2023, null);

        model.AdditionalColumns.Count.ShouldBe(3); // Account, Memo, Tag — distinct
        model.AdditionalColumns.SequenceEqual(model.AdditionalColumns.OrderBy(k => k)).ShouldBeTrue();
        model.AdditionalColumns.ShouldContain("Account");
        model.AdditionalColumns.ShouldContain("Memo");
        model.AdditionalColumns.ShouldContain("Tag");
    }

    [TestMethod]
    public void OnGet_DuplicateOtherFieldKeys_DeduplicatedInAdditionalColumns()
    {
        var mockTransactionData = new Mock<ITransactionData>();
        mockTransactionData.Setup(td => td.Transactions).Returns(
        [
            new Transaction(new DateOnly(2023, 1, 1), -100m, "Food", false,
                new Dictionary<string, string> { ["Memo"] = "A" }),
            new Transaction(new DateOnly(2023, 2, 1),  -50m, "Food", false,
                new Dictionary<string, string> { ["Memo"] = "B" }),
        ]);
        var model = new GroupTransactionsModel(mockTransactionData.Object);

        model.OnGet("expense", 2023, null);

        model.AdditionalColumns.Count.ShouldBe(1);
        model.AdditionalColumns[0].ShouldBe("Memo");
    }

    [TestMethod]
    public void OnGet_NoOtherFields_AdditionalColumnsIsEmpty()
    {
        var mockTransactionData = new Mock<ITransactionData>();
        mockTransactionData.Setup(td => td.Transactions).Returns(
        [
            new Transaction(new DateOnly(2023, 1, 1), -100m, "Food", false),
        ]);
        var model = new GroupTransactionsModel(mockTransactionData.Object);

        model.OnGet("expense", 2023, null);

        model.AdditionalColumns.ShouldBeEmpty();
    }

    #endregion
}
