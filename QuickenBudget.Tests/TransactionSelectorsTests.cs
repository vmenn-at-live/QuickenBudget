/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using QuickenBudget.Models;

using Shouldly;

namespace QuickenBudget.Tests;

[TestClass]
public class TransactionSelectorsTests : TestBase
{
    private static GroupSelector MakeGroupSelector(string field, string filter, string targetGroup) =>
        new() { Field = field, Filter = filter, TargetGroup = targetGroup };

    private static TransactionFilter MakeRegexFilter(string field, string filter) =>
        new() { Field = field, Filter = filter };

    #region IsFilteredOut

    [TestMethod]
    public void IsFilteredOut_EmptyFilters_ReturnsFalse()
    {
        var selectors = new TransactionSelectors();
        var fields = new Dictionary<string, string> { ["Category"] = "Transfer" };

        selectors.IsFilteredOut(fields).ShouldBeFalse();
    }

    [TestMethod]
    public void IsFilteredOut_SingleGroupWithMatchingFilter_ReturnsTrue()
    {
        var selectors = new TransactionSelectors();
        selectors.TransactionFilters["transfers"] = [MakeRegexFilter("Category", "Transfer")];

        var fields = new Dictionary<string, string> { ["Category"] = "Transfer" };

        selectors.IsFilteredOut(fields).ShouldBeTrue();
    }

    [TestMethod]
    public void IsFilteredOut_SingleGroupWithNoMatch_ReturnsFalse()
    {
        var selectors = new TransactionSelectors();
        selectors.TransactionFilters["transfers"] = [MakeRegexFilter("Category", "Transfer")];

        var fields = new Dictionary<string, string> { ["Category"] = "Groceries" };

        selectors.IsFilteredOut(fields).ShouldBeFalse();
    }

    [TestMethod]
    public void IsFilteredOut_MultipleGroups_AnyMatchReturnsTrue()
    {
        var selectors = new TransactionSelectors();
        selectors.TransactionFilters["transfers"] = [MakeRegexFilter("Category", "Transfer")];
        selectors.TransactionFilters["fees"] = [MakeRegexFilter("Category", "Fee")];

        // "Transfer" matches the first group only, but both groups are checked and it's found.
        var fields = new Dictionary<string, string> { ["Category"] = "Transfer" };

        selectors.IsFilteredOut(fields).ShouldBeTrue();
    }

    [TestMethod]
    public void IsFilteredOut_MultipleGroups_NoneMatchReturnsFalse()
    {
        var selectors = new TransactionSelectors();
        selectors.TransactionFilters["transfers"] = [MakeRegexFilter("Category", "Transfer")];
        selectors.TransactionFilters["fees"] = [MakeRegexFilter("Category", "Fee")];

        var fields = new Dictionary<string, string> { ["Category"] = "Groceries" };

        selectors.IsFilteredOut(fields).ShouldBeFalse();
    }

    [TestMethod]
    public void IsFilteredOut_MultipleFiltersInGroup_AnyFilterMatchReturnsTrue()
    {
        var selectors = new TransactionSelectors();
        selectors.TransactionFilters["exclude"] = [
            MakeRegexFilter("Category", "Transfer"),
            MakeRegexFilter("Category", "Fee"),
        ];

        var fields = new Dictionary<string, string> { ["Category"] = "Fee" };

        selectors.IsFilteredOut(fields).ShouldBeTrue();
    }

    [TestMethod]
    public void IsFilteredOut_ReverseEvaluation_LastGroupCheckedFirst()
    {
        // Add two filter groups; only the first-declared (index 0) has a matching filter.
        // Reverse iteration checks index 1 first (no match), then index 0 (match) → true.
        var selectors = new TransactionSelectors();
        selectors.TransactionFilters["first"] = [MakeRegexFilter("Category", "Transfer")];
        selectors.TransactionFilters["last"] = [MakeRegexFilter("Category", "Fee")];

        var fields = new Dictionary<string, string> { ["Category"] = "Transfer" };

        selectors.IsFilteredOut(fields).ShouldBeTrue();
    }

    #endregion

    #region SelectGroup

    [TestMethod]
    public void SelectGroup_EmptySelectors_ReturnsIsIncomeTrueAndNullGroup()
    {
        var selectors = new TransactionSelectors();
        var fields = new Dictionary<string, string> { ["Category"] = "Groceries" };

        var (isIncome, group) = selectors.SelectGroup(fields);

        isIncome.ShouldBeTrue();
        group.ShouldBeNull();
    }

    [TestMethod]
    public void SelectGroup_MatchesExpenseSelector_ReturnsIsIncomeFalseAndGroupName()
    {
        var selectors = new TransactionSelectors();
        selectors.ExpensesGroupSelectors["expenses"] = [MakeGroupSelector("Category", "Groceries", "Food")];

        var fields = new Dictionary<string, string> { ["Category"] = "Groceries" };

        var (isIncome, group) = selectors.SelectGroup(fields);

        isIncome.ShouldBeFalse();
        group.ShouldBe("Food");
    }

    [TestMethod]
    public void SelectGroup_MatchesIncomeSelector_ReturnsIsIncomeTrueAndGroupName()
    {
        var selectors = new TransactionSelectors();
        selectors.IncomeGroupSelectors["income"] = [MakeGroupSelector("Category", "Salary", "Wages")];

        var fields = new Dictionary<string, string> { ["Category"] = "Salary" };

        var (isIncome, group) = selectors.SelectGroup(fields);

        isIncome.ShouldBeTrue();
        group.ShouldBe("Wages");
    }

    [TestMethod]
    public void SelectGroup_MatchesBothExpenseAndIncome_PrefersExpense()
    {
        var selectors = new TransactionSelectors();
        selectors.ExpensesGroupSelectors["expenses"] = [MakeGroupSelector("Category", "Groceries", "FoodExpense")];
        selectors.IncomeGroupSelectors["income"] = [MakeGroupSelector("Category", "Groceries", "FoodIncome")];

        var fields = new Dictionary<string, string> { ["Category"] = "Groceries" };

        var (isIncome, group) = selectors.SelectGroup(fields);

        isIncome.ShouldBeFalse();
        group.ShouldBe("FoodExpense");
    }

    [TestMethod]
    public void SelectGroup_NoMatch_ReturnsIsIncomeTrueAndNullGroup()
    {
        var selectors = new TransactionSelectors();
        selectors.ExpensesGroupSelectors["expenses"] = [MakeGroupSelector("Category", "Groceries", "Food")];
        selectors.IncomeGroupSelectors["income"] = [MakeGroupSelector("Category", "Salary", "Wages")];

        var fields = new Dictionary<string, string> { ["Category"] = "Unknown" };

        var (isIncome, group) = selectors.SelectGroup(fields);

        isIncome.ShouldBeTrue();
        group.ShouldBeNull();
    }

    [TestMethod]
    public void SelectGroup_ExpensesReversedArrayOrder_LastDeclaredGroupWins()
    {
        // Add two expense groups. Both declare a selector for the same field.
        // The last-declared group (index 1) is checked first in reverse order.
        var selectors = new TransactionSelectors();
        selectors.ExpensesGroupSelectors["first"] = [MakeGroupSelector("Category", "Groceries", "EarlyFood")];
        selectors.ExpensesGroupSelectors["last"] = [MakeGroupSelector("Category", "Groceries", "LateFood")];

        var fields = new Dictionary<string, string> { ["Category"] = "Groceries" };

        var (_, group) = selectors.SelectGroup(fields);

        group.ShouldBe("LateFood");
    }

    #endregion
}
