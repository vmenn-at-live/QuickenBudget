using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using QuickenBudget.Tools;

using Shouldly;

namespace QuickenBudget.Tests;

[TestClass]
public class ApplyLogDirectoryTests : TestBase
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Creates a ConfigurationManager pre-populated with the given key/value pairs.
    /// </summary>
    private static ConfigurationManager BuildConfig(Dictionary<string, string?> initial)
    {
        var config = new ConfigurationManager();
        config.AddInMemoryCollection(initial);
        return config;
    }

    private static string Combine(string dir, string file) => Path.Combine(dir, file);
    private static string Combine(string drive, string dir, string file) => Path.Combine(drive, dir, file);

    // ---------------------------------------------------------------------------
    // Existing File sink – directory replacement
    // ---------------------------------------------------------------------------

    [TestMethod]
    public void ApplyLogDirectory_ExistingFileSink_ReplacesDirectory()
    {
        var config = BuildConfig(new()
        {
            ["Serilog:WriteTo:0:Name"]           = "File",
            ["Serilog:WriteTo:0:Args:path"]       = Combine("c:", "old", "log-.txt"),
        });

        config.ApplyLogDirectory(Combine("d:", "new"));

        config["Serilog:WriteTo:0:Args:path"].ShouldBe(Combine("d:", "new", "log-.txt"));
    }

    [TestMethod]
    public void ApplyLogDirectory_ExistingFileSink_PreservesCustomFileName()
    {
        var config = BuildConfig(new()
        {
            ["Serilog:WriteTo:0:Name"]           = "File",
            ["Serilog:WriteTo:0:Args:path"]       = Combine("c:", "old", "myapp-.txt"),
        });

        config.ApplyLogDirectory(Combine("d:", "new"));

        config["Serilog:WriteTo:0:Args:path"].ShouldBe(Combine("d:", "new", "myapp-.txt"));
    }

    [TestMethod]
    public void ApplyLogDirectory_ExistingFileSink_AtNonZeroIndex_UpdatesCorrectEntry()
    {
        var config = BuildConfig(new()
        {
            ["Serilog:WriteTo:0:Name"]           = "Console",
            ["Serilog:WriteTo:1:Name"]           = "File",
            ["Serilog:WriteTo:1:Args:path"]       = Combine("c:", "old", "log-.txt"),
        });

        config.ApplyLogDirectory(Combine("d:", "new"));

        config["Serilog:WriteTo:1:Args:path"].ShouldBe(Combine("d:", "new", "log-.txt"));
    }

    [TestMethod]
    public void ApplyLogDirectory_ExistingFileSink_OtherSinkArgsAreUnchanged()
    {
        const string template = "[{Timestamp}] {Message}";
        var config = BuildConfig(new()
        {
            ["Serilog:WriteTo:0:Name"]                         = "Console",
            ["Serilog:WriteTo:0:Args:outputTemplate"]          = template,
            ["Serilog:WriteTo:1:Name"]                         = "File",
            ["Serilog:WriteTo:1:Args:path"]                    = Combine("c:", "old", "log-.txt"),
            ["Serilog:WriteTo:1:Args:rollingInterval"]         = "Hour",
        });

        config.ApplyLogDirectory(Combine("d:", "new"));

        config["Serilog:WriteTo:0:Args:outputTemplate"].ShouldBe(template);
        config["Serilog:WriteTo:1:Args:rollingInterval"].ShouldBe("Hour");
    }

    [TestMethod]
    public void ApplyLogDirectory_ExistingFileSink_NullPath_UsesDefaultFileName()
    {
        // File sink exists but has no path configured.
        var config = BuildConfig(new()
        {
            ["Serilog:WriteTo:0:Name"] = "File",
        });

        config.ApplyLogDirectory(Combine("d:", "logs"));

        config["Serilog:WriteTo:0:Args:path"].ShouldBe(Combine("d:", "logs", "log-.txt"));
    }

    // ---------------------------------------------------------------------------
    // No File sink – new sink is added
    // ---------------------------------------------------------------------------

    [TestMethod]
    public void ApplyLogDirectory_NoFileSink_EmptyConfig_AddsSinkAtIndex0()
    {
        var config = new ConfigurationManager();

        config.ApplyLogDirectory(Combine("d:", "logs"));

        config["Serilog:WriteTo:0:Name"].ShouldBe("File");
        config["Serilog:WriteTo:0:Args:path"].ShouldBe(Combine("d:", "logs", "log-.txt"));
    }

    [TestMethod]
    public void ApplyLogDirectory_NoFileSink_ExistingConsoleSink_AppendsSinkAtNextIndex()
    {
        var config = BuildConfig(new()
        {
            ["Serilog:WriteTo:0:Name"] = "Console",
        });

        config.ApplyLogDirectory(Combine("d:", "logs"));

        config["Serilog:WriteTo:1:Name"].ShouldBe("File");
        config["Serilog:WriteTo:1:Args:path"].ShouldBe(Combine("d:", "logs", "log-.txt"));
    }

    [TestMethod]
    public void ApplyLogDirectory_NoFileSink_SetsDefaultRollingArgs()
    {
        var config = new ConfigurationManager();

        config.ApplyLogDirectory(Combine("d:", "logs"));

        config["Serilog:WriteTo:0:Args:rollOnFileSizeLimit"].ShouldBe("True");
        config["Serilog:WriteTo:0:Args:rollingInterval"].ShouldBe("Day");
        config["Serilog:WriteTo:0:Args:fileSizeLimitBytes"].ShouldBe("1000000");
    }

    // ---------------------------------------------------------------------------
    // Serilog:Using management when no File sink exists
    // ---------------------------------------------------------------------------

    [TestMethod]
    public void ApplyLogDirectory_NoFileSink_NoUsingSection_AddsSerilogSinksFile()
    {
        var config = new ConfigurationManager();

        config.ApplyLogDirectory(Combine("d:", "logs"));

        var usingValues = config.GetSection("Serilog:Using").GetChildren().Select(c => c.Value).ToList();
        usingValues.ShouldContain("Serilog.Sinks.File");
    }

    [TestMethod]
    public void ApplyLogDirectory_NoFileSink_UsingAlreadyContainsSinksFile_NoDuplicate()
    {
        var config = BuildConfig(new()
        {
            ["Serilog:Using:0"] = "Serilog.Sinks.Console",
            ["Serilog:Using:1"] = "Serilog.Sinks.File",
        });

        config.ApplyLogDirectory(Combine("d:", "logs"));

        var usingValues = config.GetSection("Serilog:Using").GetChildren().Select(c => c.Value).ToList();
        usingValues.Count(v => v == "Serilog.Sinks.File").ShouldBe(1);
    }

    [TestMethod]
    public void ApplyLogDirectory_ExistingFileSink_DoesNotModifyUsingSection()
    {
        var config = BuildConfig(new()
        {
            ["Serilog:Using:0"]           = "Serilog.Sinks.Console",
            ["Serilog:WriteTo:0:Name"]    = "File",
            ["Serilog:WriteTo:0:Args:path"] = Combine("c:", "old", "log-.txt"),
        });

        ConfigurationHelpers.ApplyLogDirectory(config, Combine("d:", "new"));

        var usingValues = config.GetSection("Serilog:Using").GetChildren().Select(c => c.Value).ToList();
        usingValues.ShouldBe(["Serilog.Sinks.Console"]);
    }
}
