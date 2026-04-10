using Microsoft.VisualStudio.TestTools.UnitTesting;

using QuickenBudget.Interfaces;
using QuickenBudget.Services;

using Serilog.Core;
using Serilog.Events;

using Shouldly;

using System;
using System.Linq;

namespace QuickenBudget.Tests;

[TestClass]
public class RecentLogBufferTests: TestBase
{
    [TestMethod]
    public void Add_GetMessages_ReturnsBufferedMessages()
    {
        var buffer = new RecentLogBuffer();

        Guid scopeId = Guid.NewGuid();
        buffer.Add(scopeId, "warning one");
        buffer.Add(scopeId, "error two");

        buffer.GetMessages(scopeId).ShouldBe(["warning one", "error two"]);
    }

    [TestMethod]
    public void Clear_RemovesBufferedMessages()
    {
        var buffer = new RecentLogBuffer();

        Guid scopeId = Guid.NewGuid();
        buffer.Add(scopeId, "warning one");

        buffer.Clear();

        buffer.GetMessages(scopeId).ShouldBeEmpty();
    }

    [TestMethod]
    public void Sink_Emit_WritesRenderedMessagesToBuffer()
    {
        var buffer = new RecentLogBuffer();
        var sink = new TransactionReloadStatus(buffer);

        Guid scopeId = Guid.NewGuid();

        sink.Emit(new LogEvent(
            timestamp: System.DateTimeOffset.UtcNow,
            level: LogEventLevel.Warning,
            exception: null,
            messageTemplate: new Serilog.Parsing.MessageTemplateParser().Parse("Problem with {Thing}"),
            properties: [new LogEventProperty("Thing", new ScalarValue("reload")), new LogEventProperty("SnapshotId", new ScalarValue(scopeId))]));

        string message = buffer.GetMessages(scopeId).Single();
        message.ShouldContain("Problem with");
        message.ShouldContain("reload");
    }
}
