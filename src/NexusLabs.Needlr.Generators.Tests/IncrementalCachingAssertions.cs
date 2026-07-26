using System.Linq;

using Microsoft.CodeAnalysis;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Shared assertions for Roslyn tracked incremental generator steps.
/// </summary>
internal static class IncrementalCachingAssertions
{
    public static void AssertAllOutputsCachedOrUnchanged(
        GeneratorRunResult result,
        string trackingName)
    {
        var outputs = GetTrackedOutputs(result, trackingName);

        Assert.NotEmpty(outputs);
        Assert.All(
            outputs,
            output => Assert.True(
                output.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
                $"Expected '{trackingName}' output to be cached or unchanged, but found '{output.Reason}'."));
    }

    public static void AssertExactlyOneChangedAndOneCached(
        GeneratorRunResult result,
        string trackingName)
    {
        var outputs = GetTrackedOutputs(result, trackingName);

        Assert.Equal(2, outputs.Length);
        Assert.Equal(
            1,
            outputs.Count(output =>
                output.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged));
        Assert.Equal(
            1,
            outputs.Count(output =>
                output.Reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New));
    }

    public static (object Value, IncrementalStepRunReason Reason)[] GetTrackedOutputs(
        GeneratorRunResult result,
        string trackingName)
    {
        Assert.True(
            result.TrackedSteps.TryGetValue(trackingName, out var steps),
            $"Expected tracked step '{trackingName}'.");

        return steps
            .SelectMany(step => step.Outputs)
            .ToArray();
    }
}
