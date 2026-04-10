/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System;
using System.Text.RegularExpressions;

namespace QuickenBudget.Models;

/// <summary>
/// Wraps other exceptions into this class. This allows us to include line number information and to provide more context for exceptions that occur during parsing of transaction data.
/// </summary>
public class ParsingException : Exception
{
    public int Line { get; }

    public ParsingException(string message, int line = -1) : base(message)
    {
        Line = line;
    }

    public ParsingException(string message, Exception innerException, int line = -1) : base(message, innerException)
    {
        Line = line;
    }

    public ParsingException(Exception innerException, int line = -1) : base(innerException?.Message ?? string.Empty, innerException)
    {
        ArgumentNullException.ThrowIfNull(innerException);
        Line = line;
    }

    public override string Message
    {
        get
        {
            var innerException = InnerException;
            string message = Line >= 0 ? $"Line {Line}: " : "";
            if (innerException is RegexMatchTimeoutException regexEx)
            {
                message += $"Regex match timed out after {regexEx.MatchTimeout}. Pattern: {regexEx.Pattern}.";
            }
            else
            {
                message += string.IsNullOrEmpty(base.Message) ? InnerException?.Message : base.Message;
            }

            return message;
        }
    }
}
