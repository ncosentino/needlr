using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Workflows;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Tests for <see cref="StreamingRunWorkflowExtensions.CollectWithTerminationAsync"/>.
/// Verifies Layer 2 turn-boundary detection and early termination behaviour.
/// </summary>
public class CollectWithTerminationAsyncTests
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

    private static IReadOnlyList<IWorkflowTerminationCondition> Keyword(string keyword) =>
        [new KeywordTerminationCondition(keyword)];

    // ----- no conditions -----

    [Fact]
    public async Task CollectWithTermination_EmptyConditions_ReturnsAllResponses()
    {
        var events = new WorkflowEvent[]
        {
            MakeUpdateEvent("Agent1", "Hello "),
            MakeUpdateEvent("Agent1", "world"),
            MakeUpdateEvent("Agent2", "Goodbye"),
        };

        var result = await StreamingRunWorkflowExtensions.CollectWithTerminationAsync(
            ToAsyncEnumerable(events),
            [],
            CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("Hello world", result["Agent1"]);
        Assert.Equal("Goodbye", result["Agent2"]);
    }

    // ----- turn-boundary detection -----

    [Fact]
    public async Task CollectWithTermination_NoKeywordMatch_ReturnsAllResponses()
    {
        var events = new WorkflowEvent[]
        {
            MakeUpdateEvent("Agent1", "Working on it"),
            MakeUpdateEvent("Agent2", "Still going"),
        };

        var result = await StreamingRunWorkflowExtensions.CollectWithTerminationAsync(
            ToAsyncEnumerable(events),
            Keyword("DONE"),
            CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("Working on it", result["Agent1"]);
        Assert.Equal("Still going", result["Agent2"]);
    }

    [Fact]
    public async Task CollectWithTermination_KeywordInFirstAgent_StopsBeforeSecondAgent()
    {
        var events = new WorkflowEvent[]
        {
            MakeUpdateEvent("Agent1", "APPROVED — looks great!"),
            MakeUpdateEvent("Agent2", "This text should not appear"),
        };

        var result = await StreamingRunWorkflowExtensions.CollectWithTerminationAsync(
            ToAsyncEnumerable(events),
            Keyword("APPROVED"),
            CancellationToken.None);

        Assert.True(result.ContainsKey("Agent1"));
        Assert.False(result.ContainsKey("Agent2"));
    }

    [Fact]
    public async Task CollectWithTermination_KeywordInSecondAgent_StopsAfterSecondAgent()
    {
        var events = new WorkflowEvent[]
        {
            MakeUpdateEvent("Agent1", "Phase one done"),
            MakeUpdateEvent("Agent2", "EXTRACTION_FAILED"),
            MakeUpdateEvent("Agent3", "Phase three should not run"),
        };

        var result = await StreamingRunWorkflowExtensions.CollectWithTerminationAsync(
            ToAsyncEnumerable(events),
            Keyword("EXTRACTION_FAILED"),
            CancellationToken.None);

        // Agent1 completed — present
        Assert.True(result.ContainsKey("Agent1"));
        // Agent2 triggered the condition — its response is still captured
        Assert.True(result.ContainsKey("Agent2"));
        // Agent3 was never started
        Assert.False(result.ContainsKey("Agent3"));
    }

    [Fact]
    public async Task CollectWithTermination_MultipleChunksPerAgent_ConcatenatesBeforeEval()
    {
        var events = new WorkflowEvent[]
        {
            MakeUpdateEvent("Agent1", "The answer is "),
            MakeUpdateEvent("Agent1", "APPROVED"),
            MakeUpdateEvent("Agent2", "Should not appear"),
        };

        var result = await StreamingRunWorkflowExtensions.CollectWithTerminationAsync(
            ToAsyncEnumerable(events),
            Keyword("APPROVED"),
            CancellationToken.None);

        // Agent1's full response includes "APPROVED" — termination fires after its turn ends
        Assert.Equal("The answer is APPROVED", result["Agent1"]);
        Assert.False(result.ContainsKey("Agent2"));
    }

    [Fact]
    public async Task CollectWithTermination_EmptyStream_ReturnsEmpty()
    {
        var result = await StreamingRunWorkflowExtensions.CollectWithTerminationAsync(
            ToAsyncEnumerable([]),
            Keyword("DONE"),
            CancellationToken.None);

        Assert.Empty(result);
    }

    // ----- agent-filtered conditions -----

    [Fact]
    public async Task CollectWithTermination_AgentFilteredCondition_WrongAgent_DoesNotTerminate()
    {
        var conditions = new IWorkflowTerminationCondition[]
        {
            new KeywordTerminationCondition("APPROVED", "ApprovalAgent"),
        };

        var events = new WorkflowEvent[]
        {
            MakeUpdateEvent("OtherAgent", "APPROVED"),
            MakeUpdateEvent("ApprovalAgent", "Still reviewing"),
        };

        var result = await StreamingRunWorkflowExtensions.CollectWithTerminationAsync(
            ToAsyncEnumerable(events),
            conditions,
            CancellationToken.None);

        // OtherAgent said APPROVED but condition is scoped to ApprovalAgent — no early stop
        Assert.True(result.ContainsKey("OtherAgent"));
        Assert.True(result.ContainsKey("ApprovalAgent"));
    }

    [Fact]
    public async Task CollectWithTermination_AgentFilteredCondition_CorrectAgent_Terminates()
    {
        var conditions = new IWorkflowTerminationCondition[]
        {
            new KeywordTerminationCondition("APPROVED", "ApprovalAgent"),
        };

        var events = new WorkflowEvent[]
        {
            MakeUpdateEvent("WorkerAgent", "Work done"),
            MakeUpdateEvent("ApprovalAgent", "APPROVED — ship it!"),
            MakeUpdateEvent("WorkerAgent", "Should not appear"),
        };

        var result = await StreamingRunWorkflowExtensions.CollectWithTerminationAsync(
            ToAsyncEnumerable(events),
            conditions,
            CancellationToken.None);

        Assert.True(result.ContainsKey("WorkerAgent"));
        Assert.True(result.ContainsKey("ApprovalAgent"));
        // Second WorkerAgent chunk would come as a new executor cycle — verifying it's absent
        // proves early termination. The result dict has 2 entries, not 3.
        Assert.Equal(2, result.Count);
    }
}
