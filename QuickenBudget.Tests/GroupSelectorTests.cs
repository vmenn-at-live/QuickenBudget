/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using QuickenBudget.Models;

using Shouldly;

namespace QuickenBudget.Tests;

[TestClass]
public class GroupSelectorTests : TestBase
{
    private static GroupSelector MakeSelector(string field, string filter, string targetGroup) =>
        new() { Field = field, Filter = filter, TargetGroup = targetGroup };

    #region SelectGroup — matching

    [TestMethod]
    public void SelectGroup_MatchingFieldAndFilter_ReturnsTargetGroup()
    {
        var selector = MakeSelector("Category", "Groceries", "Food");
        var fields = new Dictionary<string, string> { ["Category"] = "Groceries" };

        selector.SelectGroup(fields).ShouldBe("Food");
    }

    [TestMethod]
    public void SelectGroup_FilterIsRegex_ReturnsTargetGroupOnPartialMatch()
    {
        var selector = MakeSelector("Category", "Groc.*", "Food");
        var fields = new Dictionary<string, string> { ["Category"] = "Groceries" };

        selector.SelectGroup(fields).ShouldBe("Food");
    }

    [TestMethod]
    public void SelectGroup_TrimsFieldValueBeforeMatching()
    {
        var selector = MakeSelector("Category", "Groceries", "Food");
        var fields = new Dictionary<string, string> { ["Category"] = "  Groceries  " };

        selector.SelectGroup(fields).ShouldBe("Food");
    }

    #endregion

    #region SelectGroup — non-matching / invalid

    [TestMethod]
    public void SelectGroup_FilterDoesNotMatch_ReturnsNull()
    {
        var selector = MakeSelector("Category", "Groceries", "Food");
        var fields = new Dictionary<string, string> { ["Category"] = "Restaurant" };

        selector.SelectGroup(fields).ShouldBeNull();
    }

    [TestMethod]
    public void SelectGroup_FieldMissingFromDictionary_ReturnsNull()
    {
        var selector = MakeSelector("Category", "Groceries", "Food");
        var fields = new Dictionary<string, string> { ["Memo"] = "Groceries" };

        selector.SelectGroup(fields).ShouldBeNull();
    }

    [TestMethod]
    public void SelectGroup_FieldValueIsWhitespace_ReturnsNull()
    {
        var selector = MakeSelector("Category", ".*", "Food");
        var fields = new Dictionary<string, string> { ["Category"] = "   " };

        selector.SelectGroup(fields).ShouldBeNull();
    }

    [TestMethod]
    public void SelectGroup_NullFilter_ReturnsNull()
    {
        var selector = new GroupSelector { Field = "Category", Filter = null, TargetGroup = "Food" };
        var fields = new Dictionary<string, string> { ["Category"] = "Groceries" };

        selector.SelectGroup(fields).ShouldBeNull();
    }

    [TestMethod]
    public void SelectGroup_EmptyFilter_ReturnsNull()
    {
        var selector = new GroupSelector { Field = "Category", Filter = "", TargetGroup = "Food" };
        var fields = new Dictionary<string, string> { ["Category"] = "Groceries" };

        selector.SelectGroup(fields).ShouldBeNull();
    }

    [TestMethod]
    public void SelectGroup_NullField_ReturnsNull()
    {
        var selector = new GroupSelector { Field = null, Filter = "Groceries", TargetGroup = "Food" };
        var fields = new Dictionary<string, string> { ["Category"] = "Groceries" };

        selector.SelectGroup(fields).ShouldBeNull();
    }

    [TestMethod]
    public void SelectGroup_EmptyTargetGroup_ReturnsNull()
    {
        var selector = new GroupSelector { Field = "Category", Filter = "Groceries", TargetGroup = "" };
        var fields = new Dictionary<string, string> { ["Category"] = "Groceries" };

        selector.SelectGroup(fields).ShouldBeNull();
    }

    [TestMethod]
    public void SelectGroup_WhitespaceTargetGroup_ReturnsNull()
    {
        var selector = new GroupSelector { Field = "Category", Filter = "Groceries", TargetGroup = "   " };
        var fields = new Dictionary<string, string> { ["Category"] = "Groceries" };

        selector.SelectGroup(fields).ShouldBeNull();
    }

    #endregion
}
