/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using QuickenBudget.Models;

using Shouldly;

namespace QuickenBudget.Tests;

[TestClass]
public class TransactionFilterTests : TestBase
{
    #region Regex-only filtering (no amount operation)

    [TestMethod]
    public void IsFiltered_NoFilterAndNoAmountOp_ReturnsFalse()
    {
        var filter = new TransactionFilter();
        var fields = new Dictionary<string, string> { ["Amount"] = "100" };

        filter.IsFiltered(fields).ShouldBeFalse();
    }

    [TestMethod]
    public void IsFiltered_FilterMatchesAndAmountOpIsNone_ReturnsTrue()
    {
        var filter = new TransactionFilter { Field = "Category", Filter = "Groceries" };
        var fields = new Dictionary<string, string> { ["Category"] = "Groceries" };

        filter.IsFiltered(fields).ShouldBeTrue();
    }

    [TestMethod]
    public void IsFiltered_FilterDoesNotMatch_ReturnsFalse()
    {
        var filter = new TransactionFilter { Field = "Category", Filter = "Groceries" };
        var fields = new Dictionary<string, string> { ["Category"] = "Restaurant" };

        filter.IsFiltered(fields).ShouldBeFalse();
    }

    [TestMethod]
    public void IsFiltered_FieldMissingFromDictionary_ReturnsFalse()
    {
        var filter = new TransactionFilter { Field = "Category", Filter = "Groceries" };
        var fields = new Dictionary<string, string> { ["Memo"] = "Groceries" };

        filter.IsFiltered(fields).ShouldBeFalse();
    }

    [TestMethod]
    public void IsFiltered_TrimsFieldValueBeforeMatching_ReturnsTrue()
    {
        var filter = new TransactionFilter { Field = "Category", Filter = "Groceries" };
        var fields = new Dictionary<string, string> { ["Category"] = "  Groceries  " };

        filter.IsFiltered(fields).ShouldBeTrue();
    }

    [TestMethod]
    public void IsFiltered_NullAmountOperation_TreatedAsNone_ReturnsRegexResult()
    {
        var filter = new TransactionFilter { Field = "Category", Filter = "Groceries", AmountOperation = null };
        var fields = new Dictionary<string, string> { ["Category"] = "Groceries" };

        filter.IsFiltered(fields).ShouldBeTrue();
    }

    [TestMethod]
    public void IsFiltered_WhitespaceAmountOperation_TreatedAsNone_ReturnsRegexResult()
    {
        var filter = new TransactionFilter { Field = "Category", Filter = "Groceries", AmountOperation = "   " };
        var fields = new Dictionary<string, string> { ["Category"] = "Groceries" };

        filter.IsFiltered(fields).ShouldBeTrue();
    }

    #endregion

    #region Amount-only filtering (no regex filter)

    [TestMethod]
    public void IsFiltered_AmountEqualMatches_ReturnsTrue()
    {
        var filter = new TransactionFilter { AmountOperation = "Equal", Amount = 100m };
        var fields = new Dictionary<string, string> { ["Amount"] = "100" };

        filter.IsFiltered(fields).ShouldBeTrue();
    }

    [TestMethod]
    public void IsFiltered_AmountEqualDoesNotMatch_ReturnsFalse()
    {
        var filter = new TransactionFilter { AmountOperation = "Equal", Amount = 100m };
        var fields = new Dictionary<string, string> { ["Amount"] = "50" };

        filter.IsFiltered(fields).ShouldBeFalse();
    }

    [TestMethod]
    public void IsFiltered_AmountLessTransactionIsLess_ReturnsTrue()
    {
        // filter.Amount > Math.Abs(transactionAmount): threshold 100 > 50 ✓
        var filter = new TransactionFilter { AmountOperation = "Less", Amount = 100m };
        var fields = new Dictionary<string, string> { ["Amount"] = "50" };

        filter.IsFiltered(fields).ShouldBeTrue();
    }

    [TestMethod]
    public void IsFiltered_AmountLessTransactionIsGreater_ReturnsFalse()
    {
        // filter.Amount > Math.Abs(transactionAmount): threshold 50 > 100 ✗
        var filter = new TransactionFilter { AmountOperation = "Less", Amount = 50m };
        var fields = new Dictionary<string, string> { ["Amount"] = "100" };

        filter.IsFiltered(fields).ShouldBeFalse();
    }

    [TestMethod]
    public void IsFiltered_AmountGreaterTransactionIsGreater_ReturnsTrue()
    {
        // filter.Amount < Math.Abs(transactionAmount): threshold 50 < 100 ✓
        var filter = new TransactionFilter { AmountOperation = "Greater", Amount = 50m };
        var fields = new Dictionary<string, string> { ["Amount"] = "100" };

        filter.IsFiltered(fields).ShouldBeTrue();
    }

    [TestMethod]
    public void IsFiltered_AmountGreaterTransactionIsLess_ReturnsFalse()
    {
        // filter.Amount < Math.Abs(transactionAmount): threshold 100 < 50 ✗
        var filter = new TransactionFilter { AmountOperation = "Greater", Amount = 100m };
        var fields = new Dictionary<string, string> { ["Amount"] = "50" };

        filter.IsFiltered(fields).ShouldBeFalse();
    }

    [TestMethod]
    public void IsFiltered_NegativeTransactionAmountUsesAbsoluteValue_ReturnsTrue()
    {
        var filter = new TransactionFilter { AmountOperation = "Equal", Amount = 100m };
        var fields = new Dictionary<string, string> { ["Amount"] = "-100" };

        filter.IsFiltered(fields).ShouldBeTrue();
    }

    [TestMethod]
    public void IsFiltered_MissingAmountField_ReturnsFalse()
    {
        var filter = new TransactionFilter { AmountOperation = "Equal", Amount = 100m };
        var fields = new Dictionary<string, string> { ["Category"] = "Food" };

        filter.IsFiltered(fields).ShouldBeFalse();
    }

    [TestMethod]
    public void IsFiltered_InvalidAmountString_ReturnsFalse()
    {
        var filter = new TransactionFilter { AmountOperation = "Equal", Amount = 100m };
        var fields = new Dictionary<string, string> { ["Amount"] = "not-a-number" };

        filter.IsFiltered(fields).ShouldBeFalse();
    }

    [TestMethod]
    public void IsFiltered_UnrecognizedAmountOperation_ReturnsFalse()
    {
        var filter = new TransactionFilter { AmountOperation = "Between" };
        var fields = new Dictionary<string, string> { ["Amount"] = "100" };

        filter.IsFiltered(fields).ShouldBeFalse();
    }

    #endregion

    #region Combined regex + amount filtering

    [TestMethod]
    public void IsFiltered_FilterMatchesAndAmountMatches_ReturnsTrue()
    {
        var filter = new TransactionFilter { Field = "Category", Filter = "Groceries", AmountOperation = "Equal", Amount = 50m };
        var fields = new Dictionary<string, string> { ["Category"] = "Groceries", ["Amount"] = "50" };

        filter.IsFiltered(fields).ShouldBeTrue();
    }

    [TestMethod]
    public void IsFiltered_FilterDoesNotMatchButAmountMatches_ReturnsFalse()
    {
        var filter = new TransactionFilter { Field = "Category", Filter = "Groceries", AmountOperation = "Equal", Amount = 50m };
        var fields = new Dictionary<string, string> { ["Category"] = "Restaurant", ["Amount"] = "50" };

        // Regex miss short-circuits the amount check.
        filter.IsFiltered(fields).ShouldBeFalse();
    }

    [TestMethod]
    public void IsFiltered_FilterMatchesButAmountDoesNotMatch_ReturnsFalse()
    {
        var filter = new TransactionFilter { Field = "Category", Filter = "Groceries", AmountOperation = "Equal", Amount = 50m };
        var fields = new Dictionary<string, string> { ["Category"] = "Groceries", ["Amount"] = "100" };

        filter.IsFiltered(fields).ShouldBeFalse();
    }

    #endregion
}
