using System.Reflection;

using NexusLabs.Needlr.AgentFramework.Progress;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Regression-lock tests that enumerate every concrete <see cref="IProgressEvent"/>
/// type so adding a new event forces callers/sinks to acknowledge it.
/// </summary>
public class ProgressEventCoverageTests
{
    [Fact]
    public void EnumerateAll_ConcreteProgressEvents_IsNonEmpty()
    {
        var concreteEvents = GetConcreteProgressEventTypes();
        Assert.NotEmpty(concreteEvents);
    }

    [Fact]
    public void EveryConcreteProgressEvent_HasBaseContextProperties()
    {
        // Every progress event must carry correlation context — WorkflowId, AgentId,
        // ParentAgentId, Depth, SequenceNumber, Timestamp — because sinks rely on
        // these for ordering and tracing. A new event missing any of these would
        // break that contract.
        var required = new[] { "Timestamp", "WorkflowId", "AgentId", "ParentAgentId", "Depth", "SequenceNumber" };

        foreach (var type in GetConcreteProgressEventTypes())
        {
            foreach (var propertyName in required)
            {
                Assert.True(
                    type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance) is not null,
                    $"{type.Name} is missing required base property '{propertyName}'.");
            }
        }
    }

    /// <summary>
    /// Known event types that must each be handled somewhere in production code
    /// (workflow runner, middleware, or a default sink). If a new event is added
    /// to <see cref="IProgressEvent"/>, this catalogue must be updated as a
    /// deliberate acknowledgement.
    /// </summary>
    [Fact]
    public void KnownConcreteEvents_MatchTheRegisteredCatalogue()
    {
        var expected = new HashSet<string>
        {
            nameof(WorkflowStartedEvent),
            nameof(WorkflowCompletedEvent),
            nameof(AgentInvokedEvent),
            nameof(AgentCompletedEvent),
            nameof(AgentFailedEvent),
            nameof(AgentHandoffEvent),
            nameof(LlmCallStartedEvent),
            nameof(LlmCallCompletedEvent),
            nameof(LlmCallFailedEvent),
            nameof(ToolCallStartedEvent),
            nameof(ToolCallCompletedEvent),
            nameof(ToolCallFailedEvent),
            nameof(BudgetUpdatedEvent),
            nameof(BudgetExceededEvent),
            nameof(SuperStepStartedProgressEvent),
            nameof(SuperStepCompletedProgressEvent),
            nameof(ReducerNodeInvokedEvent),
            nameof(AgentResponseChunkEvent),
            nameof(PhaseStartedEvent),
            nameof(PhaseCompletedEvent),
        };

        var actual = GetConcreteProgressEventTypes().Select(t => t.Name).ToHashSet();

        var newEvents = actual.Except(expected).ToList();
        var removedEvents = expected.Except(actual).ToList();

        Assert.True(
            newEvents.Count == 0,
            $"New IProgressEvent types detected: [{string.Join(", ", newEvents)}]. " +
            $"Update the catalogue and ensure every sink + PipelineRunExtensions handles them.");
        Assert.True(
            removedEvents.Count == 0,
            $"Expected IProgressEvent types no longer present: [{string.Join(", ", removedEvents)}]. " +
            $"Remove from catalogue or restore the type.");
    }

    private static List<Type> GetConcreteProgressEventTypes()
    {
        return typeof(IProgressEvent).Assembly
            .GetTypes()
            .Where(t => typeof(IProgressEvent).IsAssignableFrom(t))
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .OrderBy(t => t.Name)
            .ToList();
    }
}
