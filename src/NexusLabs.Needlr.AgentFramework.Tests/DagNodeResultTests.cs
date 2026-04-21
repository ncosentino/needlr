using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class DagNodeResultTests
{
    [Fact]
    public void Constructor_PreservesAllProperties()
    {
        var diag = CreateDiagnostics("Agent1");
        var response = Resp("some output");

        var result = new DagNodeResult(
            nodeId: "node-1",
            agentName: "Agent1",
            kind: NodeKind.Agent,
            diagnostics: diag,
            finalResponse: response,
            inboundEdges: ["entry"],
            outboundEdges: ["node-2", "node-3"],
            startOffset: TimeSpan.FromMilliseconds(100),
            duration: TimeSpan.FromMilliseconds(500));

        Assert.Equal("node-1", result.NodeId);
        Assert.Equal("Agent1", result.AgentName);
        Assert.Equal(NodeKind.Agent, result.Kind);
        Assert.Same(diag, result.Diagnostics);
        Assert.Same(response, result.FinalResponse);
        Assert.Equal(["entry"], result.InboundEdges);
        Assert.Equal(["node-2", "node-3"], result.OutboundEdges);
        Assert.Equal(TimeSpan.FromMilliseconds(100), result.StartOffset);
        Assert.Equal(TimeSpan.FromMilliseconds(500), result.Duration);
    }

    [Fact]
    public void ReducerNode_HasNullDiagnosticsAndResponse()
    {
        var result = new DagNodeResult(
            nodeId: "reducer-1",
            agentName: "MergeResults",
            kind: NodeKind.Reducer,
            diagnostics: null,
            finalResponse: null,
            inboundEdges: ["branch-a", "branch-b"],
            outboundEdges: [],
            startOffset: TimeSpan.FromSeconds(2),
            duration: TimeSpan.FromMilliseconds(5));

        Assert.Equal(NodeKind.Reducer, result.Kind);
        Assert.Null(result.Diagnostics);
        Assert.Null(result.FinalResponse);
        Assert.Equal(2, result.InboundEdges.Count);
        Assert.Empty(result.OutboundEdges);
    }

    [Fact]
    public void EmptyEdgeLists_ArePreserved()
    {
        var result = new DagNodeResult(
            nodeId: "isolated",
            agentName: "Solo",
            kind: NodeKind.Agent,
            diagnostics: null,
            finalResponse: null,
            inboundEdges: [],
            outboundEdges: [],
            startOffset: TimeSpan.Zero,
            duration: TimeSpan.FromMilliseconds(10));

        Assert.Empty(result.InboundEdges);
        Assert.Empty(result.OutboundEdges);
    }

    private static ChatResponse Resp(string text) =>
        new(new ChatMessage(ChatRole.Assistant, text));

    private static IAgentRunDiagnostics CreateDiagnostics(string agentName) =>
        new AgentRunDiagnostics(
            AgentName: agentName,
            TotalDuration: TimeSpan.FromMilliseconds(100),
            AggregateTokenUsage: new TokenUsage(10, 20, 30, 0, 0),
            ChatCompletions: [],
            ToolCalls: [],
            TotalInputMessages: 1,
            TotalOutputMessages: 1,
            InputMessages: [],
            OutputResponse: null,
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow);
}
