using System;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Serilog;
using Serilog.Context;
using Serilog.Events;
using Serilog.Sinks.InMemory;

namespace QuickenBudget.Tests;

public class TestBase(bool useMemorySync = false, LogEventLevel inMemoryLevel = LogEventLevel.Warning)
{
    public ILoggerFactory LogFactory { get; } = CreateLoggerFactory(useMemorySync, inMemoryLevel);

    // Hopefully set by the test framework
    public TestContext? TestContext { get; set; }

    private IDisposable? _loggingContext;

    private static ILoggerFactory CreateLoggerFactory(bool useMemorySync = false, LogEventLevel inMemoryLevel = LogEventLevel.Warning)
    {
        // Add in-memory sink if requested.
        LoggerConfiguration config = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Verbose()
            .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {TestName} - {Message:lj}{NewLine}{Exception}");

        if (useMemorySync)
        {
            config.WriteTo.InMemory(restrictedToMinimumLevel: inMemoryLevel);
        }
        Log.Logger = config.CreateLogger();
        return new LoggerFactory().AddSerilog(Log.Logger);
    }

    public ILogger<T> CreateTestLogger<T>() => LogFactory.CreateLogger<T>();

    /// <summary>
    /// Initializes the test environment before each test.
    /// </summary>
    [TestInitialize]
    public void TestBaseSetup()
    {
        _loggingContext = LogContext.PushProperty("TestName", TestContext?.TestName ?? "UnknownTest");
    }

    /// <summary>
    /// `Cleanup` method to dispose the logger context.
    /// </summary>
    [TestCleanup]
    public void TestBaseCleanup()
    {
        _loggingContext?.Dispose();
        _loggingContext = null;
    }
}
