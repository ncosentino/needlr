using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Tests.Sequential;

/// <summary>
/// Tests for the <see cref="StageTermination"/> wiring on
/// <see cref="StageExecutionResult"/> static factories. Verifies that:
/// <list type="bullet">
///   <item><see cref="StageExecutionResult.Failed"/> auto-populates
///     <c>Termination = new StageTermination.Failed(exception)</c> when the
///     caller does not supply one.</item>
///   <item><see cref="StageExecutionResult.Skipped"/> populates
///     <c>Termination = new StageTermination.Skipped(reason)</c> with the
///     optional reason.</item>
///   <item>An explicitly supplied <c>termination</c> overrides the
///     auto-populated default.</item>
///   <item>Direct record construction with no termination produces
///     <c>Termination == null</c> (legacy behaviour).</item>
/// </list>
/// </summary>
public sealed class StageExecutionResultTerminationTests
{
    [Fact]
    public void Success_NoTermination_IsNull()
    {
        var result = StageExecutionResult.Success("Stage", diagnostics: null, responseText: "ok");
        Assert.Null(result.Termination);
    }

    [Fact]
    public void Success_WithTermination_PreservesIt()
    {
        var termination = new StageTermination.NaturalCompletion();
        var result = StageExecutionResult.Success("Stage", diagnostics: null, responseText: "ok", termination: termination);
        Assert.Same(termination, result.Termination);
    }

    [Fact]
    public void Failed_NoTermination_AutoPopulatesFailedFromException()
    {
        var ex = new InvalidOperationException("boom");
        var result = StageExecutionResult.Failed("Stage", ex);

        var termination = Assert.IsType<StageTermination.Failed>(result.Termination);
        Assert.Same(ex, termination.Exception);
    }

    [Fact]
    public void Failed_WithExplicitTermination_OverridesAutoPopulated()
    {
        var ex = new InvalidOperationException("boom");
        var explicitTermination = new StageTermination.Cancelled();
        var result = StageExecutionResult.Failed("Stage", ex, termination: explicitTermination);

        Assert.Same(explicitTermination, result.Termination);
    }

    [Fact]
    public void Skipped_NoReason_PopulatesSkippedWithNullReason()
    {
        var result = StageExecutionResult.Skipped("Stage");

        var termination = Assert.IsType<StageTermination.Skipped>(result.Termination);
        Assert.Null(termination.Reason);
    }

    [Fact]
    public void Skipped_WithReason_PopulatesSkippedWithReason()
    {
        var result = StageExecutionResult.Skipped("Stage", reason: "no work to do");

        var termination = Assert.IsType<StageTermination.Skipped>(result.Termination);
        Assert.Equal("no work to do", termination.Reason);
    }

    [Fact]
    public void DirectRecordConstruction_NoTermination_IsNull()
    {
        var result = new StageExecutionResult(
            StageName: "Stage",
            Diagnostics: null,
            ResponseText: "ok",
            Succeeded: true);

        Assert.Null(result.Termination);
    }

    [Fact]
    public void DirectRecordConstruction_WithTermination_PreservesIt()
    {
        var termination = new StageTermination.MaxIterationsReached(Limit: 10, IterationsUsed: 10);
        var result = new StageExecutionResult(
            StageName: "Stage",
            Diagnostics: null,
            ResponseText: "ok",
            Succeeded: true,
            Termination: termination);

        Assert.Same(termination, result.Termination);
    }
}
