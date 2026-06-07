using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using NexusLabs.Needlr.Logging;

using Xunit;

namespace NexusLabs.Needlr.Logging.Tests;

/// <summary>
/// Unit tests for the <see cref="NeedlrCancellationLogging"/> policy surface.
/// </summary>
public sealed class NeedlrCancellationLoggingTests : IDisposable
{
    public NeedlrCancellationLoggingTests()
    {
        ClearEnvironment();
        NeedlrCancellationLogging.ResetForTests();
    }

    public void Dispose()
    {
        ClearEnvironment();
        NeedlrCancellationLogging.ResetForTests();
    }

    [Theory]
    [InlineData("skip", CancellationLoggingBehavior.Skip)]
    [InlineData("SKIP", CancellationLoggingBehavior.Skip)]
    [InlineData("log", CancellationLoggingBehavior.Log)]
    [InlineData("Log", CancellationLoggingBehavior.Log)]
    [InlineData("demote", CancellationLoggingBehavior.Demote)]
    [InlineData("  demote  ", CancellationLoggingBehavior.Demote)]
    public void ParseBehavior_RecognizesKnownValues(string raw, CancellationLoggingBehavior expected)
    {
        Assert.Equal(expected, NeedlrCancellationLogging.ParseBehavior(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nonsense")]
    public void ParseBehavior_FallsBackToSkip(string? raw)
    {
        Assert.Equal(CancellationLoggingBehavior.Skip, NeedlrCancellationLogging.ParseBehavior(raw));
    }

    [Theory]
    [InlineData("Trace", LogLevel.Trace)]
    [InlineData("debug", LogLevel.Debug)]
    [InlineData("  Warning  ", LogLevel.Warning)]
    [InlineData("Critical", LogLevel.Critical)]
    public void ParseDemotedLevel_RecognizesKnownValues(string raw, LogLevel expected)
    {
        Assert.Equal(expected, NeedlrCancellationLogging.ParseDemotedLevel(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-level")]
    [InlineData("9999")]
    public void ParseDemotedLevel_FallsBackToDebug(string? raw)
    {
        Assert.Equal(LogLevel.Debug, NeedlrCancellationLogging.ParseDemotedLevel(raw));
    }

    [Fact]
    public void Behavior_DefaultsToSkip_WhenNoEnvironmentVariable()
    {
        Assert.Equal(CancellationLoggingBehavior.Skip, NeedlrCancellationLogging.Behavior);
    }

    [Fact]
    public void Behavior_ReadsEnvironmentDefault_Once()
    {
        Environment.SetEnvironmentVariable(NeedlrCancellationLogging.BehaviorEnvironmentVariable, "demote");
        NeedlrCancellationLogging.ResetForTests();

        Assert.Equal(CancellationLoggingBehavior.Demote, NeedlrCancellationLogging.Behavior);

        // Changing the variable after the first read has no effect because the value is cached.
        Environment.SetEnvironmentVariable(NeedlrCancellationLogging.BehaviorEnvironmentVariable, "log");
        Assert.Equal(CancellationLoggingBehavior.Demote, NeedlrCancellationLogging.Behavior);
    }

    [Fact]
    public void DemotedLevel_ReadsEnvironmentDefault()
    {
        Environment.SetEnvironmentVariable(NeedlrCancellationLogging.DemotedLevelEnvironmentVariable, "Trace");
        NeedlrCancellationLogging.ResetForTests();

        Assert.Equal(LogLevel.Trace, NeedlrCancellationLogging.DemotedLevel);
    }

    [Fact]
    public void Behavior_Setter_OverridesEnvironmentDefault()
    {
        Environment.SetEnvironmentVariable(NeedlrCancellationLogging.BehaviorEnvironmentVariable, "skip");
        NeedlrCancellationLogging.ResetForTests();

        NeedlrCancellationLogging.Behavior = CancellationLoggingBehavior.Log;

        Assert.Equal(CancellationLoggingBehavior.Log, NeedlrCancellationLogging.Behavior);
    }

    [Fact]
    public void DemotedLevel_Setter_OverridesEnvironmentDefault()
    {
        NeedlrCancellationLogging.DemotedLevel = LogLevel.Error;

        Assert.Equal(LogLevel.Error, NeedlrCancellationLogging.DemotedLevel);
    }

    [Fact]
    public void IsCancellation_DefaultPredicate_TreatsOperationCanceledAsCancellation()
    {
        Assert.True(NeedlrCancellationLogging.IsCancellation(new OperationCanceledException()));
        Assert.True(NeedlrCancellationLogging.IsCancellation(new TaskCanceledException()));
        Assert.False(NeedlrCancellationLogging.IsCancellation(new InvalidOperationException()));
    }

    [Fact]
    public void IsCancellation_NullException_ReturnsFalse()
    {
        Assert.False(NeedlrCancellationLogging.IsCancellation(null));
    }

    [Fact]
    public void IsCancellationPredicate_CanBeOverridden()
    {
        NeedlrCancellationLogging.IsCancellationPredicate = exception => exception is InvalidOperationException;

        Assert.True(NeedlrCancellationLogging.IsCancellation(new InvalidOperationException()));
        Assert.False(NeedlrCancellationLogging.IsCancellation(new OperationCanceledException()));
    }

    [Fact]
    public void IsCancellationPredicate_NullValue_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => NeedlrCancellationLogging.IsCancellationPredicate = null!);
    }

    [Fact]
    public void ResetForTests_ClearsOverrides()
    {
        NeedlrCancellationLogging.Behavior = CancellationLoggingBehavior.Log;
        NeedlrCancellationLogging.DemotedLevel = LogLevel.Critical;

        NeedlrCancellationLogging.ResetForTests();

        Assert.Equal(CancellationLoggingBehavior.Skip, NeedlrCancellationLogging.Behavior);
        Assert.Equal(LogLevel.Debug, NeedlrCancellationLogging.DemotedLevel);
    }

    private static void ClearEnvironment()
    {
        Environment.SetEnvironmentVariable(NeedlrCancellationLogging.BehaviorEnvironmentVariable, null);
        Environment.SetEnvironmentVariable(NeedlrCancellationLogging.DemotedLevelEnvironmentVariable, null);
    }
}
