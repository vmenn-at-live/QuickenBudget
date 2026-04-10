using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;
using Serilog.Events;
using Serilog.Sinks.InMemory;
using Serilog.Sinks.InMemory.Assertions;
using Shouldly;

using QuickenBudget.Interfaces;
using QuickenBudget.Models;
using QuickenBudget.Services;

namespace QuickenBudget.Tests;

[TestClass]
public class WatcherTests : TestBase
{
    private Mock<IServiceProvider>? _mockServiceProvider;
    private Mock<ITransactionReader>? _mockReader;
    private Mock<IRecentLogBuffer>? _mockLogBuffer;
    private Mock<ITransactionReloadStatus>? _mockStatus;
    private TestOptionsMonitor<TransactionReaderSettings>? _readerSettings;
    private string? _tempDirectory;
    private Watcher? _watcher;
    private bool _watcherStopped;

    private static readonly Transaction SampleTransaction =
        new(new DateOnly(2024, 1, 1), 100m, "Income", true);

    public WatcherTests() : base(true, LogEventLevel.Information) { }

    [TestInitialize]
    public void Setup()
    {
        _mockStatus = new Mock<ITransactionReloadStatus>();
        _mockStatus.Setup(s => s.UpdateSnapshot(It.IsAny<Guid>(), It.IsAny<ITransactionData>())).Returns(true);

        _readerSettings = new TestOptionsMonitor<TransactionReaderSettings>(new TransactionReaderSettings());
        _tempDirectory = Directory.CreateTempSubdirectory("WatcherTests_").FullName;

        SetupMockServiceProvider([SampleTransaction]);
        _watcherStopped = false;
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (_watcher != null)
        {
            if (!_watcherStopped)
            {
                try
                {
                    await _watcher.StopAsync(CancellationToken.None);
                }
                catch (ObjectDisposedException)
                {
                    // Watcher was already disposed/stopped; nothing more to do.
                }
                catch (OperationCanceledException)
                {
                    // Stop was cancelled; treat as already stopped for test cleanup.
                }
            }
        }

        if (_tempDirectory != null && Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    private void SetupMockServiceProvider(List<Transaction>? transactions = null, Exception? readerException = null)
    {
        _mockReader = new Mock<ITransactionReader>();
        if (readerException != null)
            _mockReader.Setup(r => r.Ingest()).Throws(readerException);
        else
            _mockReader.Setup(r => r.Ingest()).Returns(transactions ?? []);

        _mockLogBuffer = new Mock<IRecentLogBuffer>();

        var mockScopeProvider = new Mock<IServiceProvider>();
        mockScopeProvider.Setup(sp => sp.GetService(typeof(ITransactionReader))).Returns(_mockReader.Object);
        mockScopeProvider.Setup(sp => sp.GetService(typeof(IRecentLogBuffer))).Returns(_mockLogBuffer.Object);

        var mockScope = new Mock<IServiceScope>();
        mockScope.Setup(s => s.ServiceProvider).Returns(mockScopeProvider.Object);

        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(IServiceScopeFactory))).Returns(mockScopeFactory.Object);

        // Return non-firing token sources so AddOptionsMonitor<T> succeeds without warning logs
        var mockSelectorsTokenSource = new Mock<IOptionsChangeTokenSource<TransactionSelectors>>();
        mockSelectorsTokenSource.Setup(s => s.GetChangeToken()).Returns(new CancellationChangeToken(CancellationToken.None));
        var mockSettingsTokenSource = new Mock<IOptionsChangeTokenSource<TransactionReaderSettings>>();
        mockSettingsTokenSource.Setup(s => s.GetChangeToken()).Returns(new CancellationChangeToken(CancellationToken.None));

        _mockServiceProvider.Setup(sp => sp.GetService(typeof(IOptionsChangeTokenSource<TransactionSelectors>))).Returns(mockSelectorsTokenSource.Object);
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(IOptionsChangeTokenSource<TransactionReaderSettings>))).Returns(mockSettingsTokenSource.Object);
    }

    private Watcher CreateWatcher()
    {
        _watcher = new Watcher(LogFactory, _mockServiceProvider!.Object, _mockStatus!.Object, _readerSettings!, TimeProvider.System);
        return _watcher;
    }

    /// <summary>
    /// Reconfigures <see cref="_mockStatus"/> to release the returned semaphore when either
    /// <see cref="ITransactionReloadStatus.UpdateSnapshot"/> or
    /// <see cref="ITransactionReloadStatus.LastReloadFailed"/> is first called.
    /// Use this to block until the Watcher's initial snapshot operation completes.
    /// </summary>
    private SemaphoreSlim WaitForInitialSnapshot()
    {
        var semaphore = new SemaphoreSlim(0, 1);
        int released = 0;
        void Release()
        {
            if (Interlocked.Exchange(ref released, 1) == 0)
                semaphore.Release();
        }

        _mockStatus!.Setup(s => s.UpdateSnapshot(It.IsAny<Guid>(), It.IsAny<ITransactionData>()))
            .Returns(true)
            .Callback<Guid, ITransactionData>((_, _) => Release());
        _mockStatus.Setup(s => s.LastReloadFailed(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>()))
            .Callback<Guid, DateTimeOffset>((_, _) => Release());
        return semaphore;
    }

    /// <summary>
    /// Reconfigures <see cref="_mockStatus"/> so that the returned semaphore is released when
    /// the Nth call to <see cref="ITransactionReloadStatus.UpdateSnapshot"/> occurs.
    /// </summary>
    private SemaphoreSlim WaitForNthUpdateSnapshot(int n)
    {
        var semaphore = new SemaphoreSlim(0, 1);
        int callCount = 0;
        _mockStatus!.Setup(s => s.UpdateSnapshot(It.IsAny<Guid>(), It.IsAny<ITransactionData>()))
            .Returns(true)
            .Callback<Guid, ITransactionData>((_, _) =>
            {
                if (Interlocked.Increment(ref callCount) == n)
                    semaphore.Release();
            });
        return semaphore;
    }

    #region ExecuteAsync — Startup

    [TestMethod]
    public async Task ExecuteAsync_OnStartup_IngestsTransactionsAndPublishesSnapshot()
    {
        var initialDone = WaitForInitialSnapshot();
        var watcher = CreateWatcher();
        await watcher.StartAsync(CancellationToken.None);
        (await initialDone.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBeTrue();

        _mockReader!.Verify(r => r.Ingest(), Times.Once);
        _mockStatus!.Verify(s => s.UpdateSnapshot(It.IsAny<Guid>(), It.IsAny<ITransactionData>()), Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_OnStartup_ClearsLogBuffer()
    {
        var initialDone = WaitForInitialSnapshot();
        var watcher = CreateWatcher();
        await watcher.StartAsync(CancellationToken.None);
        (await initialDone.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBeTrue();

        _mockLogBuffer!.Verify(b => b.Clear(), Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_OnStartup_LogsStartupMessage()
    {
        var initialDone = WaitForInitialSnapshot();
        var watcher = CreateWatcher();
        await watcher.StartAsync(CancellationToken.None);
        (await initialDone.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBeTrue();

        InMemorySink.Instance
            .Should()
            .HaveMessage("Watcher service is executing.")
            .ShouldMatchTestName()
            .WithLevel(LogEventLevel.Information);
    }

    #endregion

    #region ExecuteAsync — Error handling

    [TestMethod]
    public async Task ExecuteAsync_WhenIngestReturnsEmpty_CallsLastReloadFailed()
    {
        SetupMockServiceProvider(transactions: []);
        var initialDone = WaitForInitialSnapshot();
        var watcher = CreateWatcher();
        await watcher.StartAsync(CancellationToken.None);
        (await initialDone.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBeTrue();

        _mockStatus!.Verify(s => s.LastReloadFailed(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>()), Times.Once);
        _mockStatus.Verify(s => s.UpdateSnapshot(It.IsAny<Guid>(), It.IsAny<ITransactionData>()), Times.Never);
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenIngestThrows_CallsLastReloadFailed()
    {
        SetupMockServiceProvider(readerException: new IOException("File not found"));
        var initialDone = WaitForInitialSnapshot();
        var watcher = CreateWatcher();
        await watcher.StartAsync(CancellationToken.None);
        (await initialDone.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBeTrue();

        _mockStatus!.Verify(s => s.LastReloadFailed(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>()), Times.Once);
        _mockStatus.Verify(s => s.UpdateSnapshot(It.IsAny<Guid>(), It.IsAny<ITransactionData>()), Times.Never);
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenIngestThrowsParsingExceptionWithInner_LogsInnerException()
    {
        var innerException = new IOException("File read error");
        SetupMockServiceProvider(readerException: new ParsingException(innerException));
        var initialDone = WaitForInitialSnapshot();
        var watcher = CreateWatcher();
        await watcher.StartAsync(CancellationToken.None);
        (await initialDone.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBeTrue();

        // The inner IOException should be the logged exception, not the ParsingException wrapper
        var errorEvents = InMemorySink.Instance.LogEvents
            .SelectLogEventsForThisTest(LogEventLevel.Error)
            .Where(e => e.Exception is IOException)
            .ToList();

        errorEvents.Count.ShouldBe(1, "The inner exception should be logged, not the wrapping ParsingException");
    }

    #endregion

    #region SetupMonitor — First call

    [TestMethod]
    public async Task SetupMonitor_FirstCallWithFilePath_LogsFileSetupInfo()
    {
        string filePath = Path.Combine(_tempDirectory!, "settings.qif");
        _readerSettings!.Update(new TransactionReaderSettings { QuickenReportFile = filePath });
        var initialDone = WaitForInitialSnapshot();
        var watcher = CreateWatcher();
        await watcher.StartAsync(CancellationToken.None);
        (await initialDone.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBeTrue();
        await Task.Delay(200); // Allow SetupMonitor to run after UpdateSnapshot

        InMemorySink.Instance
            .Should()
            .HaveMessage("Setting up file monitor for the first time with file path: '{FilePath}'")
            .ShouldMatchTestName()
            .WithLevel(LogEventLevel.Information);
    }

    [TestMethod]
    public async Task SetupMonitor_FirstCallWithoutFilePath_LogsNoFileInfo()
    {
        // Default TransactionReaderSettings has an empty QuickenReportFile
        var initialDone = WaitForInitialSnapshot();
        var watcher = CreateWatcher();
        await watcher.StartAsync(CancellationToken.None);
        (await initialDone.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBeTrue();
        await Task.Delay(200);

        InMemorySink.Instance
            .Should()
            .HaveMessage("Setting up file monitor without an input file")
            .ShouldMatchTestName()
            .WithLevel(LogEventLevel.Information);
    }

    #endregion

    #region StopAsync

    [TestMethod]
    public async Task StopAsync_AfterStart_CompletesCleanly()
    {
        var initialDone = WaitForInitialSnapshot();
        var watcher = CreateWatcher();
        await watcher.StartAsync(CancellationToken.None);
        (await initialDone.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBeTrue();

        await Should.NotThrowAsync(async () => await watcher.StopAsync(CancellationToken.None));
        _watcherStopped = true; // Mark as stopped to prevent Cleanup from trying to stop again
    }

    [TestMethod]
    public async Task StopAsync_LogsStoppingMessage()
    {
        var initialDone = WaitForInitialSnapshot();
        var watcher = CreateWatcher();
        await watcher.StartAsync(CancellationToken.None);
        (await initialDone.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBeTrue();
        await watcher.StopAsync(CancellationToken.None);
        _watcherStopped = true;

        InMemorySink.Instance
            .Should()
            .HaveMessage("Watcher is stopping at: {time}")
            .ShouldMatchTestName()
            .WithLevel(LogEventLevel.Information);
    }

    #endregion

    #region Integration — File-triggered reload

    [TestMethod]
    public async Task ExecuteAsync_WhenFileChanges_ReloadsSnapshot()
    {
        string filePath = Path.Combine(_tempDirectory!, "data.qif");
        _readerSettings!.Update(new TransactionReaderSettings { QuickenReportFile = filePath });

        var reloadSemaphore = WaitForNthUpdateSnapshot(2);
        var watcher = CreateWatcher();
        await watcher.StartAsync(CancellationToken.None);

        // Wait for initial setup to complete before triggering file change
        await Task.Delay(500);

        // Create the file to trigger the reload signal
        File.WriteAllText(filePath, "content");

        bool signalReceived = await reloadSemaphore.WaitAsync(TimeSpan.FromSeconds(5));
        signalReceived.ShouldBeTrue("A second snapshot update should occur after the file is created");

        _mockStatus!.Verify(s => s.UpdateSnapshot(It.IsAny<Guid>(), It.IsAny<ITransactionData>()), Times.AtLeast(2));
    }

    [TestMethod]
    public async Task SetupMonitor_WhenFilePathChanges_LogsPathChange()
    {
        string file1 = Path.Combine(_tempDirectory!, "file1.qif");
        string file2 = Path.Combine(_tempDirectory!, "file2.qif");
        _readerSettings!.Update(new TransactionReaderSettings { QuickenReportFile = file1 });

        var reloadSemaphore = WaitForNthUpdateSnapshot(2);
        var watcher = CreateWatcher();
        await watcher.StartAsync(CancellationToken.None);

        // Wait for initial setup to complete (first SetupMonitor now watching file1)
        await Task.Delay(500);

        // Update settings to the new path before triggering the file change
        _readerSettings.Update(new TransactionReaderSettings { QuickenReportFile = file2 });

        // Create file1 to trigger the reload signal (file1 is still being watched)
        File.WriteAllText(file1, "content");

        bool signalReceived = await reloadSemaphore.WaitAsync(TimeSpan.FromSeconds(5));
        signalReceived.ShouldBeTrue("Reload should have been triggered by the file change");

        // Allow SetupMonitor to run after UpdateSnapshot
        await Task.Delay(300);

        var pathChangeLogs = InMemorySink.Instance.LogEvents
            .SelectLogEventsForThisTest(LogEventLevel.Information)
            .Where(e => e.MessageTemplate.Text == "Monitored file path changed from '{OldPath}' to '{NewPath}', updating file monitor...")
            .ToList();

        pathChangeLogs.Count.ShouldBe(1, "SetupMonitor should log the file path change exactly once");
    }

    [TestMethod]
    public async Task SetupMonitor_WhenFilePathChangesToEmpty_LogsPathRemoval()
    {
        string file1 = Path.Combine(_tempDirectory!, "file1.qif");
        _readerSettings!.Update(new TransactionReaderSettings { QuickenReportFile = file1 });

        var reloadSemaphore = WaitForNthUpdateSnapshot(2);
        var watcher = CreateWatcher();
        await watcher.StartAsync(CancellationToken.None);

        await Task.Delay(500);

        // Clear the file path before triggering the signal
        _readerSettings.Update(new TransactionReaderSettings { QuickenReportFile = string.Empty });

        File.WriteAllText(file1, "content");

        bool signalReceived = await reloadSemaphore.WaitAsync(TimeSpan.FromSeconds(5));
        signalReceived.ShouldBeTrue("Reload should have been triggered by the file change");

        await Task.Delay(300);

        var pathRemovedLogs = InMemorySink.Instance.LogEvents
            .SelectLogEventsForThisTest(LogEventLevel.Information)
            .Where(e => e.MessageTemplate.Text == "Monitored file path was '{OldPath}'; no file is being watched, updating file monitor...")
            .ToList();

        pathRemovedLogs.Count.ShouldBe(1, "SetupMonitor should log that no file is being watched");
    }

    #endregion

    #region Integration — Settings-triggered reload

    [TestMethod]
    public async Task ExecuteAsync_WhenSettingsChangeTokenFires_TriggersReload()
    {
        var settingsCts = new CancellationTokenSource();
        int tokenCallCount = 0;

        var mockSettingsTokenSource = new Mock<IOptionsChangeTokenSource<TransactionReaderSettings>>();
        mockSettingsTokenSource
            .Setup(s => s.GetChangeToken())
            .Returns(() => Interlocked.Increment(ref tokenCallCount) <= 1
                ? new CancellationChangeToken(settingsCts.Token)
                : new CancellationChangeToken(CancellationToken.None));

        // Override the default non-firing token source set up in Setup()
        _mockServiceProvider!
            .Setup(sp => sp.GetService(typeof(IOptionsChangeTokenSource<TransactionReaderSettings>)))
            .Returns(mockSettingsTokenSource.Object);

        var reloadSemaphore = WaitForNthUpdateSnapshot(2);
        var watcher = CreateWatcher();
        await watcher.StartAsync(CancellationToken.None);

        // Wait for initial snapshot + SetupMonitor to complete (SetupMonitor runs after UpdateSnapshot)
        await Task.Delay(500);

        // Fire the settings change token → OptionsFileMonitor debounces 300ms then signals Watcher
        await settingsCts.CancelAsync();

        (await reloadSemaphore.WaitAsync(TimeSpan.FromSeconds(5)))
            .ShouldBeTrue("A second snapshot update should occur after the settings change token fires");

        _mockStatus!.Verify(s => s.UpdateSnapshot(It.IsAny<Guid>(), It.IsAny<ITransactionData>()), Times.AtLeast(2));
    }

    #endregion
}
