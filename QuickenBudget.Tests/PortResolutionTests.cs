using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using System.Collections.Generic;

using QuickenBudget.Tools;

namespace QuickenBudget.Tests;

[TestClass]
public class PortResolutionTests : TestBase
{
    private const int DefaultPort = ConfigurationHelpers.DefaultPort;

    private static IConfiguration BuildConfig(Dictionary<string, string?> values)
    {
        var config = new ConfigurationManager();
        config.AddInMemoryCollection(values);
        return config;
    }

    [TestMethod]
    public void ResolvePort_CommandLine_TakesPrecedenceOverConfig()
    {
        var config = BuildConfig(new() { ["Port"] = "9090" });
        config.TryResolvePort(5000, out int port).ShouldBeTrue();
        port.ShouldBe(5000);
    }

    [TestMethod]
    public void ResolvePort_CommandLine_TakesPrecedenceOverDefault()
    {
        BuildConfig([]).TryResolvePort(3000, out int port).ShouldBeTrue();  
        port.ShouldBe(3000);
    }

    [TestMethod]
    public void ResolvePort_Config_UsedWhenNoCommandLinePort()
    {
        var config = BuildConfig(new() { ["Port"] = "9090" });
        config.TryResolvePort(null, out int port).ShouldBeTrue();
        port.ShouldBe(9090);
    }

    [TestMethod]
    public void ResolvePort_Default_UsedWhenNeitherCommandLineNorConfig()
    {
        BuildConfig([]).TryResolvePort(null, out int port).ShouldBeTrue();
        port.ShouldBe(DefaultPort);
    }

    [TestMethod]
    public void ResolvePort_Default_Is8080()
    {
        DefaultPort.ShouldBe(8080);
    }

    [TestMethod]
    public void ResolvePort_Config_MissingKey_FallsBackToDefault()
    {
        var config = BuildConfig(new() { ["SomeOtherKey"] = "1234" });
        config.TryResolvePort(null, out int port).ShouldBeTrue();
        port.ShouldBe(DefaultPort);
    }

    [TestMethod]
    public void ResolvePort_Config_WhitespaceValue_FallsBackToDefault()
    {
        BuildConfig(new() { ["Port"] = "   " }).TryResolvePort(null, out int port).ShouldBeTrue();
        port.ShouldBe(DefaultPort);
    }

    [TestMethod]
    public void ResolvePort_Config_ZeroPort_IsInvalid()
    {
        BuildConfig(new() { ["Port"] = "0" }).TryResolvePort(null, out int _).ShouldBeFalse();  
    }

    [TestMethod]
    public void ResolvePort_CommandLine_ZeroPort_IsInvalid()
    {
        BuildConfig([]).TryResolvePort(0, out int _).ShouldBeFalse();
    }

    [TestMethod]
    public void ResolvePort_Config_PortTooHigh_IsInvalid()
    {
        BuildConfig(new() { ["Port"] = "70000" }).TryResolvePort(null, out int _).ShouldBeFalse();
    }

    [TestMethod]
    public void ResolvePort_CommandLine_PortTooHigh_IsInvalid()
    {
        BuildConfig([]).TryResolvePort(70000, out int _).ShouldBeFalse();
    }

    [TestMethod]
    public void ResolvePort_Config_NegativePort_IsInvalid()
    {
        BuildConfig(new() { ["Port"] = "-1" }).TryResolvePort(null, out int _).ShouldBeFalse();
    }

    [TestMethod]
    public void ResolvePort_CommandLine_NegativePort_IsInvalid()
    {
        BuildConfig([]).TryResolvePort(-1, out int _).ShouldBeFalse();
    }

    [TestMethod]
    public void ResolvePort_Config_NonNumericValue_IsInvalid()
    {
        BuildConfig(new() { ["Port"] = "abc" }).TryResolvePort(null, out int _).ShouldBeFalse();
    }

    [TestMethod]
    public void ResolvePort_Config_BoundaryLow_IsValid()
    {
        BuildConfig(new() { ["Port"] = "1" }).TryResolvePort(null, out int port).ShouldBeTrue();
        port.ShouldBe(1);
    }

    [TestMethod]
    public void ResolvePort_Config_BoundaryHigh_IsValid()
    {
        BuildConfig(new() { ["Port"] = "65535" }).TryResolvePort(null, out int port).ShouldBeTrue();
        port.ShouldBe(65535);
    }
}
