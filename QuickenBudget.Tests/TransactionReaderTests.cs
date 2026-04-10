using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using QuickenBudget.Models;
using QuickenBudget.Services;

using Serilog.Events;
using Serilog.Sinks.InMemory;
using Serilog.Sinks.InMemory.Assertions;

using Shouldly;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace QuickenBudget.Tests;

[TestClass]
public class TransactionReaderTests : TestBase
{
    private ILogger<TransactionReader>? _logger;
    private TransactionReaderSettings _readerSettings = new();
    private TransactionSelectors _groupSelectors = new();
    private TestOptionsMonitor<TransactionReaderSettings> _readerSettingsMonitor = null!;
    private TestOptionsMonitor<TransactionSelectors> _groupSelectorsMonitor = null!;
    private TransactionReader _reader = null!;
    private string _tempFilePath = string.Empty;
    private readonly List<string> _extraTempFilePaths = [];

    public TransactionReaderTests() : base(true, LogEventLevel.Information)
    {
    }

    /// <summary>
    /// Initializes the test environment by configuring the transaction reader settings, group selectors, and ensuring
    /// the logger is set up before each test.
    /// </summary>
    /// <remarks>This method is decorated with the TestInitialize attribute and is executed before each test
    /// to ensure that all required dependencies and configurations are properly set up for consistent test
    /// execution.</remarks>
    /// <exception cref="InvalidOperationException">Thrown if the logger is not initialized prior to calling this method.</exception>

    [TestInitialize]
    public void Setup()
    {
        _logger = CreateTestLogger<TransactionReader>();
        _tempFilePath = Path.GetTempFileName();
        _readerSettings = new TransactionReaderSettings { QuickenReportFile = _tempFilePath };
        _groupSelectors = new TransactionSelectors
        {
            ExpensesGroupSelectors = {["Test"] = [
                    new GroupSelector { Field = "Category", Filter = "Food", TargetGroup = "Food" },
                    new GroupSelector { Field = "Category", Filter = "Transport", TargetGroup = "Transport" }]},
            TransactionFilters = {["Test"] = [
                new TransactionFilter { Field = "Category", Filter = "Ignore" }]
            },
            IncomeGroupSelectors = {["Test"] = [
                    new GroupSelector { Field = "Category", Filter = "Salary", TargetGroup = "Salary" },
                    new GroupSelector { Field = "Category", Filter = "Bonus", TargetGroup = "Bonus" }]}
        };

        _readerSettingsMonitor = new TestOptionsMonitor<TransactionReaderSettings>(_readerSettings);
        _groupSelectorsMonitor = new TestOptionsMonitor<TransactionSelectors>(_groupSelectors);
        _reader = new TransactionReader(_logger, _groupSelectorsMonitor, _readerSettingsMonitor);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (!string.IsNullOrEmpty(_tempFilePath) && File.Exists(_tempFilePath))
        {
            File.Delete(_tempFilePath);
        }

        foreach (string filePath in _extraTempFilePaths.Where(File.Exists))
        {
            File.Delete(filePath);
        }

        _extraTempFilePaths.Clear();
    }

    /// <summary>
    /// Verifies that the Ingest method correctly parses a valid transaction file and returns the expected list of
    /// transactions.
    /// </summary>
    /// <remarks>This test creates a sample transaction file with a header and multiple entries, then asserts
    /// that the Ingest method returns a list containing the correct transactions with accurate data for each
    /// field.</remarks>
    [TestMethod]
    public void Ingest_ValidFile_ReturnsTransactions()
    {
        // Arrange
        File.WriteAllLines(_readerSettings.QuickenReportFile,
        [
            "\n\n\n\n\n",
            "Date\tCategory\tAmount\tDescription",
            "2023-01-01\tFood\t-50.00\tGroceries",
            "2023-01-02\tTransport\t-20.00\tBus fare",
            "2023-01-03\tIncome\t100.00\tSalary"
        ]);

        // Act
        List<Transaction> transactions = _reader.Ingest();

        // Assert
        transactions.Count.ShouldBe(3);
        transactions.ShouldContain(t => t.GroupName == "Food" && t.Amount == -50m && !t.IsIncome);
        transactions.ShouldContain(t => t.GroupName == "Transport" && t.Amount == -20m && !t.IsIncome);
        transactions.ShouldContain(t => t.GroupName == "Other" && t.Amount == 100m && t.IsIncome);
    }


    /// <summary>
    /// Verifies that the Ingest method throws an InvalidOperationException when called with a null settings
    /// configuration.
    /// </summary>
    /// <remarks>This test ensures that the Ingest method enforces the requirement for a valid logger instance
    /// and handles null settings appropriately by throwing an exception.</remarks>
    /// <exception cref="InvalidOperationException">Thrown if the logger is not initialized before calling the Ingest method.</exception>
    [TestMethod]
    public void Ingest_NullSettings_ThrowsException()
    {
        // Arrange
        // Separate reader with no settings
        if (_logger == null)
        {
            throw new InvalidOperationException("Logger not initialized");
        }

        TestOptionsMonitor<TransactionReaderSettings> readerSettingsMonitor = new(null!);
        TransactionReader reader = new(_logger, _groupSelectorsMonitor, readerSettingsMonitor);

        // Act and Assert
        Should.Throw<InvalidOperationException>(reader.Ingest);
    }

    /// <summary>
    /// Verifies that the Ingest method throws an InvalidOperationException when the file path is empty.
    /// </summary>
    [TestMethod]
    public void Ingest_EmptyFilePath_ThrowsException()
    {
        // Arrange
        _readerSettings.QuickenReportFile = "";

        // Act
        Should.Throw<InvalidOperationException>(_reader.Ingest);

        // Assert: Exception expected
    }

    [TestMethod]
    public void Ingest_UsesUpdatedSettingsFromMonitor()
    {
        // Arrange
        string secondFilePath = Path.GetTempFileName();
        _extraTempFilePaths.Add(secondFilePath);

        File.WriteAllLines(_readerSettings.QuickenReportFile,
        [
            "\n\n\n\n\n",
            "Date\tCategory\tAmount\tDescription",
            "2023-01-01\tFood\t-50.00\tGroceries"
        ]);
        File.WriteAllLines(secondFilePath,
        [
            "\n\n\n\n\n",
            "Date\tCategory\tAmount\tDescription",
            "2024-01-01\tTransport\t-25.00\tTrain"
        ]);

        // Act
        List<Transaction> initialTransactions = _reader.Ingest();
        _readerSettingsMonitor.Update(new TransactionReaderSettings {QuickenReportFile = secondFilePath});
        List<Transaction> updatedTransactions = _reader.Ingest();

        // Assert
        initialTransactions.Count.ShouldBe(1);
        initialTransactions[0].GroupName.ShouldBe("Food");
        updatedTransactions.Count.ShouldBe(1);
        updatedTransactions[0].GroupName.ShouldBe("Transport");
        updatedTransactions[0].Date.ShouldBe(new DateOnly(2024, 1, 1));
    }

    /// <summary>
    /// Verifies that the Ingest method filters out transactions that do not meet the specified criteria and returns
    /// only the relevant transactions.
    /// </summary>
    [TestMethod]
    public void Ingest_FiltersOutTransactions()
    {
        // Arrange
        File.WriteAllLines(_readerSettings.QuickenReportFile,
        [
            "\n\n\n\n\n",
            "Date\tCategory\tAmount\tDescription",
            "2023-01-01\tFood\t-50.00\tGroceries",
            "2023-01-02\tIgnore\t-10.00\tFiltered out"
        ]);

        // Act
        List<Transaction> transactions = _reader.Ingest();

        // Assert
        transactions.Count.ShouldBe(1);
        transactions[0].GroupName.ShouldBe("Food");
    }

    /// <summary>
    /// Tests that the Ingest method correctly propagates the date from a transaction with a valid date to subsequent
    /// transactions that have no date.
    /// </summary>
    [TestMethod]
    public void Ingest_PropagatesDate()
    {
        // Arrange
        File.WriteAllLines(_readerSettings.QuickenReportFile,
        [
            "\n\n\n\n\n",
            "Date\tCategory\tAmount\tDescription",
            "2023-01-01\tFood\t-50.00\tGroceries",
            "\tSomething\t-20.00\tMore groceries" // No date, should propagate
        ]);

        // Act
        List<Transaction> transactions = _reader.Ingest();

        // Assert
        transactions.Count.ShouldBe(2);
        transactions[0].Date.ShouldBe(new DateOnly(2023, 1, 1));
        transactions[1].Date.ShouldBe(new DateOnly(2023, 1, 1)); // Propagated
    }

    /// <summary>
    /// Tests the ingestion of transactions from a Quicken report file, ensuring that transactions with negative amounts
    /// are grouped as 'Other' and those with positive amounts are grouped as 'Income'.
    /// </summary>
    [TestMethod]
    public void Ingest_DefaultsToOtherOrIncomeGroup()
    {
        // Arrange
        File.WriteAllLines(_readerSettings.QuickenReportFile,
        [
            "\n\n\n\n",
            "Date\tCategory\tAmount\tDescription",
            "2023-01-01\tUnknown\t-50.00\tMisc",
            "2023-01-02\tBonus\t100.00\tExtra income"
        ]);

        // Act
        List<Transaction> transactions = _reader.Ingest();

        // Assert
        transactions.Count.ShouldBe(2);
        transactions[0].GroupName.ShouldBe("Other"); // Expense
        transactions[1].GroupName.ShouldBe("Bonus"); // Income
    }

    /// <summary>
    /// Validates that the ingestion process ignores a record that contains an invalid amount value.
    /// </summary>
    [TestMethod]
    public void Ingest_InvalidAmount_IgnoresTransaction()
    {
        // Arrange
        File.WriteAllLines(_readerSettings.QuickenReportFile,
        [
            "\n\n\n\n\n",
            "Date\tCategory\tAmount\tDescription",
            "2023-01-01\tFood\tInvalid\tGroceries"
        ]);

        // Act
        List<Transaction> transactions = _reader.Ingest();

        // Assert
        transactions.Count.ShouldBe(0);
    }

    [TestMethod]
    public void Ingest_InvalidDate_IgnoreTransaction()
    {
        // Arrange
        File.WriteAllLines(_readerSettings.QuickenReportFile,
        [
            "\n\n\n\n\n",
            "Date\tCategory\tAmount\tDescription",
            "2023-01-01\tFood\t-50.00\tGroceries",
            "Invalid Date\tSomething\t-20.00\tMore groceries" // Invalid date, should be ignored
        ]);

        // Act
        List<Transaction> transactions = _reader.Ingest();

        // Assert
        transactions.Count.ShouldBe(1);
        transactions[0].Date.ShouldBe(new DateOnly(2023, 1, 1));
    }

    /// <summary>
    /// Verifies that the Ingest method handles a data line with fewer fields than headers by creating a transaction
    /// with the available fields and missing fields not included in OtherFields.
    /// </summary>
    [TestMethod]
    public void Ingest_LineWithFewerFieldsThanHeaders()
    {
        // Arrange
        File.WriteAllLines(_readerSettings.QuickenReportFile,
        [
            "\n\n\n\n",
            "Date\tCategory\tAmount\tDescription\tExtra",
            "2023-01-01\tFood\t-50.00\tGroceries",
            "2023-01-01\tFood\t-50.00\tGroceries\tExtra Data"
        ]);

        // Act
        List<Transaction> transactions = _reader.Ingest();

        // Assert
        transactions.Count.ShouldBe(1);
        var tx = transactions[0];
        tx.Date.ShouldBe(new DateOnly(2023, 1, 1));
        tx.Amount.ShouldBe(-50.00m);
        tx.OtherFields.ContainsKey("Category").ShouldBeTrue();
        tx.OtherFields["Category"].ShouldBe("Food");
        tx.OtherFields.ContainsKey("Description").ShouldBeTrue();
        tx.OtherFields["Description"].ShouldBe("Groceries");
        tx.OtherFields.ContainsKey("Extra").ShouldBeTrue();
        tx.OtherFields["Extra"].ShouldBe("Extra Data");
    }

    /// <summary>
    /// Verifies that the Ingest method handles a data line with more fields than headers by ignoring the extra fields.
    /// </summary>
    [TestMethod]
    public void Ingest_LineWithMoreFieldsThanHeaders()
    {
        // Arrange
        File.WriteAllLines(_readerSettings.QuickenReportFile,
        [
            "\n\n\n\n",
            "Date\tCategory\tAmount\tDescription",
            "2023-01-01\tFood\t-50.00\tGroceries\tExtraValue"
        ]);

        // Act
        List<Transaction> transactions = _reader.Ingest();

        // Assert
        transactions.Count.ShouldBe(0);
    }

    /// <summary>
    /// Verifies that the Ingest method throws a FileNotFoundException wrapped into the ParsingException when attempting to ingest a non-existent file.
    /// </summary>
    [TestMethod]
    public void Ingest_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        _readerSettings.QuickenReportFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt"); // guaranteed non-existent
        var reader = new TransactionReader(_logger!, _groupSelectorsMonitor, _readerSettingsMonitor);

        // Act / Assert
        Should
            .Throw<ParsingException>(() => reader.Ingest().ToList()).InnerException
            .ShouldNotBeNull()
            .ShouldBeOfType<FileNotFoundException>();
    }

    /// <summary>
    /// Verifies that selector regexes use a bounded timeout to avoid unbounded regex evaluation.
    /// </summary>
    [TestMethod]
    public void GroupSelector_Filter_UsesBoundedRegexTimeout()
    {
        GroupSelector selector = new() { Field = "Category", Filter = "Food", TargetGroup = "Food" };

        Regex compiledRegex = GetCompiledRegex<GroupSelector>(selector);

        compiledRegex.MatchTimeout.ShouldNotBe(System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
    }

    /// <summary>
    /// Verifies that filter regexes use a bounded timeout to avoid unbounded regex evaluation.
    /// </summary>
    [TestMethod]
    public void TransactionFilter_Filter_UsesBoundedRegexTimeout()
    {
        TransactionFilter filter = new() { Field = "Category", Filter = "Ignore" };

        Regex compiledRegex = GetCompiledRegex<TransactionFilter>(filter);

        compiledRegex.MatchTimeout.ShouldNotBe(System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
    }

    /// <summary>
    /// Verifies that pathological selector input surfaces a regex timeout through the reader.
    /// </summary>
    [TestMethod]
    public void Ingest_RegexTimeout_ThrowsWrappedRegexMatchTimeoutException()
    {
        _groupSelectors = new TransactionSelectors
        {
            ExpensesGroupSelectors =
            {
                ["Catastrophic"] =
                [
                    new GroupSelector { Field = "Category", Filter = "^(a+)+$", TargetGroup = "SlowGroup" }
                ]
            }
        };
        _groupSelectorsMonitor.Update(_groupSelectors);
        _reader = new TransactionReader(_logger!, _groupSelectorsMonitor, _readerSettingsMonitor);

        string pathologicalCategory = new string('a', 20_000) + "!";
        File.WriteAllLines(_readerSettings.QuickenReportFile,
        [
            "\n\n\n\n\n",
            "Date\tCategory\tAmount\tDescription",
            $"2023-01-01\t{pathologicalCategory}\t-50.00\tGroceries"
        ]);

        var ex = Should.Throw<ParsingException>(_reader.Ingest);
        ex.InnerException.ShouldNotBeNull().ShouldBeOfType<RegexMatchTimeoutException>();
    }

    /// <summary>
    /// Verifies that ingest logging includes the source file size for troubleshooting large reloads.
    /// </summary>
    [TestMethod]
    public void Ingest_LogsFileSizeWhenReadingTransactions()
    {
        File.WriteAllLines(_readerSettings.QuickenReportFile,
        [
            "\n\n\n\n\n",
            "Date\tCategory\tAmount\tDescription",
            "2023-01-01\tFood\t-50.00\tGroceries"
        ]);

        _reader.Ingest();

        InMemorySink.Instance
            .Should()
            .HaveMessage()
            .Containing("Loading transactions from file")
            .ShouldMatchTestName()
            .WithLevel(LogEventLevel.Information)
            .WithProperty("FileSizeBytes");
    }

    private static Regex GetCompiledRegex<T>(T instance)
    {
        FieldInfo? regexField = typeof(T).GetField("_filter", BindingFlags.Instance | BindingFlags.NonPublic);
        regexField.ShouldNotBeNull();

        Regex? compiledRegex = regexField.GetValue(instance) as Regex;
        compiledRegex.ShouldNotBeNull();

        return compiledRegex;
    }
}
