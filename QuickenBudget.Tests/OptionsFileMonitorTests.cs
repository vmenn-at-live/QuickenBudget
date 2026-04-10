using System;
using System.IO;
using System.Linq;
using System.Threading;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;
using Serilog.Events;
using Serilog.Sinks.InMemory;
using Serilog.Sinks.InMemory.Assertions;
using Shouldly;

using QuickenBudget.Services;

namespace QuickenBudget.Tests;

[TestClass]
public class OptionsFileMonitorTests : TestBase
{
    private ILogger<OptionsFileMonitor>? _logger;
    private Mock<IServiceProvider>? _mockServiceProvider;
    private string? _tempDirectory;
    private OptionsFileMonitor? _monitor;

    public class TestOptions { }

    public OptionsFileMonitorTests() : base(true, LogEventLevel.Warning)
    {
    }

    [TestInitialize]
    public void Setup()
    {
        _logger = CreateTestLogger<OptionsFileMonitor>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _tempDirectory = Directory.CreateTempSubdirectory("OptionsFileMonitorTests_").FullName;
        _monitor = new OptionsFileMonitor(_logger, _mockServiceProvider.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _monitor?.Dispose();
        if (_tempDirectory != null && Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    #region AddFilePath

    [TestMethod]
    public void AddFilePath_ValidPathInExistingDirectory_DoesNotLogWarning()
    {
        string filePath = Path.Combine(_tempDirectory!, "test.json");

        _monitor!.AddFilePath(filePath);

        var warnings = InMemorySink.Instance.LogEvents
            .SelectLogEventsForThisTest(LogEventLevel.Warning)
            .Where(e => e.MessageTemplate.Text == "File path {FilePath} cannot be processed. The path will not be monitored.")
            .ToList();
        warnings.ShouldBeEmpty();
    }

    [TestMethod]
    public void AddFilePath_ValidPath_EnablesOnChangeWithoutThrowing()
    {
        string filePath = Path.Combine(_tempDirectory!, "test.json");
        _monitor!.AddFilePath(filePath);

        Should.NotThrow(() => _monitor.OnChange(() => { }));
    }

    [TestMethod]
    public void AddFilePath_NullPath_LogsWarning()
    {
        _monitor!.AddFilePath(null!);

        InMemorySink.Instance
            .Should()
            .HaveMessage("File path {FilePath} cannot be processed. The path will not be monitored.")
            .ShouldMatchTestName()
            .WithLevel(LogEventLevel.Warning);
    }

    [TestMethod]
    public void AddFilePath_EmptyPath_LogsWarning()
    {
        _monitor!.AddFilePath(string.Empty);

        InMemorySink.Instance
            .Should()
            .HaveMessage("File path {FilePath} cannot be processed. The path will not be monitored.")
            .ShouldMatchTestName()
            .WithLevel(LogEventLevel.Warning);
    }

    [TestMethod]
    public void AddFilePath_WhitespacePath_LogsWarning()
    {
        _monitor!.AddFilePath("   ");

        InMemorySink.Instance
            .Should()
            .HaveMessage("File path {FilePath} cannot be processed. The path will not be monitored.")
            .ShouldMatchTestName()
            .WithLevel(LogEventLevel.Warning);
    }

    [TestMethod]
    public void AddFilePath_DirectoryDoesNotExist_LogsWarning()
    {
        string nonExistentDir = Path.Combine(_tempDirectory!, "does_not_exist", "file.json");

        _monitor!.AddFilePath(nonExistentDir);

        InMemorySink.Instance
            .Should()
            .HaveMessage("File path {FilePath} cannot be processed. The path will not be monitored.")
            .ShouldMatchTestName()
            .WithLevel(LogEventLevel.Warning);
    }

    [TestMethod]
    public void AddFilePath_AfterDispose_ThrowsObjectDisposedException()
    {
        _monitor!.Dispose();

        Should.Throw<ObjectDisposedException>(() => _monitor.AddFilePath(Path.Combine(_tempDirectory!, "test.json")));
    }

    [TestMethod]
    public void AddFilePath_PathWithDotSegments_IsNormalizedAndAccepted()
    {
        // Path.Combine preserves ".." segments; AddFilePath calls Path.GetFullPath internally to normalize
        string pathWithDots = Path.Combine(_tempDirectory!, "subdir", "..", "test.json");

        _monitor!.AddFilePath(pathWithDots);

        var warnings = InMemorySink.Instance.LogEvents
            .SelectLogEventsForThisTest(LogEventLevel.Warning)
            .Where(e => e.MessageTemplate.Text == "File path {FilePath} cannot be processed. The path will not be monitored.")
            .ToList();
        warnings.ShouldBeEmpty("Path with '..' segments should resolve to _tempDirectory\\test.json and be accepted");
    }

    #endregion

    #region AddOptionsMonitor

    [TestMethod]
    public void AddOptionsMonitor_ServiceNotRegistered_LogsWarning()
    {
        _mockServiceProvider!
            .Setup(sp => sp.GetService(typeof(IOptionsChangeTokenSource<TestOptions>)))
            .Returns((object?)null);

        _monitor!.AddOptionsMonitor<TestOptions>();

        InMemorySink.Instance
            .Should()
            .HaveMessage("The monitor for {Type} is not registered. The type will not be monitored.")
            .ShouldMatchTestName()
            .WithLevel(LogEventLevel.Warning);
    }

    [TestMethod]
    public void AddOptionsMonitor_ServiceRegistered_CallsGetChangeTokenOnOnChange()
    {
        var mockTokenSource = new Mock<IOptionsChangeTokenSource<TestOptions>>();
        mockTokenSource
            .Setup(s => s.GetChangeToken())
            .Returns(new Microsoft.Extensions.Primitives.CancellationChangeToken(CancellationToken.None));
        _mockServiceProvider!
            .Setup(sp => sp.GetService(typeof(IOptionsChangeTokenSource<TestOptions>)))
            .Returns(mockTokenSource.Object);

        _monitor!.AddOptionsMonitor<TestOptions>();
        _monitor.OnChange(() => { });

        mockTokenSource.Verify(s => s.GetChangeToken(), Times.Once);
    }

    [TestMethod]
    public void AddOptionsMonitor_ServiceRegistered_DoesNotLogWarning()
    {
        var mockTokenSource = new Mock<IOptionsChangeTokenSource<TestOptions>>();
        mockTokenSource
            .Setup(s => s.GetChangeToken())
            .Returns(new Microsoft.Extensions.Primitives.CancellationChangeToken(CancellationToken.None));
        _mockServiceProvider!
            .Setup(sp => sp.GetService(typeof(IOptionsChangeTokenSource<TestOptions>)))
            .Returns(mockTokenSource.Object);

        _monitor!.AddOptionsMonitor<TestOptions>();

        var warnings = InMemorySink.Instance.LogEvents
            .SelectLogEventsForThisTest(LogEventLevel.Warning)
            .Where(e => e.MessageTemplate.Text == "The monitor for {Type} is not registered. The type will not be monitored.")
            .ToList();
        warnings.ShouldBeEmpty();
    }

    [TestMethod]
    public void AddOptionsMonitor_AfterDispose_ThrowsObjectDisposedException()
    {
        _monitor!.Dispose();

        Should.Throw<ObjectDisposedException>(() => _monitor.AddOptionsMonitor<TestOptions>());
    }

    #endregion

    #region RemoveFilePath

    [TestMethod]
    public void RemoveFilePath_AfterAdding_DoesNotThrow()
    {
        string filePath = Path.Combine(_tempDirectory!, "test.json");
        _monitor!.AddFilePath(filePath);

        Should.NotThrow(() => _monitor.RemoveFilePath(filePath));
    }

    [TestMethod]
    public void RemoveFilePath_NonExistentPath_DoesNotThrow()
    {
        Should.NotThrow(() => _monitor!.RemoveFilePath(Path.Combine(_tempDirectory!, "not_added.json")));
    }

    [TestMethod]
    public void RemoveFilePath_AfterDispose_DoesNotThrow()
    {
        _monitor!.Dispose();

        Should.NotThrow(() => _monitor.RemoveFilePath(Path.Combine(_tempDirectory!, "test.json")));
    }

    [TestMethod]
    public void RemoveFilePath_AfterAdding_PreventsFileWatcherFromBeingCreated()
    {
        string filePath = Path.Combine(_tempDirectory!, "test.json");
        _monitor!.AddFilePath(filePath);
        _monitor.RemoveFilePath(filePath);

        // OnChange should still work, just won't be watching the removed file
        Should.NotThrow(() => _monitor.OnChange(() => { }));
    }

    #endregion

    #region RemoveOptionsMonitor

    [TestMethod]
    public void RemoveOptionsMonitor_AfterAdding_DoesNotThrow()
    {
        var mockTokenSource = new Mock<IOptionsChangeTokenSource<TestOptions>>();
        mockTokenSource
            .Setup(s => s.GetChangeToken())
            .Returns(new Microsoft.Extensions.Primitives.CancellationChangeToken(CancellationToken.None));
        _mockServiceProvider!
            .Setup(sp => sp.GetService(typeof(IOptionsChangeTokenSource<TestOptions>)))
            .Returns(mockTokenSource.Object);
        _monitor!.AddOptionsMonitor<TestOptions>();

        Should.NotThrow(() => _monitor.RemoveOptionsMonitor<TestOptions>());
    }

    [TestMethod]
    public void RemoveOptionsMonitor_AfterAdding_StopsCallingGetChangeToken()
    {
        var mockTokenSource = new Mock<IOptionsChangeTokenSource<TestOptions>>();
        mockTokenSource
            .Setup(s => s.GetChangeToken())
            .Returns(new Microsoft.Extensions.Primitives.CancellationChangeToken(CancellationToken.None));
        _mockServiceProvider!
            .Setup(sp => sp.GetService(typeof(IOptionsChangeTokenSource<TestOptions>)))
            .Returns(mockTokenSource.Object);
        _monitor!.AddOptionsMonitor<TestOptions>();
        _monitor.RemoveOptionsMonitor<TestOptions>();

        _monitor.OnChange(() => { });

        mockTokenSource.Verify(s => s.GetChangeToken(), Times.Never);
    }

    [TestMethod]
    public void RemoveOptionsMonitor_NonRegisteredType_DoesNotThrow()
    {
        Should.NotThrow(() => _monitor!.RemoveOptionsMonitor<TestOptions>());
    }

    [TestMethod]
    public void RemoveOptionsMonitor_AfterDispose_DoesNotThrow()
    {
        _monitor!.Dispose();

        Should.NotThrow(() => _monitor.RemoveOptionsMonitor<TestOptions>());
    }

    #endregion

    #region OnChange

    [TestMethod]
    public void OnChange_NullCallback_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => _monitor!.OnChange(null!));
    }

    [TestMethod]
    public void OnChange_AfterDispose_ThrowsObjectDisposedException()
    {
        _monitor!.Dispose();

        Should.Throw<ObjectDisposedException>(() => _monitor.OnChange(() => { }));
    }

    [TestMethod]
    public void OnChange_WithValidCallback_DoesNotThrow()
    {
        Should.NotThrow(() => _monitor!.OnChange(() => { }));
    }

    [TestMethod]
    public void OnChange_WithAlreadyCancelledToken_DoesNotInvokeCallback()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        bool callbackInvoked = false;

        // The callback passed to OnChange should never be called when the CancellationToken is already cancelled
        _monitor!.OnChange(() => callbackInvoked = true, cts.Token);

        callbackInvoked.ShouldBeFalse();
    }

    [TestMethod]
    public void OnChange_WithCancellationToken_DoesNotInvokeCallbackOnCancellation()
    {
        using var cts = new CancellationTokenSource();
        bool callbackInvoked = false;

        _monitor!.OnChange(() => callbackInvoked = true, cts.Token);
        cts.Cancel();

        // Cancellation stops monitoring but does not trigger the user callback
        callbackInvoked.ShouldBeFalse();
    }

    [TestMethod]
    public void OnChange_WithGetChangeTokenReturningNull_ThrowsInvalidOperationException()
    {
        var mockTokenSource = new Mock<IOptionsChangeTokenSource<TestOptions>>();
        mockTokenSource
            .Setup(s => s.GetChangeToken())
            .Returns((Microsoft.Extensions.Primitives.IChangeToken?)null!);
        _mockServiceProvider!
            .Setup(sp => sp.GetService(typeof(IOptionsChangeTokenSource<TestOptions>)))
            .Returns(mockTokenSource.Object);

        _monitor!.AddOptionsMonitor<TestOptions>();

        // CollectMonitoredOptionsTokens yields via reflection; null return triggers the ?? throw
        Should.Throw<InvalidOperationException>(() => _monitor.OnChange(() => { }));
    }

    #endregion

    #region StopMonitoring

    [TestMethod]
    public void StopMonitoring_WhenNotMonitoring_DoesNotThrow()
    {
        Should.NotThrow(() => _monitor!.StopMonitoring());
    }

    [TestMethod]
    public void StopMonitoring_AfterOnChange_StopsWithoutError()
    {
        _monitor!.OnChange(() => { });

        Should.NotThrow(() => _monitor.StopMonitoring());
    }

    [TestMethod]
    public void StopMonitoring_AfterStop_CanRestartMonitoring()
    {
        _monitor!.OnChange(() => { });
        _monitor.StopMonitoring();

        // Should be able to re-register after stopping
        Should.NotThrow(() => _monitor.OnChange(() => { }));
    }

    [TestMethod]
    public void StopMonitoring_CalledMultipleTimes_DoesNotThrow()
    {
        _monitor!.OnChange(() => { });
        _monitor.StopMonitoring();

        Should.NotThrow(() => _monitor.StopMonitoring());
    }

    [TestMethod]
    public void StopMonitoring_ThenDispose_DoesNotThrow()
    {
        _monitor!.OnChange(() => { });
        _monitor.StopMonitoring();

        Should.NotThrow(() => _monitor.Dispose());
    }

    #endregion

    #region Dispose

    [TestMethod]
    public void Dispose_CanCallMultipleTimes_DoesNotThrow()
    {
        _monitor!.Dispose();

        Should.NotThrow(() => _monitor.Dispose());
    }

    #endregion

    #region Integration: file change triggers callback

    [TestMethod]
    public void OnChange_WhenWatchedFileIsCreated_InvokesCallbackAfterDebounce()
    {
        string filePath = Path.Combine(_tempDirectory!, "watched.json");
        _monitor!.AddFilePath(filePath);

        using var callbackReceived = new ManualResetEventSlim(false);
        _monitor.OnChange(() => callbackReceived.Set());

        File.WriteAllText(filePath, "{}");

        // Wait up to 2 seconds: 300ms debounce + margin for FileSystemWatcher latency
        bool triggered = callbackReceived.Wait(TimeSpan.FromSeconds(2));
        triggered.ShouldBeTrue("Callback should have been invoked after the watched file was created");
    }

    [TestMethod]
    public void OnChange_WhenWatchedFileIsModified_InvokesCallbackAfterDebounce()
    {
        string filePath = Path.Combine(_tempDirectory!, "watched.json");
        File.WriteAllText(filePath, "{}");

        _monitor!.AddFilePath(filePath);

        using var callbackReceived = new ManualResetEventSlim(false);
        _monitor.OnChange(() => callbackReceived.Set());

        File.WriteAllText(filePath, "{\"updated\": true}");

        bool triggered = callbackReceived.Wait(TimeSpan.FromSeconds(2));
        triggered.ShouldBeTrue("Callback should have been invoked after the watched file was modified");
    }

    [TestMethod]
    public void OnChange_RapidFileChanges_InvokesCallbackOnce()
    {
        string filePath = Path.Combine(_tempDirectory!, "debounce.json");
        File.WriteAllText(filePath, "{}");

        _monitor!.AddFilePath(filePath);

        int callbackCount = 0;
        using var callbackReceived = new ManualResetEventSlim(false);
        _monitor.OnChange(() =>
        {
            Interlocked.Increment(ref callbackCount);
            callbackReceived.Set();
        });

        // Write multiple times rapidly to trigger debounce behavior
        for (int i = 0; i < 5; i++)
        {
            File.WriteAllText(filePath, $"{{\"i\": {i}}}");
        }

        // Wait for the debounced callback to fire
        callbackReceived.Wait(TimeSpan.FromSeconds(2));

        // Additional wait to ensure no further callbacks fire from the debounce window
        Thread.Sleep(600);

        callbackCount.ShouldBe(1, "Rapid changes should be debounced into a single callback invocation");
    }

    [TestMethod]
    public void StopMonitoring_BeforeFileChange_DoesNotInvokeCallback()
    {
        string filePath = Path.Combine(_tempDirectory!, "watched.json");
        File.WriteAllText(filePath, "{}");

        _monitor!.AddFilePath(filePath);

        bool callbackInvoked = false;
        _monitor.OnChange(() => callbackInvoked = true);
        _monitor.StopMonitoring();

        File.WriteAllText(filePath, "{\"updated\": true}");

        // Wait longer than the debounce to confirm no callback fires
        Thread.Sleep(600);

        callbackInvoked.ShouldBeFalse("Callback should not be invoked after monitoring is stopped");
    }

    [TestMethod]
    public void OnChange_CalledTwice_OnlySecondCallbackFires()
    {
        string filePath = Path.Combine(_tempDirectory!, "watched.json");
        File.WriteAllText(filePath, "{}");
        _monitor!.AddFilePath(filePath);

        int firstCallbackCount = 0;
        int secondCallbackCount = 0;
        using var secondFired = new ManualResetEventSlim(false);

        _monitor.OnChange(() => Interlocked.Increment(ref firstCallbackCount));
        _monitor.OnChange(() =>
        {
            Interlocked.Increment(ref secondCallbackCount);
            secondFired.Set();
        });

        File.WriteAllText(filePath, "{\"updated\": true}");
        secondFired.Wait(TimeSpan.FromSeconds(2));
        Thread.Sleep(600);  // Extra margin to catch any spurious first-callback invocations

        firstCallbackCount.ShouldBe(0, "First OnChange registration should have been replaced by the second call");
        secondCallbackCount.ShouldBe(1);
    }

    [TestMethod]
    public void OnChange_AfterCallbackFires_ReRegistersForNextChange()
    {
        string filePath = Path.Combine(_tempDirectory!, "watched.json");
        File.WriteAllText(filePath, "{}");
        _monitor!.AddFilePath(filePath);

        int callbackCount = 0;
        using var twoFired = new CountdownEvent(2);
        _monitor.OnChange(() =>
        {
            Interlocked.Increment(ref callbackCount);
            if (!twoFired.IsSet) twoFired.Signal();
        });

        // First change — fires callback and immediately re-registers for next change
        File.WriteAllText(filePath, "{\"i\": 1}");
        Thread.Sleep(500);  // Wait past debounce so re-registration is complete

        // Second change — should fire because OnChange re-registered itself
        File.WriteAllText(filePath, "{\"i\": 2}");

        twoFired.Wait(TimeSpan.FromSeconds(3))
            .ShouldBeTrue("Callback should fire twice: once per file change, proving automatic re-registration");
        callbackCount.ShouldBe(2);
    }

    [TestMethod]
    public void StopMonitoring_DuringDebounceWindow_CancelsCallback()
    {
        string filePath = Path.Combine(_tempDirectory!, "watched.json");
        File.WriteAllText(filePath, "{}");
        _monitor!.AddFilePath(filePath);

        bool callbackInvoked = false;
        _monitor.OnChange(() => callbackInvoked = true);

        File.WriteAllText(filePath, "{\"updated\": true}");
        Thread.Sleep(100);  // Let FileSystemWatcher detect the change and start the debounce timer

        _monitor.StopMonitoring();  // Dispose the timer before the 300ms debounce elapses

        Thread.Sleep(500);  // Wait well past what the debounce would have been
        callbackInvoked.ShouldBeFalse("StopMonitoring should have cancelled the pending debounce timer");
    }

    [TestMethod]
    public void Dispose_WhileMonitoring_SuppressesCallback()
    {
        string filePath = Path.Combine(_tempDirectory!, "watched.json");
        File.WriteAllText(filePath, "{}");
        _monitor!.AddFilePath(filePath);

        bool callbackInvoked = false;
        _monitor.OnChange(() => callbackInvoked = true);

        File.WriteAllText(filePath, "{\"updated\": true}");
        Thread.Sleep(100);  // Let FileSystemWatcher detect the change and start the debounce timer

        _monitor.Dispose();  // Dispose the timer before the 300ms debounce elapses
        // Cleanup will call Dispose again; that is safe because Dispose is idempotent

        Thread.Sleep(500);
        callbackInvoked.ShouldBeFalse("Dispose should suppress the pending debounce callback");
    }

    [TestMethod]
    public void AddFilePath_SamePathAddedTwice_CallbackFiresExactlyOnce()
    {
        string filePath = Path.Combine(_tempDirectory!, "test.json");
        File.WriteAllText(filePath, "{}");

        _monitor!.AddFilePath(filePath);
        _monitor.AddFilePath(filePath);  // Duplicate — overwrites the same dictionary key

        int callbackCount = 0;
        using var callbackReceived = new ManualResetEventSlim(false);
        _monitor.OnChange(() =>
        {
            Interlocked.Increment(ref callbackCount);
            callbackReceived.Set();
        });

        File.WriteAllText(filePath, "{\"updated\": true}");
        callbackReceived.Wait(TimeSpan.FromSeconds(2));
        Thread.Sleep(600);

        callbackCount.ShouldBe(1, "Duplicate AddFilePath should not cause multiple callbacks per change");
    }

    [TestMethod]
    public void OnChange_TwoFilesInSameDirectory_BothFilesAreWatched()
    {
        string file1 = Path.Combine(_tempDirectory!, "file1.json");
        string file2 = Path.Combine(_tempDirectory!, "file2.json");
        File.WriteAllText(file1, "{}");
        File.WriteAllText(file2, "{}");

        _monitor!.AddFilePath(file1);
        _monitor.AddFilePath(file2);  // Same directory — CollectFileWatchTokens reuses one PhysicalFileProvider

        using var bothFired = new CountdownEvent(2);
        _monitor.OnChange(() =>
        {
            if (!bothFired.IsSet) bothFired.Signal();
        });

        // First change triggers callback and re-registers (picking up file2 continues to be watched)
        File.WriteAllText(file1, "{\"a\": 1}");
        Thread.Sleep(500);  // Wait past debounce so re-registration completes

        File.WriteAllText(file2, "{\"b\": 2}");

        bothFired.Wait(TimeSpan.FromSeconds(3))
            .ShouldBeTrue("Both files in the same directory should be independently watched via a shared PhysicalFileProvider");
    }

    [TestMethod]
    public void RemoveFilePath_WithNormalizedPathWhenAddedWithDotSegments_DoesNotRemoveEntry()
    {
        // AddFilePath stores the raw input string as the dictionary key.
        // RemoveFilePath must be called with the exact same string to succeed.
        string pathWithDots = Path.Combine(_tempDirectory!, "subdir", "..", "test.json");
        string normalizedPath = Path.GetFullPath(pathWithDots);
        pathWithDots.ShouldNotBe(normalizedPath, "Test precondition: the two path forms must differ");

        File.WriteAllText(normalizedPath, "{}");
        _monitor!.AddFilePath(pathWithDots);

        // Remove using the normalized form — key mismatch means the entry survives
        _monitor.RemoveFilePath(normalizedPath);

        using var callbackReceived = new ManualResetEventSlim(false);
        _monitor.OnChange(() => callbackReceived.Set());

        File.WriteAllText(normalizedPath, "{\"updated\": true}");
        callbackReceived.Wait(TimeSpan.FromSeconds(2))
            .ShouldBeTrue("Entry added with dot-segment path should survive RemoveFilePath called with the normalized path");
    }

    [TestMethod]
    public void AddFilePath_AfterOnChangeRegistered_NewPathPickedUpAfterReRegistration()
    {
        string file1 = Path.Combine(_tempDirectory!, "file1.json");
        string file2 = Path.Combine(_tempDirectory!, "file2.json");
        File.WriteAllText(file1, "{}");
        File.WriteAllText(file2, "{}");

        _monitor!.AddFilePath(file1);

        int callbackCount = 0;
        using var firstFired = new ManualResetEventSlim(false);
        using var secondFired = new ManualResetEventSlim(false);
        _monitor.OnChange(() =>
        {
            if (Interlocked.Increment(ref callbackCount) == 1) firstFired.Set();
            else secondFired.Set();
        });

        // Add file2 AFTER OnChange — not yet included in the watched set
        _monitor.AddFilePath(file2);

        // Changing file1 fires the callback AND immediately re-registers OnChange (now including file2)
        File.WriteAllText(file1, "{\"changed\": 1}");
        firstFired.Wait(TimeSpan.FromSeconds(2)).ShouldBeTrue("file1 change should trigger first callback");

        // file2 is now watched following re-registration
        File.WriteAllText(file2, "{\"changed\": 2}");
        secondFired.Wait(TimeSpan.FromSeconds(2))
            .ShouldBeTrue("file2 should be monitored after the re-registration triggered by file1's change");
    }

    [TestMethod]
    public void OnChange_AfterFilePathRemoved_StaleFileIsNoLongerWatched()
    {
        string filePath = Path.Combine(_tempDirectory!, "stale.json");
        File.WriteAllText(filePath, "{}");
        _monitor!.AddFilePath(filePath);

        // First registration creates the PhysicalFileProvider for the directory
        _monitor.OnChange(() => { });

        // Remove the path and force a fresh registration — disposes the stale provider
        _monitor.RemoveFilePath(filePath);
        _monitor.StopMonitoring();

        bool callbackInvoked = false;
        _monitor.OnChange(() => callbackInvoked = true);  // CollectFileWatchTokens now skips filePath

        File.WriteAllText(filePath, "{\"updated\": true}");
        Thread.Sleep(600);

        callbackInvoked.ShouldBeFalse("Removed file path should not trigger callback after re-registration");
    }

    [TestMethod]
    public void OnChange_WhenWatchedFileIsDeleted_InvokesCallback()
    {
        string filePath = Path.Combine(_tempDirectory!, "watched.json");
        File.WriteAllText(filePath, "{}");
        _monitor!.AddFilePath(filePath);

        using var callbackReceived = new ManualResetEventSlim(false);
        _monitor.OnChange(() => callbackReceived.Set());

        File.Delete(filePath);

        callbackReceived.Wait(TimeSpan.FromSeconds(2))
            .ShouldBeTrue("Deleting a watched file should trigger the callback");
    }

    #endregion
}
