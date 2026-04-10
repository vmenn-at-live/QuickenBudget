using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Serilog.Events;
using Serilog.Sinks.InMemory;
using Serilog.Sinks.InMemory.Assertions;
using Shouldly;

using QuickenBudget.Models;
using QuickenBudget.Tools;

namespace QuickenBudget.Tests;

[TestClass]
public class CSVReaderTests : TestBase
{
    private ILogger? _logger;

    public CSVReaderTests() : base(true, LogEventLevel.Warning)
    {
    }

    [TestInitialize]
    public void Setup()
    {
        _logger = CreateTestLogger<CSVReaderTests>();
    }

    /// <summary>
    ///  Simple test to verify that the ParseLines method correctly parses lines with a header, applies field mappings, and skips lines as configured in the settings.
    ///  It also verifies that the required fields are correctly mapped and that skipped fields are not included in the output records.
    /// </summary>
    [TestMethod]
    public void ParseLines_ParsesSimpleRows_WithHeaderMappingAndSkips()
    {
        // Arrange
        var settings = new CSVSettings
        {
            LinesToSkip = 2,
            Delimiter = '\t',
            TrimFields = true,
            HandleQuotedFields = true,
            FieldMappings = new Dictionary<string, CSVSettings.FieldRecord>
            {
                ["Category"] = new("Group", true, false),
                ["Amount"] = new("Amount", true, false),
                ["IgnoreMe"] = new("", false, true)
            }
        };

        var lines = new[]
        {
            "skip this",
            "and this",
            "Date\tCategory\tAmount\tIgnoreMe",
            "2023-01-01\tFood\t-50.00\tX",
            "2023-01-02\tTransport\t-20.00\tY"
        };

        // Act
        var records = CSVReader.ParseLines(lines, settings, _logger!).ToList();

        // Assert
        records.Count.ShouldBe(2);
        records[0].line.ShouldBe(4);
        records[0].propertyMap.ContainsKey("Group").ShouldBeTrue();
        records[0].propertyMap["Group"].ShouldBe("Food");
        records[0].propertyMap.ContainsKey("Amount").ShouldBeTrue();
        records[0].propertyMap["Amount"].ShouldBe("-50.00");
        records[0].propertyMap.ContainsKey("Date").ShouldBeTrue();
        records[0].propertyMap["Date"].ShouldBe("2023-01-01");
        records[0].propertyMap.ContainsKey("IgnoreMe").ShouldBeFalse();
        records[1].line.ShouldBe(5);
        records[1].propertyMap.ContainsKey("IgnoreMe").ShouldBeFalse();
    }

    /// <summary>
    ///  Verify that providing wrong number of fields will generate a warning  and skips the offending row.
    /// </summary>
    [TestMethod]
    public void ParseLines_LogsWarning_WhenWrongFieldCount()
    {
        // Arrange
        var settings = new CSVSettings
        {
            LinesToSkip = 0,
            Delimiter = '\t',
            TrimFields = true,
            HandleQuotedFields = true,
            FieldMappings = new Dictionary<string, CSVSettings.FieldRecord>
            {
                ["Category"] = new("Category", true, false),
                ["Amount"] = new("Amount", true, false)
            }
        };

        var lines = new[]
        {
            "Category\tAmount",
            "Food\t-50.00",
            "BadLineWith\tToo\tMany"
        };

        // Act
        var records = CSVReader.ParseLines(lines, settings, _logger!).ToList();

        // Assert: we expect 1 good record and one skipped line with a warning
        records.Count.ShouldBe(1);
        records[0].line.ShouldBe(2);
        InMemorySink.Instance
            .Should()
            .HaveMessage()
            .Containing("wrong number of fields")
            .ShouldMatchTestName()
            .WithLevel(LogEventLevel.Warning)
            .WithProperty("lineNumber")
            .WithValues([3])
            .WithProperty("actualFields")
            .WithValues([3])
            .WithProperty("headerFields")
            .WithValues([2]);
    }

    /// <summary>
    /// Verifies that if a required field specified in the settings is missing from the header, the ParseLines method throws an InvalidDataException.
    /// </summary>
    [TestMethod]
    public void ParseLines_Throws_WhenMissingRequiredField()
    {
        // Arrange
        var settings = new CSVSettings
        {
            LinesToSkip = 0,
            Delimiter = '\t',
            TrimFields = true,
            HandleQuotedFields = true,
            FieldMappings = new Dictionary<string, CSVSettings.FieldRecord>
            {
                ["Category"] = new("Category", true, false),
                ["Amount"] = new("Amount", true, false),
                ["RequiredButMissing"] = new("X", true, false)
            }
        };

        var lines = new[]
        {
            "Category\tAmount",
            "Food\t-50.00"
        };

        // Act & Assert
        var ex = Should.Throw<InvalidDataException>(() => CSVReader.ParseLines(lines, settings, _logger!).ToList());
        ex.Message.ShouldContain("Missing required fields: RequiredButMissing");
    }

    /// <summary>
    /// Verifies that when the delimiter is a whitespace character, the ParseLines method still splits and trims individual fields as configured in the settings.
    /// This ensures that leading/trailing whitespace in the line does not affect field parsing, while still allowing for clean field values.
    /// </summary>
    [TestMethod]
    public void ParseLines_TrimsFields_ButNotLineWhenDelimiterIsWhitespace()
    {
        // Arrange
        var settings = new CSVSettings
        {
            LinesToSkip = 0,
            Delimiter = '\t',
            TrimFields = true,
            HandleQuotedFields = true,
            FieldMappings = new Dictionary<string, CSVSettings.FieldRecord>
            {
                ["A"] = new("A", false, false),
                ["B"] = new("B", false, false)
            }
        };

        var lines = new[]
        {
            "A\tB",
            "  x \t y  "
        };

        // Act
        var records = CSVReader.ParseLines(lines, settings, _logger!).ToList();

        // Assert: since delimiter is whitespace, trimming of the whole line is disabled but fields are still split and then trimmed
        records.Count.ShouldBe(1);
        records[0].propertyMap["A"].ShouldBe("x");
        records[0].propertyMap["B"].ShouldBe("y");
    }

    /// <summary>
    /// Verifies that when the delimiter is not a whitespace character, the ParseLines method still trims the whole line and splits.
    /// Individual fields are trimmed as configured in the settings and this tests verifies that if settings says not to trim fields,
    /// the fields are not trimmed.
    /// </summary>
    [TestMethod]
    public void ParseLines_NoTrimsFields_TrimLineWhenDelimiterIsNotWhitespace()
    {
        // Arrange
        var settings = new CSVSettings
        {
            LinesToSkip = 0,
            Delimiter = ',',
            TrimFields = false,
            HandleQuotedFields = true,
            FieldMappings = new Dictionary<string, CSVSettings.FieldRecord>
            {
                ["A"] = new("A", false, false),
                ["B"] = new("B", false, false)
            }
        };

        var lines = new[]
        {
            "A,B",
            "  x , y  "
        };

        // Act
        var records = CSVReader.ParseLines(lines, settings, _logger!).ToList();

        // Assert: since the delimiter is not whitespace, the whole line is trimmed before splitting;
        // fields are not trimmed because TrimFields is false.
        records.Count.ShouldBe(1);
        records[0].propertyMap["A"].ShouldBe("x ");
        records[0].propertyMap["B"].ShouldBe(" y");
    }

    /// <summary>
    /// Verifies that the ParseLines method throws an InvalidDataException when duplicate headers are encountered in the CSV input.
    /// </summary>
    [TestMethod]
    public void ParseLines_ThrowsInvalidDataException_OnDuplicateHeaders()
    {
        // Arrange
        var settings = new CSVSettings
        {
            LinesToSkip = 0,
            Delimiter = ',',
            TrimFields = false,
            HandleQuotedFields = true,
            FieldMappings = new Dictionary<string, CSVSettings.FieldRecord>
            {
                ["A"] = new("A", false, false)
            }
        };

        string[] lines =
        [
            "A,A",
            "1,2"
        ];

        // Act & Assert
        var ex = Should.Throw<InvalidDataException>(() => CSVReader.ParseLines(lines, settings, _logger!).ToList());
        ex.Message.ShouldContain("Duplicate header found: A");
    }

    /// <summary>
    /// Verifies that the ParseLines method throws an InvalidDataException when duplicate mapping values are present in
    /// the field mappings configuration.
    /// </summary>
    [TestMethod]
    public void ParseLines_ThrowsInvalidDataException_OnDuplicateMapToValues()
    {
        // Arrange
        var settings = new CSVSettings
        {
            LinesToSkip = 0,
            Delimiter = ',',
            TrimFields = true,
            HandleQuotedFields = true,
            FieldMappings = new Dictionary<string, CSVSettings.FieldRecord>
            {
                ["A"] = new("A", false, false),
                ["B"] = new("A", false, false)
            }
        };
        string[] lines =
        [
            "A,B",
            "1,2"
        ];

        // Act & Assert
        var ex = Should.Throw<InvalidDataException>(() => CSVReader.ParseLines(lines, settings, _logger!).ToList());
        ex.Message.ShouldContain("Duplicate MapTo values found in field mappings.");
    }

    /// <summary>
    /// Verifies that the ParseLines method correctly handles quoted fields when using a comma as the delimiter.
    /// </summary>
    [TestMethod]
    public void ParseLines_HandlesQuotedFields_WithCommaDelimiter()
    {
        // Arrange
        var settings = new CSVSettings
        {
            LinesToSkip = 0,
            Delimiter = ',',
            TrimFields = true,
            HandleQuotedFields = true,
            FieldMappings = new Dictionary<string, CSVSettings.FieldRecord>
            {
                ["A"] = new("A", false, false),
                ["B"] = new("B", false, false)
            }
        };
        var lines = new[]
        {
            "A,B",
            "\"x,y\",z"
        };

        // Act
        var records = CSVReader.ParseLines(lines, settings, _logger!).ToList();

        // Assert
        records.Count.ShouldBe(1);
        records[0].propertyMap["A"].ShouldBe("x,y");
        records[0].propertyMap["B"].ShouldBe("z");
    }

    /// <summary>
    /// Verifies that the CSV parser correctly handles escaped quotes within quoted fields, ensuring accurate parsing
    /// according to CSV standards (the RFC 4180 specification).
    /// </summary>
    [TestMethod]
    public void ParseLines_HandlesEscapedQuotes_InsideQuotedField()
    {
        // Arrange
        var settings = new CSVSettings
        {
            LinesToSkip = 0,
            Delimiter = ',',
            TrimFields = true,
            HandleQuotedFields = true,
            FieldMappings = new Dictionary<string, CSVSettings.FieldRecord>
            {
                ["A"] = new("A", false, false),
                ["B"] = new("B", false, false)
            }
        };

        var lines = new[]
        {
            "A,B",
            "\"He said \"\"Hello\"\"\",Value"
        };

        // Act
        var records = CSVReader.ParseLines(lines, settings, _logger!).ToList();

        // Assert
        records.Count.ShouldBe(1);
        records[0].propertyMap["A"].ShouldBe("He said \"Hello\"");
        records[0].propertyMap["B"].ShouldBe("Value");
    }

    /// <summary>
    /// Verifies that the CSV parser treats a quote character within an unquoted field as a regular character, ensuring
    /// correct parsing of CSV lines.
    /// </summary>
    [TestMethod]
    public void ParseLines_TreatsQuoteInMiddleOfUnquotedField_AsRegularCharacter()
    {
        // Arrange
        var settings = new CSVSettings
        {
            LinesToSkip = 0,
            Delimiter = ',',
            TrimFields = true,
            HandleQuotedFields = true,
            FieldMappings = new Dictionary<string, CSVSettings.FieldRecord>
            {
                ["A"] = new("A", false, false),
                ["B"] = new("B", false, false)
            }
        };
        var lines = new[]
        {
            "A,B",
            "abc\"def,Value"
        };

        // Act
        var records = CSVReader.ParseLines(lines, settings, _logger!).ToList();

        // Assert
        records.Count.ShouldBe(1);
        records[0].propertyMap["A"].ShouldBe("abc\"def");
        records[0].propertyMap["B"].ShouldBe("Value");
    }

    /// <summary>
    /// Verifies that the CSV parser correctly interprets a line containing only quotes as an empty field.
    /// </summary>
    [TestMethod]
    public void ParseLines_HandlesLineWithOnlyQuotes_AsEmptyField()
    {
        // Arrange
        var settings = new CSVSettings
        {
            LinesToSkip = 0,
            Delimiter = ',',
            TrimFields = true,
            HandleQuotedFields = true,
            FieldMappings = new Dictionary<string, CSVSettings.FieldRecord>
            {
                ["A"] = new("A", false, false)
            }
        };
        var lines = new[]
        {
            "A",
            "\"\""
        };

        // Act
        var records = CSVReader.ParseLines(lines, settings, _logger!).ToList();

        // Assert
        records.Count.ShouldBe(1);
        records[0].propertyMap["A"].ShouldBe(string.Empty);
    }

    /// <summary>
    /// Verifies that the CSV parser correctly handles empty quoted fields located in the middle of a row.
    /// </summary>
    [TestMethod]
    public void ParseLines_HandlesEmptyQuotedField_InMiddleOfRow()
    {
        // Arrange
        var settings = new CSVSettings
        {
            LinesToSkip = 0,
            Delimiter = ',',
            TrimFields = true,
            HandleQuotedFields = true,
            FieldMappings = new Dictionary<string, CSVSettings.FieldRecord>
            {
                ["A"] = new("A", false, false),
                ["B"] = new("B", false, false),
                ["C"] = new("C", false, false)
            }
        };
        var lines = new[]
        {
            "A,B,C",
            "1,\"\",3"
        };

        // Act
        var records = CSVReader.ParseLines(lines, settings, _logger!).ToList();

        // Assert
        records.Count.ShouldBe(1);
        records[0].propertyMap["A"].ShouldBe("1");
        records[0].propertyMap["B"].ShouldBe(string.Empty);
        records[0].propertyMap["C"].ShouldBe("3");
    }

    /// <summary>
    /// Verifies that the CSV parser correctly handles whitespace within quoted fields.
    /// </summary>
    [TestMethod]
    public void ParseLines_HandlesWhitespaceWithinQuotes()
    {
        // Arrange
        var settings = new CSVSettings
        {
            LinesToSkip = 0,
            Delimiter = ',',
            TrimFields = false,
            HandleQuotedFields = true,
            FieldMappings = new Dictionary<string, CSVSettings.FieldRecord>
            {
                ["A"] = new("A", false, false)
            }
        };
        var lines = new[]
        {
            "A",
            "\" \""
        };
        // Act
        var records = CSVReader.ParseLines(lines, settings, _logger!).ToList();
        // Assert
        records.Count.ShouldBe(1);
        records[0].propertyMap["A"].ShouldBe(" ");
    }


    /// <summary>
    /// Verifies that the CSV parser does not handle quoted fields and treats quotes as literal characters.
    /// </summary>
    [TestMethod]
    public void ParseLines_DoesNotHandleQuotedFields_TreatsQuotesAsLiterals()
    {
        // Arrange
        var settings = new CSVSettings
        {
            LinesToSkip = 0,
            Delimiter = ',',
            TrimFields = true,
            HandleQuotedFields = false,
            FieldMappings = new Dictionary<string, CSVSettings.FieldRecord>
            {
                ["A"] = new("A", false, false),
                ["B"] = new("B", false, false)
            }
        };
        var lines = new[]
        {
            "A,B",
            "\"value\",plain"
        };
        // Act
        var records = CSVReader.ParseLines(lines, settings, _logger!).ToList();
        // Assert
        records.Count.ShouldBe(1);
        records[0].propertyMap["A"].ShouldBe("\"value\"");
        records[0].propertyMap["B"].ShouldBe("plain");
    }

    /// <summary>
    /// Verifies that the CSV parser does not unescape embedded quotes when quoted field handling is disabled.
    /// </summary>
    [TestMethod]
    public void ParseLines_DoesNotHandleQuotedFields_DoesNotUnescapeEmbeddedQuotes()
    {
        // Arrange
        var settings = new CSVSettings
        {
            LinesToSkip = 0,
            Delimiter = ',',
            TrimFields = true,
            HandleQuotedFields = false,
            FieldMappings = new Dictionary<string, CSVSettings.FieldRecord>
            {
                ["A"] = new("A", false, false),
                ["B"] = new("B", false, false)
            }
        };
        var lines = new[]
        {
            "A,B",
            "\"He said \"\"Hello\"\"\",Value"
        };
        // Act
        var records = CSVReader.ParseLines(lines, settings, _logger!).ToList();
        // Assert
        records.Count.ShouldBe(1);
        records[0].propertyMap["A"].ShouldBe("\"He said \"\"Hello\"\"\"");
        records[0].propertyMap["B"].ShouldBe("Value");
    }

    /// <summary>
    /// Verifies that the ParseLines method throws an ArgumentNullException when the settings parameter is null.
    /// </summary>
    [TestMethod]
    public void ParseLines_Throws_WhenSettingsNull()
    {
        // Arrange
        var lines = new[] { "A,B", "1,2" };

        // Act / Assert
        var ex = Should.Throw<ArgumentNullException>(() => CSVReader.ParseLines(lines, null!, _logger!).ToList());
        ex.ParamName.ShouldBe("settings");
    }

    /// <summary>
    /// Verifies that the ParseLines method throws an ArgumentNullException when a null logger is provided.
    /// </summary>
    [TestMethod]
    public void ParseLines_Throws_WhenLoggerNull()
    {
        // Arrange
        var settings = new CSVSettings
        {
            LinesToSkip = 0,
            Delimiter = ',',
            TrimFields = true,
            HandleQuotedFields = false,
            FieldMappings = new Dictionary<string, CSVSettings.FieldRecord>
            {
                ["A"] = new("A", false, false)
            }
        };
        var lines = new[] { "A", "1" };

        // Act / Assert
        var ex = Should.Throw<ArgumentNullException>(() => CSVReader.ParseLines(lines, settings, null!).ToList());
        ex.ParamName.ShouldBe("logger");
    }

    /// <summary>
    /// Verifies that the CSV parser correctly handles empty fields resulting from consecutive delimiters in input
    /// lines.
    /// </summary>
    [TestMethod]
    public void ParseLines_HandlesEmptyFields_ForConsecutiveDelimiters()
    {
        // Arrange
        var settings = new CSVSettings
        {
            LinesToSkip = 0,
            Delimiter = ',',
            TrimFields = true,
            HandleQuotedFields = false,
            FieldMappings = new Dictionary<string, CSVSettings.FieldRecord>
            {
                ["A"] = new("A", false, false),
                ["B"] = new("B", false, false),
                ["C"] = new("C", false, false)
            }
        };

        var lines = new[]
        {
            "A,B,C",
            "1,,3"
        };

        // Act
        var records = CSVReader.ParseLines(lines, settings, _logger!).ToList();

        // Assert
        records.Count.ShouldBe(1);
        records[0].propertyMap["A"].ShouldBe("1");
        records[0].propertyMap["B"].ShouldBe(string.Empty);
        records[0].propertyMap["C"].ShouldBe("3");
    }

    /// <summary>
    /// Verifies that a warning is logged when the FieldMappings property in CSVSettings is null during line parsing.
    /// Otherwise it should handle it gracefully and not throw an exception, allowing the parsing to proceed without field mappings.
    /// </summary>
    [TestMethod]
    public void ParseLines_LogsWarning_WhenFieldMappingsNull()
    {
        // Arrange
        var settings = new CSVSettings
        {
            LinesToSkip = 0,
            Delimiter = ',',
            TrimFields = true,
            HandleQuotedFields = false,
            FieldMappings = null!
        };

        var lines = new[]
        {
            "A",
            "1"
        };

        // Act
        var records = CSVReader.ParseLines(lines, settings, _logger!).ToList();

        // Assert
        records.Count.ShouldBe(1);
        InMemorySink.Instance
            .Should()
            .HaveMessage()
            .Containing("No field mappings provided in settings")
            .ShouldMatchTestName()
            .WithLevel(LogEventLevel.Warning);
    }
}
