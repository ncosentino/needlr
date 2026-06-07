using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Logging;

using NexusLabs.Needlr.Logging;

using Xunit;

namespace NexusLabs.Needlr.Logging.Tests;

/// <summary>
/// End-to-end tests that build the generated <c>[NeedlrLoggerMessage]</c> bodies and verify their
/// runtime behavior.
/// </summary>
public sealed class NeedlrLoggerMessageGeneratorTests : IDisposable
{
    public NeedlrLoggerMessageGeneratorTests()
    {
        NeedlrCancellationLogging.ResetForTests();
    }

    public void Dispose()
    {
        NeedlrCancellationLogging.ResetForTests();
    }

    [Fact]
    public void InstanceMethod_NonCancellationException_LogsEntry()
    {
        var logger = new CapturingLogger();
        var target = new InstanceLogTarget(logger);
        var error = new InvalidOperationException("boom");

        target.LogFailed(5, error);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(7, entry.EventId.Id);
        Assert.Equal("LogFailed", entry.EventId.Name);
        Assert.Equal("Failed 5", entry.Message);
        Assert.Same(error, entry.Exception);
    }

    [Fact]
    public void InstanceMethod_Cancellation_DefaultBehaviorSkips()
    {
        var logger = new CapturingLogger();
        var target = new InstanceLogTarget(logger);

        target.LogFailed(5, new OperationCanceledException());

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void Cancellation_BehaviorLog_LogsNormally()
    {
        NeedlrCancellationLogging.Behavior = CancellationLoggingBehavior.Log;
        var logger = new CapturingLogger();
        var target = new InstanceLogTarget(logger);

        target.LogFailed(5, new OperationCanceledException());

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("Failed 5", entry.Message);
    }

    [Fact]
    public void Cancellation_BehaviorDemote_LogsAtDemotedLevel()
    {
        NeedlrCancellationLogging.Behavior = CancellationLoggingBehavior.Demote;
        NeedlrCancellationLogging.DemotedLevel = LogLevel.Trace;
        var logger = new CapturingLogger();
        var target = new InstanceLogTarget(logger);
        var cancellation = new OperationCanceledException();

        target.LogFailed(5, cancellation);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Trace, entry.Level);
        Assert.Equal(7, entry.EventId.Id);
        Assert.Equal("Failed 5", entry.Message);
        Assert.Same(cancellation, entry.Exception);
    }

    [Fact]
    public void StaticMethod_LogsViaParameterLogger()
    {
        var logger = new CapturingLogger();

        StaticLogTarget.LogBoom(logger, 42, new InvalidOperationException());

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("Boom 42", entry.Message);
    }

    [Fact]
    public void StaticMethod_Cancellation_DefaultSkips()
    {
        var logger = new CapturingLogger();

        StaticLogTarget.LogBoom(logger, 42, new OperationCanceledException());

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void MethodWithoutException_AlwaysLogs_EvenWhenBehaviorWouldSkip()
    {
        NeedlrCancellationLogging.Behavior = CancellationLoggingBehavior.Skip;
        var logger = new CapturingLogger();
        var target = new InstanceLogTarget(logger);

        target.LogPlain("abc");

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("Plain abc", entry.Message);
    }

    [Fact]
    public void CustomPredicate_RedefinesWhatCountsAsCancellation()
    {
        NeedlrCancellationLogging.IsCancellationPredicate = exception => exception is InvalidOperationException;
        var logger = new CapturingLogger();
        var target = new InstanceLogTarget(logger);

        // The custom predicate now treats InvalidOperationException as cancellation -> skipped.
        target.LogFailed(1, new InvalidOperationException());
        Assert.Empty(logger.Entries);

        // ...and an OperationCanceledException is no longer treated as cancellation -> logged.
        target.LogFailed(2, new OperationCanceledException());
        Assert.Single(logger.Entries);
    }

    [Fact]
    public void SkipEnabledCheck_LogsEvenWhenLoggerDisabled()
    {
        var logger = new CapturingLogger { Enabled = false };
        var target = new InstanceLogTarget(logger);

        target.LogSkip(3);
        Assert.Single(logger.Entries);

        // A method without SkipEnabledCheck respects IsEnabled and stays silent.
        target.LogPlain("x");
        Assert.Single(logger.Entries);
    }

    [Fact]
    public void MultipleMessageParameters_RenderInOrder()
    {
        var logger = new CapturingLogger();
        var target = new InstanceLogTarget(logger);

        target.LogTwo("left", 2);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal("left-2", entry.Message);
    }

    [Fact]
    public void ExplicitEventName_IsUsed()
    {
        var logger = new CapturingLogger();
        var target = new InstanceLogTarget(logger);

        target.LogNamed("v");

        var entry = Assert.Single(logger.Entries);
        Assert.Equal("custom-event", entry.EventId.Name);
    }

    [Fact]
    public void MoreThanSixParameters_LogsViaFallback()
    {
        var logger = new CapturingLogger();
        var target = new InstanceLogTarget(logger);

        target.LogSeven(1, 2, 3, 4, 5, 6, 7);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("1234567", entry.Message);
    }

    [Fact]
    public void MoreThanSixParameters_WithException_FallbackHonorsCancellationGuard()
    {
        var logger = new CapturingLogger();
        var target = new InstanceLogTarget(logger);
        var error = new InvalidOperationException();

        target.LogManyWithError(1, 2, 3, 4, 5, 6, 7, error);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(13, entry.EventId.Id);
        Assert.Equal("Many 1234567", entry.Message);
        Assert.Same(error, entry.Exception);

        // The fallback path still skips cancellations by default.
        logger.Entries.Clear();
        target.LogManyWithError(1, 2, 3, 4, 5, 6, 7, new OperationCanceledException());
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void PropertyLogger_IsUsedForInstanceMethods()
    {
        var logger = new CapturingLogger();
        var target = new PropertyLoggerTarget(logger);

        target.LogProp(9);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal("Prop 9", entry.Message);
    }

    [Fact]
    public void NullableStringParameter_HandlesNull()
    {
        var logger = new CapturingLogger();
        var target = new InstanceLogTarget(logger);

        target.LogMaybe(null);

        var entry = Assert.Single(logger.Entries);
        Assert.StartsWith("Maybe", entry.Message);
    }

    [Fact]
    public void NestedType_GeneratesAndLogs()
    {
        var logger = new CapturingLogger();
        var target = new OuterTarget.InnerTarget(logger);

        target.LogNested(11, new InvalidOperationException());

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("Nested 11", entry.Message);
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = new();

        public bool Enabled { get; set; } = true;

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull => new Scope();

        public bool IsEnabled(LogLevel logLevel) => Enabled;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception), exception));
        }

        private sealed class Scope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed record LogEntry(LogLevel Level, EventId EventId, string Message, Exception? Exception);
}

public partial class InstanceLogTarget
{
    private readonly ILogger _logger;

    public InstanceLogTarget(ILogger logger) => _logger = logger;

    [NeedlrLoggerMessage(EventId = 7, Level = LogLevel.Warning, Message = "Failed {Count}")]
    public partial void LogFailed(int count, Exception error);

    [NeedlrLoggerMessage(Level = LogLevel.Information, Message = "Plain {Name}")]
    public partial void LogPlain(string name);

    [NeedlrLoggerMessage(Level = LogLevel.Debug, Message = "Skip {X}", SkipEnabledCheck = true)]
    public partial void LogSkip(int x);

    [NeedlrLoggerMessage(Level = LogLevel.Information, Message = "{First}-{Second}")]
    public partial void LogTwo(string first, int second);

    [NeedlrLoggerMessage(Level = LogLevel.Information, EventName = "custom-event", Message = "Named {Value}")]
    public partial void LogNamed(string value);

    [NeedlrLoggerMessage(Level = LogLevel.Information, Message = "{A}{B}{C}{D}{E}{F}{G}")]
    public partial void LogSeven(int a, int b, int c, int d, int e, int f, int g);

    [NeedlrLoggerMessage(EventId = 13, Level = LogLevel.Warning, Message = "Many {A}{B}{C}{D}{E}{F}{G}")]
    public partial void LogManyWithError(int a, int b, int c, int d, int e, int f, int g, Exception error);

    [NeedlrLoggerMessage(Level = LogLevel.Information, Message = "Maybe {Value}")]
    public partial void LogMaybe(string? value);
}

public partial class PropertyLoggerTarget
{
    private ILogger Logger { get; }

    public PropertyLoggerTarget(ILogger logger) => Logger = logger;

    [NeedlrLoggerMessage(Level = LogLevel.Information, Message = "Prop {Value}")]
    public partial void LogProp(int value);
}

public static partial class StaticLogTarget
{
    [NeedlrLoggerMessage(Level = LogLevel.Error, Message = "Boom {Code}")]
    public static partial void LogBoom(ILogger logger, int code, Exception error);
}

public partial class OuterTarget
{
    public partial class InnerTarget
    {
        private readonly ILogger _logger;

        public InnerTarget(ILogger logger) => _logger = logger;

        [NeedlrLoggerMessage(EventId = 99, Level = LogLevel.Error, Message = "Nested {Value}")]
        public partial void LogNested(int value, Exception error);
    }
}
