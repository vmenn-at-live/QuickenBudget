using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using QuickenBudget.Interfaces;
using QuickenBudget.Models;
using QuickenBudget.Services;

using Shouldly;

using System;
using System.Collections.Generic;


namespace QuickenBudget.Tests;

[TestClass]
public class TransactionReloadStatusTests : TestBase
{
    private ILogger<TransactionReloadStatus>? _logger;
    private IRecentLogBuffer? _buffer;
    private readonly Guid _defaultScopeId = Guid.NewGuid();

    [TestInitialize]
    public void Setup()
    {
        _logger = CreateTestLogger<TransactionReloadStatus>();
        _buffer = new RecentLogBuffer();
    }

    /// <summary>
    /// Verifies that the initial status is success and that consuming it leaves it at success.
    /// </summary>
    [TestMethod]
    public void GetStatusSince_WithoutChanges_ReturnsSuccess()
    {
        var status = new TransactionReloadStatus(_buffer!);
        DateTimeOffset pageRefreshTime = DateTimeOffset.UtcNow;

        status.GetStatusSince(pageRefreshTime).ShouldBe("success");
        status.GetStatusSince(pageRefreshTime).ShouldBe("success");
    }

    /// <summary>
    /// Verifies that a successful reload after a page refresh is reported as reload.
    /// </summary>
    [TestMethod]
    public void GetStatusSince_AfterReload_ReturnsReload()
    {
        DateTimeOffset pageRefreshTime = DateTimeOffset.UtcNow;

        var mockSnapshot = new Mock<ITransactionData>();
        mockSnapshot.Setup(td => td.CreationTime).Returns(pageRefreshTime.AddMinutes(1));

        var status = new TransactionReloadStatus(_buffer!);
        status.UpdateSnapshot(_defaultScopeId, mockSnapshot.Object);

        status.GetStatusSince(pageRefreshTime).ShouldBe("reload");
        status.GetStatusSince(pageRefreshTime.AddMinutes(2)).ShouldBe("success");
    }

    /// <summary>
    /// Verifies that the latest event since a page refresh determines the reported status.
    /// </summary>
    [TestMethod]
    public void GetStatusSince_UsesLatestEventAfterRefresh()
    {
        DateTimeOffset currentDate = DateTimeOffset.UtcNow;
        DateTimeOffset pageRefreshTime = currentDate;


        var mockSnapshot = new Mock<ITransactionData>();
        List<Transaction> transactions = [new (new DateOnly(2023, 1, 1), 1000, "Income", true)];
        mockSnapshot.Setup(td => td.Transactions).Returns(transactions);
        mockSnapshot.Setup(td => td.AllYears).Returns([2023]);
        mockSnapshot.SetupSequence(td => td.CreationTime)
            .Returns(currentDate)
            .Returns(currentDate)
            .Returns(currentDate.AddMinutes(2));

        var status = new TransactionReloadStatus(_buffer!);
        status.LastReloadFailed(_defaultScopeId, pageRefreshTime.AddMinutes(1));
        status.GetStatusSince(pageRefreshTime.AddSeconds(30)).ShouldBe("errors");

        status.UpdateSnapshot(_defaultScopeId, mockSnapshot.Object);
        status.GetStatusSince(pageRefreshTime).ShouldBe("reload");
    }
}
