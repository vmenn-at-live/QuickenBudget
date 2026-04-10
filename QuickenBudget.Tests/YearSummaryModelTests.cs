using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Shouldly;
using Moq;

using QuickenBudget.Interfaces;
using QuickenBudget.Models;
using QuickenBudget.Pages;

namespace QuickenBudget.Tests;

[TestClass]
public class YearSummaryModelTests : TestBase
{
    [TestMethod]
    public void OnGet_SetsYearAndGroupsAndIncomeAndTotalSpent()
    {
        // Arrange
        var mockTransactionData = new Mock<ITransactionData>();
        var year = 2023;
        var groups = new List<GroupTotals>
        {
            new(2023, "Food", -500, -41.67m, "#FF0000"),
            new(2023, "Transport", -300, -25m, "#FF0000")
        };
        var income = new GroupTotals (2023, "Income", 1000, 83.33m, "#60b33b");
        var expense = new GroupTotals (2023, "Expenses", -800, -66.67m, "#FF0000");
        mockTransactionData.Setup(td => td.GetExpensesGroups(year)).Returns(groups);
        mockTransactionData.Setup(td => td.GetIncomeGroups(year)).Returns([]);
        mockTransactionData.Setup(td => td.GetIncomeGroupsTotal(year)).Returns(income);
        mockTransactionData.Setup(td => td.GetExpensesGroupsTotal(year)).Returns(expense);
        var model = new YearSummaryModel(mockTransactionData.Object);

        // Act
        model.OnGet(year);

        // Assert
        model.Year.ShouldBe(year);
        model.ExpensesGroups.ShouldBe(groups);
        model.ExpensesTotal.ShouldBe(800m); // Sum of -(-500) + -(-300) = 500 + 300 = 800
        model.ExpensesAverage.ShouldBe(66.67m); // Sum of -(-41.67) + -(-25) = 41.67 + 25 = 66.67
        model.IncomeTotal.ShouldBe(1000m);
        model.IncomeAverage.ShouldBe(83.33m);
        mockTransactionData.Verify(td => td.GetExpensesGroups(year), Times.Once);
        mockTransactionData.Verify(td => td.GetIncomeGroupsTotal(year), Times.Once);
    }

    [TestMethod]
    public void Groups_IsInitializedAsEmptyList()
    {
        // Arrange
        var mockTransactionData = new Mock<ITransactionData>();
        var model = new YearSummaryModel(mockTransactionData.Object);

        // Act & Assert
        model.ExpensesGroups.ShouldNotBeNull();
        model.ExpensesGroups.ShouldBeEmpty();
    }

    [TestMethod]
    public void TotalSpent_IsCalculatedCorrectly()
    {
        // Arrange
        var mockTransactionData = new Mock<ITransactionData>();
        var year = 2023;
        List<GroupTotals> groups =
        [
            new (2023, "Food", -200, -20m, "#FF0000"),
            new (2023, "Transport", -100, -8.33m, "#FF0000")
        ];
        var income = new GroupTotals (2023, "Income", 500, 50m, "#60b33b");
        var expense = new GroupTotals (2023, "Expenses", -300, -25m, "#FF0000");
        mockTransactionData.Setup(td => td.GetExpensesGroups(year)).Returns(groups);
        mockTransactionData.Setup(td => td.GetIncomeGroups(year)).Returns([]);
        mockTransactionData.Setup(td => td.GetIncomeGroupsTotal(year)).Returns(income);
        mockTransactionData.Setup(td => td.GetExpensesGroupsTotal(year)).Returns(expense);
        var model = new YearSummaryModel(mockTransactionData.Object);

        // Act
        model.OnGet(year);

        // Assert
        model.ExpensesTotal.ShouldBe(300m); // -(-200) + -(-100) = 200 + 100 = 300
    }

    [TestMethod]
    public void AverageSpent_IsCalculatedCorrectly()
    {
        // Arrange
        var mockTransactionData = new Mock<ITransactionData>();
        var year = 2024;
        List<GroupTotals> groups =
        [
            new(2024, "Housing", -1200m, -100m, "#FF0000"),
            new(2024, "Utilities", -600m, -50m, "#FF0000")
        ];
        var income = new GroupTotals(2024, "Income", 3000m, 250m, "#60b33b");
        var expense = new GroupTotals(2024, "Expenses", -1800m, -150m, "#FF0000");
        mockTransactionData.Setup(td => td.GetExpensesGroups(year)).Returns(groups);
        mockTransactionData.Setup(td => td.GetIncomeGroups(year)).Returns([]);
        mockTransactionData.Setup(td => td.GetIncomeGroupsTotal(year)).Returns(income);
        mockTransactionData.Setup(td => td.GetExpensesGroupsTotal(year)).Returns(expense);
        var model = new YearSummaryModel(mockTransactionData.Object);

        // Act
        model.OnGet(year);

        // Assert
        model.ExpensesAverage.ShouldBe(150m); // -(-100) + -(-50) = 100 + 50
    }
}
