/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System;
using System.IO;
using System.Text.RegularExpressions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using QuickenBudget.Models;

using Shouldly;

namespace QuickenBudget.Tests;

[TestClass]
public class ParsingExceptionTests : TestBase
{
    #region Message — string-only constructor

    [TestMethod]
    public void Message_WithMessageOnly_ReturnsMessage()
    {
        var ex = new ParsingException("something went wrong");

        ex.Message.ShouldBe("something went wrong");
    }

    [TestMethod]
    public void Message_WithMessageAndPositiveLine_ReturnsLinePrefixedMessage()
    {
        var ex = new ParsingException("bad field", line: 10);

        ex.Message.ShouldBe("Line 10: bad field");
    }

    [TestMethod]
    public void Message_WithLineZero_ReturnsLinePrefixedMessage()
    {
        var ex = new ParsingException("bad field", line: 0);

        ex.Message.ShouldBe("Line 0: bad field");
    }

    [TestMethod]
    public void Message_WithNegativeLine_ReturnsMessageWithNoPrefix()
    {
        var ex = new ParsingException("bad field", line: -1);

        ex.Message.ShouldBe("bad field");
    }

    #endregion

    #region Message — Exception-wrapping constructor (no explicit message)

    [TestMethod]
    public void Message_WithInnerException_ReturnsInnerExceptionMessage()
    {
        var inner = new IOException("file not found");
        var ex = new ParsingException(inner);

        ex.Message.ShouldBe("file not found");
    }

    [TestMethod]
    public void Message_WithInnerExceptionAndLine_ReturnsLinePrefixedInnerMessage()
    {
        var inner = new IOException("file not found");
        var ex = new ParsingException(inner, line: 3);

        ex.Message.ShouldBe("Line 3: file not found");
    }

    #endregion

    #region Message — string + Exception constructor

    [TestMethod]
    public void Message_WithExplicitMessageAndInnerException_ReturnsExplicitMessage()
    {
        var inner = new IOException("low-level error");
        var ex = new ParsingException("high-level error", inner, line: 5);

        ex.Message.ShouldBe("Line 5: high-level error");
    }

    #endregion

    #region Message — RegexMatchTimeoutException inner

    [TestMethod]
    public void Message_WithRegexTimeoutInner_ReturnsTimeoutDescription()
    {
        var timeout = TimeSpan.FromMilliseconds(250);
        var regexEx = new RegexMatchTimeoutException("some input", "bad.*pattern", timeout);
        var ex = new ParsingException(regexEx);

        ex.Message.ShouldBe($"Regex match timed out after {timeout}. Pattern: bad.*pattern.");
    }

    [TestMethod]
    public void Message_WithRegexTimeoutInnerAndLine_ReturnsLinePrefixedTimeoutDescription()
    {
        var timeout = TimeSpan.FromMilliseconds(250);
        var regexEx = new RegexMatchTimeoutException("some input", "bad.*pattern", timeout);
        var ex = new ParsingException(regexEx, line: 7);

        ex.Message.ShouldBe($"Line 7: Regex match timed out after {timeout}. Pattern: bad.*pattern.");
    }

    #endregion
}
