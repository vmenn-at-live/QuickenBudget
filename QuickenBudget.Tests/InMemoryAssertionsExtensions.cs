using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using Serilog.Events;
using Serilog.Sinks.InMemory.Assertions;

namespace QuickenBudget.Tests;

public static class InMemoryAssertionsExtensions
{
    /// <summary>
    /// Validates that the log events in the sink have a "TestName" property with a value matching the caller's member name.
    /// This is useful for ensuring that log events are correctly associated with the test method that generated them,
    /// allowing for more precise assertions in tests that involve logging.
    /// </summary>
    /// <param name="sink"></param>
    /// <param name="caller">Optional default value required for the CallerMemberName attribute, representing the caller's member name.</param>
    /// <returns></returns>
    public static LogEventsAssertions ShouldMatchTestName(this LogEventsAssertions sink, [CallerMemberName] string? caller = null)
    {
        return sink
            .WithProperty("TestName")
            .WithValues([caller]);
    }

    public static IEnumerable<LogEvent> SelectLogEventsForThisTest(this IEnumerable<LogEvent>  events, LogEventLevel level, [CallerMemberName] string? caller = null)
    {
        return events.Where(e => e.Level == level
                && e.Properties.TryGetValue("TestName", out var v)
                && v is ScalarValue scalarValue && scalarValue.Value is string testName
                && testName.Equals(caller, System.StringComparison.Ordinal));
    }

}
