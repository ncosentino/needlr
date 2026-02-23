using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Workflows;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Tests for <see cref="StreamingRunWorkflowExtensions.CollectAgentResponsesAsync"/>
/// and the internal <c>CollectFromEventsAsync</c> helper.
/// </summary>
public class CollectAgentResponsesExtensionsTests
{
    private static AgentResponseUpdateEvent MakeUpdateEvent(string executorId, string text)
    {
        var update = new AgentResponseUpdate(ChatRole.Assistant, text);
        return new AgentResponseUpdateEvent(executorId, update);
    }

    private static async IAsyncEnumerable<WorkflowEvent> ToAsyncEnumerable(
        IEnumerable<WorkflowEvent> events)
    {
        foreach (var evt in events)
            yield return evt;
        await Task.CompletedTask;
    }

    [Fact]
    public async Task CollectFromEvents_TwoAgentUpdates_ReturnsGroupedByExecutorId()
    {
        var events = new WorkflowEvent[]
        {
            MakeUpdateEvent("Agent1", "Hello from one"),
            MakeUpdateEvent("Agent2", "Hello from two"),
        };

        var result = await StreamingRunWorkflowExtensions.CollectFromEventsAsync(
            ToAsyncEnumerable(events));

        Assert.Equal(2, result.Count);
        Assert.Equal("Hello from one", result["Agent1"]);
        Assert.Equal("Hello from two", result["Agent2"]);
    }

    [Fact]
    public async Task CollectFromEvents_MultipleUpdatesPerAgent_ConcatenatesText()
    {
        var events = new WorkflowEvent[]
        {
            MakeUpdateEvent("Agent1", "Hello "),
            MakeUpdateEvent("Agent1", "world"),
            MakeUpdateEvent("Agent1", "!"),
        };

        var result = await StreamingRunWorkflowExtensions.CollectFromEventsAsync(
            ToAsyncEnumerable(events));

        Assert.Single(result);
        Assert.Equal("Hello world!", result["Agent1"]);
    }

    [Fact]
    public async Task CollectFromEvents_NoResponseEvents_ReturnsEmpty()
    {
        var result = await StreamingRunWorkflowExtensions.CollectFromEventsAsync(
            ToAsyncEnumerable([]));

        Assert.Empty(result);
    }

    [Fact]
    public async Task CollectFromEvents_NullData_Skipped()
    {
        // An AgentResponseUpdateEvent where the update has no text content
        var emptyUpdate = new AgentResponseUpdate(ChatRole.Assistant, string.Empty);
        var events = new WorkflowEvent[]
        {
            new AgentResponseUpdateEvent("Agent1", emptyUpdate),
            MakeUpdateEvent("Agent2", "Valid text"),
        };

        var result = await StreamingRunWorkflowExtensions.CollectFromEventsAsync(
            ToAsyncEnumerable(events));

        // Agent1 produced empty text — no entry expected
        Assert.DoesNotContain("Agent1", result.Keys);
        Assert.Equal("Valid text", result["Agent2"]);
    }

    [Fact]
    public async Task CollectFromEvents_ParityWithManualAggregation_ProducesSameResult()
    {
        var events = new WorkflowEvent[]
        {
            MakeUpdateEvent("Triage", "Routing to specialist"),
            MakeUpdateEvent("Specialist", "Here is the answer: "),
            MakeUpdateEvent("Specialist", "42"),
        };

        // Needlr helper
        var needlrResult = await StreamingRunWorkflowExtensions.CollectFromEventsAsync(
            ToAsyncEnumerable(events));

        // Manual equivalent — the boilerplate Needlr replaces
        var manualResponses = new Dictionary<string, System.Text.StringBuilder>();
        await foreach (var evt in ToAsyncEnumerable(events))
        {
            if (evt is AgentResponseUpdateEvent update
                && update.ExecutorId is not null
                && update.Data is not null)
            {
                var text = update.Data.ToString();
                if (string.IsNullOrEmpty(text)) continue;
                if (!manualResponses.TryGetValue(update.ExecutorId, out var sb))
                    manualResponses[update.ExecutorId] = sb = new System.Text.StringBuilder();
                sb.Append(text);
            }
        }
        var manualResult = manualResponses.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());

        Assert.Equal(manualResult.Count, needlrResult.Count);
        foreach (var (key, value) in manualResult)
            Assert.Equal(value, needlrResult[key]);
    }
}
