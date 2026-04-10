using Microsoft.VisualStudio.TestTools.UnitTesting;

using QuickenBudget.Tools;

using Shouldly;

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace QuickenBudget.Tests;

[TestClass]
public class CommandLineOptionsTests : TestBase
{
    [TestMethod]
    public void Parse_NoArgs_Defaults()
    {
        var opts = CommandLineOptions.Parse([]);

        opts.ShouldNotBeNull();
        opts.Continue.ShouldBeTrue();
        opts.ReportFile.ShouldBeNull();
        opts.ConfigFiles.Length.ShouldBe(0);
        opts.Port.ShouldBeNull();
    }

    [TestMethod]
    public void Parse_ReportFileExists_SetsReportFile()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            var opts = CommandLineOptions.Parse([tmp]);

            opts.ReportFile.ShouldNotBeNull();
            opts.ReportFile!.FullName.ShouldBe(Path.GetFullPath(tmp));
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [TestMethod]
    public void Parse_ConfigFilesCollected()
    {
        var f1 = Path.GetTempFileName();
        var f2 = Path.GetTempFileName();
        try
        {
            var opts = CommandLineOptions.Parse(["--config", f1, "--config", f2]);

            opts.ConfigFiles.Length.ShouldBe(2);
            var expected = new[] { Path.GetFullPath(f1), Path.GetFullPath(f2) };
            opts.ConfigFiles.Select(f => f.FullName).ShouldBe(expected, Case.Insensitive);
        }
        finally
        {
            File.Delete(f1);
            File.Delete(f2);
        }
    }

    [TestMethod]
    public void Parse_Port_SetsPort()
    {
        var opts = CommandLineOptions.Parse(["--port", "4321"]);

        opts.Port.ShouldBe(4321);
    }

    [TestMethod]
    public void Parse_MissingReportFile_ThrowsArgumentException()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt");

        var ex = Should.Throw<ArgumentException>(() => CommandLineOptions.Parse([missing]));
        ex.Message.ShouldContain("Report file does not exist");
    }

    [TestMethod]
    public void Parse_MissingConfigFile_ThrowsArgumentException()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".cfg");

        var ex = Should.Throw<ArgumentException>(() => CommandLineOptions.Parse(["--config", missing]));
        ex.Message.ShouldContain("--config file(s) do not exist");
    }

    [TestMethod]
    public void Parse_PortOutOfRange_ThrowsArgumentException()
    {
        var ex = Should.Throw<ArgumentException>(() => CommandLineOptions.Parse(["--port", "70000"]));
        ex.Message.ShouldContain("Port must be between 1 and 65535");
    }

    [TestMethod]
    public void Parse_ConfigWithMultipleValuesOnAlias_CollectsAll()
    {
        var f1 = Path.GetTempFileName();
        var f2 = Path.GetTempFileName();
        try
        {
            var opts = CommandLineOptions.Parse(["-c", f1, f2]);
            opts.ConfigFiles.Length.ShouldBe(2);
            opts.ConfigFiles[0].FullName.ShouldBe(Path.GetFullPath(f1));
            opts.ConfigFiles[1].FullName.ShouldBe(Path.GetFullPath(f2));
        }
        finally
        {
            File.Delete(f1);
            File.Delete(f2);
        }
    }

    [TestMethod]
    public void Parse_DuplicateConfigFiles_DeduplicatesCaseInsensitively()
    {
        var f1 = Path.GetTempFileName();
        try
        {
            // Provide the same file twice with different casing to exercise case-insensitivity.
            var f1Upper = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? f1 : Path.GetFullPath(f1).ToUpperInvariant();

            var opts = CommandLineOptions.Parse(["--config", f1, "--config", f1Upper]);

            // Parser removes duplicates.
            opts.ConfigFiles.Length.ShouldBe(1);
        }
        finally
        {
            File.Delete(f1);
        }
    }

    [TestMethod]
    public void Parse_LogDirectory_SetsLogDirectory()
    {
        var opts = CommandLineOptions.Parse(["--logDirectory", "c:/logs"]);

        opts.LogDirectory.ShouldBe("c:/logs");
    }

    [TestMethod]
    public void Parse_LogDirectoryAlias_SetsLogDirectory()
    {
        var opts = CommandLineOptions.Parse(["-ld", "c:/logs"]);

        opts.LogDirectory.ShouldBe("c:/logs");
    }

    [TestMethod]
    public void Parse_NoLogDirectory_LogDirectoryIsNull()
    {
        var opts = CommandLineOptions.Parse([]);

        opts.LogDirectory.ShouldBeNull();
    }

    [TestMethod]
    public void Parse_InvalidLogDirectory_ThrowsArgumentException()
    {
        // A path containing a null character is invalid on all platforms.
        var ex = Should.Throw<ArgumentException>(() => CommandLineOptions.Parse(["--logDirectory", "c:/\0invalid"]));
        ex.Message.ShouldContain("Invalid log directory");
    }
}
