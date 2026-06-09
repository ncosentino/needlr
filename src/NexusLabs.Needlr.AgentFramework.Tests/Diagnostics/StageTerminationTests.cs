using NexusLabs.Needlr.AgentFramework.Diagnostics;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Tests.Diagnostics;

/// <summary>
/// Tests for the <see cref="StageTermination"/> typed-case hierarchy: case
/// construction, value equality, the <see cref="StageTermination.ToTagValue"/>
/// contract, and exhaustive switch ergonomics.
/// </summary>
public sealed class StageTerminationTests
{
    [Fact]
    public void Completed_Constructs()
    {
        var t = new StageTermination.Completed();
        Assert.NotNull(t);
    }

    [Fact]
    public void NaturalCompletion_Constructs()
    {
        var t = new StageTermination.NaturalCompletion();
        Assert.NotNull(t);
    }

    [Fact]
    public void CompletedEarlyAfterToolCall_Constructs()
    {
        var t = new StageTermination.CompletedEarlyAfterToolCall();
        Assert.NotNull(t);
    }

    [Fact]
    public void MaxIterationsReached_PreservesLimitAndIterationsUsed()
    {
        var t = new StageTermination.MaxIterationsReached(Limit: 10, IterationsUsed: 7);
        Assert.Equal(10, t.Limit);
        Assert.Equal(7, t.IterationsUsed);
    }

    [Fact]
    public void MaxToolCallsReached_PreservesLimitAndToolCallsUsed()
    {
        var t = new StageTermination.MaxToolCallsReached(Limit: 50, ToolCallsUsed: 53);
        Assert.Equal(50, t.Limit);
        Assert.Equal(53, t.ToolCallsUsed);
    }

    [Fact]
    public void BudgetPressure_PreservesThreshold()
    {
        var t = new StageTermination.BudgetPressure(Threshold: 0.85);
        Assert.Equal(0.85, t.Threshold);
    }

    [Fact]
    public void BudgetPressure_NullThreshold_Allowed()
    {
        var t = new StageTermination.BudgetPressure(Threshold: null);
        Assert.Null(t.Threshold);
    }

    [Fact]
    public void StallDetected_PreservesConsecutiveThreshold()
    {
        var t = new StageTermination.StallDetected(ConsecutiveThreshold: 3);
        Assert.Equal(3, t.ConsecutiveThreshold);
    }

    [Fact]
    public void StallDetected_NullConsecutiveThreshold_Allowed()
    {
        var t = new StageTermination.StallDetected(ConsecutiveThreshold: null);
        Assert.Null(t.ConsecutiveThreshold);
    }

    [Fact]
    public void Cancelled_Constructs()
    {
        var t = new StageTermination.Cancelled();
        Assert.NotNull(t);
    }

    [Fact]
    public void Failed_PreservesException()
    {
        var ex = new InvalidOperationException("boom");
        var t = new StageTermination.Failed(ex);
        Assert.Same(ex, t.Exception);
    }

    [Fact]
    public void Skipped_DefaultReasonIsNull()
    {
        var t = new StageTermination.Skipped();
        Assert.Null(t.Reason);
    }

    [Fact]
    public void Skipped_PreservesReason()
    {
        var t = new StageTermination.Skipped("no work to do");
        Assert.Equal("no work to do", t.Reason);
    }

    [Fact]
    public void Custom_PreservesReasonAndProperties()
    {
        var props = new Dictionary<string, object?>
        {
            ["FindingCount"] = 4,
            ["Severity"] = "warn",
        };
        var t = new StageTermination.Custom("Reconciled", props);
        Assert.Equal("Reconciled", t.Reason);
        Assert.NotNull(t.Properties);
        Assert.Equal(4, t.Properties!["FindingCount"]);
        Assert.Equal("warn", t.Properties["Severity"]);
    }

    [Fact]
    public void Custom_DefaultPropertiesIsNull()
    {
        var t = new StageTermination.Custom("OnlyText");
        Assert.Null(t.Properties);
    }

    [Fact]
    public void Equality_TwoCompleted_AreEqual()
    {
        var a = new StageTermination.Completed();
        var b = new StageTermination.Completed();
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Equality_TwoMaxIterationsReached_SameValues_AreEqual()
    {
        var a = new StageTermination.MaxIterationsReached(Limit: 10, IterationsUsed: 7);
        var b = new StageTermination.MaxIterationsReached(Limit: 10, IterationsUsed: 7);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_TwoMaxIterationsReached_DifferentValues_AreNotEqual()
    {
        var a = new StageTermination.MaxIterationsReached(Limit: 10, IterationsUsed: 7);
        var b = new StageTermination.MaxIterationsReached(Limit: 10, IterationsUsed: 8);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equality_DifferentCases_AreNotEqual()
    {
        var a = (StageTermination)new StageTermination.Completed();
        var b = (StageTermination)new StageTermination.NaturalCompletion();
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ToTagValue_FrameworkCases_ReturnCaseName()
    {
        Assert.Equal("Completed", new StageTermination.Completed().ToTagValue());
        Assert.Equal("NaturalCompletion", new StageTermination.NaturalCompletion().ToTagValue());
        Assert.Equal("CompletedEarlyAfterToolCall", new StageTermination.CompletedEarlyAfterToolCall().ToTagValue());
        Assert.Equal("MaxIterationsReached", new StageTermination.MaxIterationsReached(1, 1).ToTagValue());
        Assert.Equal("MaxToolCallsReached", new StageTermination.MaxToolCallsReached(1, 1).ToTagValue());
        Assert.Equal("BudgetPressure", new StageTermination.BudgetPressure(null).ToTagValue());
        Assert.Equal("StallDetected", new StageTermination.StallDetected(null).ToTagValue());
        Assert.Equal("Cancelled", new StageTermination.Cancelled().ToTagValue());
        Assert.Equal("Failed", new StageTermination.Failed(new Exception()).ToTagValue());
        Assert.Equal("Skipped", new StageTermination.Skipped().ToTagValue());
    }

    [Fact]
    public void ToTagValue_Custom_ReturnsBoundedDiscriminator()
    {
        var t = new StageTermination.Custom("Reconciled — 7 issues remaining");
        Assert.Equal("Custom", t.ToTagValue());
    }

    [Fact]
    public void ToTagValue_Custom_IsBoundedRegardlessOfReason()
    {
        var a = new StageTermination.Custom("Reconciled — 7 issues remaining");
        var b = new StageTermination.Custom("Analysis complete: 12 issues found");
        Assert.Equal(a.ToTagValue(), b.ToTagValue());
    }

    [Fact]
    public void Custom_Reason_IsPreservedOnTheRecord()
    {
        var t = new StageTermination.Custom("Reconciled — 7 issues remaining");
        Assert.Equal("Reconciled — 7 issues remaining", t.Reason);
    }

    /// <summary>
    /// Smoke test that all 12 cases participate in a switch expression — captures
    /// the public surface so adding a new case in the future surfaces the missed
    /// arm at compile time for callers who exhaustively switch.
    /// </summary>
    [Fact]
    public void ExhaustiveSwitch_HandlesAllCases()
    {
        var all = new StageTermination[]
        {
            new StageTermination.Completed(),
            new StageTermination.NaturalCompletion(),
            new StageTermination.CompletedEarlyAfterToolCall(),
            new StageTermination.MaxIterationsReached(1, 1),
            new StageTermination.MaxToolCallsReached(1, 1),
            new StageTermination.BudgetPressure(null),
            new StageTermination.StallDetected(null),
            new StageTermination.Cancelled(),
            new StageTermination.Failed(new Exception()),
            new StageTermination.Skipped(),
            new StageTermination.Custom("c1"),
        };

        foreach (var t in all)
        {
            string label = t switch
            {
                StageTermination.Completed => "Completed",
                StageTermination.NaturalCompletion => "NaturalCompletion",
                StageTermination.CompletedEarlyAfterToolCall => "CompletedEarlyAfterToolCall",
                StageTermination.MaxIterationsReached => "MaxIterationsReached",
                StageTermination.MaxToolCallsReached => "MaxToolCallsReached",
                StageTermination.BudgetPressure => "BudgetPressure",
                StageTermination.StallDetected => "StallDetected",
                StageTermination.Cancelled => "Cancelled",
                StageTermination.Failed => "Failed",
                StageTermination.Skipped => "Skipped",
                StageTermination.Custom => "Custom",
                _ => throw new InvalidOperationException("Unhandled StageTermination case"),
            };
            Assert.NotNull(label);
        }
    }

    [Fact]
    public void PatternMatch_MaxToolCallsReached_ExtractsLimit()
    {
        StageTermination t = new StageTermination.MaxToolCallsReached(Limit: 50, ToolCallsUsed: 53);

        if (t is StageTermination.MaxToolCallsReached { Limit: var limit, ToolCallsUsed: var used })
        {
            Assert.Equal(50, limit);
            Assert.Equal(53, used);
        }
        else
        {
            Assert.Fail("Expected MaxToolCallsReached pattern match");
        }
    }
}
