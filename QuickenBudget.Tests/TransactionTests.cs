/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using QuickenBudget.Models;

using Shouldly;

namespace QuickenBudget.Tests;

[TestClass]
public class TransactionTests : TestBase
{
    private static readonly DateOnly DefaultDate = new(2024, 1, 1);

    private static GroupSelector MakeGroupSelector(string field, string filter, string targetGroup) =>
        new() { Field = field, Filter = filter, TargetGroup = targetGroup };

    private static TransactionFilter MakeRegexFilter(string field, string filter) =>
        new() { Field = field, Filter = filter };

    #region CreateTransaction — basic field parsing

    [TestMethod]
    public void CreateTransaction_ValidAmount_CreatesTransaction()
    {
        var fields = new Dictionary<string, string> { ["Amount"] = "100" };

        var (_, tx) = Transaction.CreateTransaction(fields, DefaultDate, null);

        tx.ShouldNotBeNull();
        tx!.Amount.ShouldBe(100m);
    }

    [TestMethod]
    public void CreateTransaction_PositiveAmount_SetsIsIncomeTrue()
    {
        var fields = new Dictionary<string, string> { ["Amount"] = "500" };

        var (_, tx) = Transaction.CreateTransaction(fields, DefaultDate, null);

        tx!.IsIncome.ShouldBeTrue();
    }

    [TestMethod]
    public void CreateTransaction_NegativeAmount_SetsIsIncomeFalse()
    {
        var fields = new Dictionary<string, string> { ["Amount"] = "-250" };

        var (_, tx) = Transaction.CreateTransaction(fields, DefaultDate, null);

        tx!.IsIncome.ShouldBeFalse();
    }

    [TestMethod]
    public void CreateTransaction_NoSelectorsMatch_SetsGroupNameToOther()
    {
        var fields = new Dictionary<string, string> { ["Amount"] = "100" };

        var (_, tx) = Transaction.CreateTransaction(fields, DefaultDate, null);

        tx!.GroupName.ShouldBe("Other");
    }

    [TestMethod]
    public void CreateTransaction_MissingAmount_ReturnsNullTransaction()
    {
        var fields = new Dictionary<string, string> { ["Category"] = "Food" };

        var (date, tx) = Transaction.CreateTransaction(fields, DefaultDate, null);

        tx.ShouldBeNull();
        date.ShouldBe(DefaultDate);
    }

    [TestMethod]
    public void CreateTransaction_InvalidAmountString_ReturnsNullTransaction()
    {
        var fields = new Dictionary<string, string> { ["Amount"] = "not-a-number" };

        var (_, tx) = Transaction.CreateTransaction(fields, DefaultDate, null);

        tx.ShouldBeNull();
    }

    [TestMethod]
    public void CreateTransaction_EmptyFields_ReturnsNullTransaction()
    {
        var (date, tx) = Transaction.CreateTransaction([], DefaultDate, null);

        tx.ShouldBeNull();
        date.ShouldBe(DefaultDate);
    }

    [TestMethod]
    public void CreateTransaction_NonDateNonAmountFields_PopulatesOtherFields()
    {
        var fields = new Dictionary<string, string>
        {
            ["Category"] = "Groceries",
            ["Memo"] = "Weekly shop",
            ["Amount"] = "75",
        };

        var (_, tx) = Transaction.CreateTransaction(fields, DefaultDate, null);

        tx.ShouldNotBeNull();
        tx!.OtherFields["Category"].ShouldBe("Groceries");
        tx.OtherFields["Memo"].ShouldBe("Weekly shop");
    }

    [TestMethod]
    public void CreateTransaction_AmountFieldIsCaseInsensitive()
    {
        var fields = new Dictionary<string, string> { ["amount"] = "200" };

        var (_, tx) = Transaction.CreateTransaction(fields, DefaultDate, null);

        tx.ShouldNotBeNull();
        tx!.Amount.ShouldBe(200m);
    }

    #endregion

    #region CreateTransaction — date handling

    [TestMethod]
    public void CreateTransaction_WithDateField_UsesDateInTransaction()
    {
        var fields = new Dictionary<string, string>
        {
            ["Date"] = "2024-06-15",
            ["Amount"] = "100",
        };

        var (returnedDate, tx) = Transaction.CreateTransaction(fields, DefaultDate, null);

        tx.ShouldNotBeNull();
        tx!.Date.ShouldBe(new DateOnly(2024, 6, 15));
        returnedDate.ShouldBe(new DateOnly(2024, 6, 15));
    }

    [TestMethod]
    public void CreateTransaction_MissingDateField_UsesDefaultDate()
    {
        var fields = new Dictionary<string, string> { ["Amount"] = "100" };

        var (returnedDate, tx) = Transaction.CreateTransaction(fields, DefaultDate, null);

        tx!.Date.ShouldBe(DefaultDate);
        returnedDate.ShouldBe(DefaultDate);
    }

    [TestMethod]
    public void CreateTransaction_EmptyDateField_UsesDefaultDate()
    {
        var fields = new Dictionary<string, string>
        {
            ["Date"] = "",
            ["Amount"] = "100",
        };

        var (_, tx) = Transaction.CreateTransaction(fields, DefaultDate, null);

        tx!.Date.ShouldBe(DefaultDate);
    }

    [TestMethod]
    public void CreateTransaction_WhitespaceDateField_UsesDefaultDate()
    {
        var fields = new Dictionary<string, string>
        {
            ["Date"] = "   ",
            ["Amount"] = "100",
        };

        var (_, tx) = Transaction.CreateTransaction(fields, DefaultDate, null);

        tx!.Date.ShouldBe(DefaultDate);
    }

    [TestMethod]
    public void CreateTransaction_InvalidDateField_ReturnsNullTransaction()
    {
        var fields = new Dictionary<string, string> { ["Date"] = "not-a-date" };

        var (date, tx) = Transaction.CreateTransaction(fields, DefaultDate, null);

        tx.ShouldBeNull();
        date.ShouldBe(DefaultDate);
    }

    [TestMethod]
    public void CreateTransaction_DateFieldIsCaseInsensitive()
    {
        var fields = new Dictionary<string, string>
        {
            ["date"] = "2024-03-20",
            ["Amount"] = "100",
        };

        var (_, tx) = Transaction.CreateTransaction(fields, DefaultDate, null);

        tx!.Date.ShouldBe(new DateOnly(2024, 3, 20));
    }

    #endregion

    #region CreateTransaction — selectors

    [TestMethod]
    public void CreateTransaction_ExpenseSelectorMatches_SetsGroupAndIsIncomeFalse()
    {
        var selectors = new TransactionSelectors();
        selectors.ExpensesGroupSelectors["expenses"] = [MakeGroupSelector("Category", "Groceries", "Food")];

        var fields = new Dictionary<string, string> { ["Category"] = "Groceries", ["Amount"] = "50" };

        var (_, tx) = Transaction.CreateTransaction(fields, DefaultDate, selectors);

        tx.ShouldNotBeNull();
        tx!.GroupName.ShouldBe("Food");
        tx.IsIncome.ShouldBeFalse();
    }

    [TestMethod]
    public void CreateTransaction_IncomeSelectorMatches_SetsGroupAndIsIncomeTrue()
    {
        var selectors = new TransactionSelectors();
        selectors.IncomeGroupSelectors["income"] = [MakeGroupSelector("Category", "Salary", "Wages")];

        var fields = new Dictionary<string, string> { ["Category"] = "Salary", ["Amount"] = "5000" };

        var (_, tx) = Transaction.CreateTransaction(fields, DefaultDate, selectors);

        tx.ShouldNotBeNull();
        tx!.GroupName.ShouldBe("Wages");
        tx.IsIncome.ShouldBeTrue();
    }

    [TestMethod]
    public void CreateTransaction_NoSelectorMatch_FallsBackToAmountSign()
    {
        var selectors = new TransactionSelectors();
        selectors.ExpensesGroupSelectors["expenses"] = [MakeGroupSelector("Category", "Groceries", "Food")];

        // Category doesn't match → no selector match → "Other" group, sign-based income
        var fields = new Dictionary<string, string> { ["Category"] = "Unknown", ["Amount"] = "100" };

        var (_, tx) = Transaction.CreateTransaction(fields, DefaultDate, selectors);

        tx.ShouldNotBeNull();
        tx!.GroupName.ShouldBe("Other");
        tx.IsIncome.ShouldBeTrue();
    }

    [TestMethod]
    public void CreateTransaction_NullSelectors_UsesAmountSignForClassification()
    {
        var fields = new Dictionary<string, string> { ["Amount"] = "-300" };

        var (_, tx) = Transaction.CreateTransaction(fields, DefaultDate, null);

        tx!.GroupName.ShouldBe("Other");
        tx.IsIncome.ShouldBeFalse();
    }

    #endregion

    #region CreateTransaction — filtering

    [TestMethod]
    public void CreateTransaction_TransactionIsFilteredOut_ReturnsNullTransaction()
    {
        var selectors = new TransactionSelectors();
        selectors.TransactionFilters["exclude"] = [MakeRegexFilter("Category", "Transfer")];

        var fields = new Dictionary<string, string> { ["Category"] = "Transfer", ["Amount"] = "1000" };

        var (date, tx) = Transaction.CreateTransaction(fields, DefaultDate, selectors);

        tx.ShouldBeNull();
        date.ShouldBe(DefaultDate);
    }

    [TestMethod]
    public void CreateTransaction_FilteredOutWithDateField_PropagatesDateInTuple()
    {
        var selectors = new TransactionSelectors();
        selectors.TransactionFilters["exclude"] = [MakeRegexFilter("Category", "Transfer")];

        var fields = new Dictionary<string, string>
        {
            ["Category"] = "Transfer",
            ["Date"] = "2024-09-20",
            ["Amount"] = "1000",
        };

        var (date, tx) = Transaction.CreateTransaction(fields, DefaultDate, selectors);

        tx.ShouldBeNull();
        date.ShouldBe(new DateOnly(2024, 9, 20));
    }

    [TestMethod]
    public void CreateTransaction_FilteredOutWithNoDateField_ReturnsDefaultDateInTuple()
    {
        var selectors = new TransactionSelectors();
        selectors.TransactionFilters["exclude"] = [MakeRegexFilter("Category", "Transfer")];

        var fields = new Dictionary<string, string> { ["Category"] = "Transfer", ["Amount"] = "1000" };

        var (date, tx) = Transaction.CreateTransaction(fields, DefaultDate, selectors);

        tx.ShouldBeNull();
        date.ShouldBe(DefaultDate);
    }

    #endregion
}
