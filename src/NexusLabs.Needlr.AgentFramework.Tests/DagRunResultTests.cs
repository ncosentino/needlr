using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class DagRunResultTests
{
    [Fact]
    public void Constructor_PreservesStagesAndNodeResults()
    {
        var stages = new IAgentStageResult[]
        {
            new AgentStageResult("Agent1", Resp("output-1"), null),
            new AgentStageResult("Agent2", Resp("output-2"), null),
        };

        var nodeResults = new Dictionary<string, IDagNodeResult>
        {
            ["node-1"] = CreateNodeResult("node-1", "Agent1", NodeKind.Agent),
            ["node-2"] = CreateNodeResult("node-2", "Agent2", NodeKind.Agent),
        };

        var branchResults = new Dictionary<string, IReadOnlyList<IAgentStageResult>>
        {
            ["branch-a"] = [stages[0]],
            ["branch-b"] = [stages[1]],
        };

        var result = new DagRunResult(
            stages: stages,
            nodeResults: nodeResults,
            branchResults: branchResults,
            totalDuration: TimeSpan.FromSeconds(3),
            succeeded: true,
            errorMessage: null);

        Assert.Equal(2, result.Stages.Count);
        Assert.Equal(2, result.NodeResults.Count);
        Assert.Equal(2, result.BranchResults.Count);
        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(TimeSpan.FromSeconds(3), result.TotalDuration);
    }

    [Fact]
    public void FinalResponses_BuiltFromStages()
    {
        var stages = new IAgentStageResult[]
        {
            new AgentStageResult("Agent1", Resp("text-1"), null),
            new AgentStageResult("Agent2", Resp("text-2"), null),
        };

        var result = CreateResult(stages);

        Assert.Equal(2, result.FinalResponses.Count);
        Assert.Equal("text-1", result.FinalResponses["Agent1"]?.Text);
        Assert.Equal("text-2", result.FinalResponses["Agent2"]?.Text);
    }

    [Fact]
    public void AggregateTokenUsage_SumsAcrossStages()
    {
        var stages = new IAgentStageResult[]
        {
            new AgentStageResult("A", Resp("text"), CreateDiagnostics("A", inputTokens: 10, outputTokens: 20, totalTokens: 30)),
            new AgentStageResult("B", Resp("text"), CreateDiagnostics("B", inputTokens: 5, outputTokens: 15, totalTokens: 20)),
        };

        var result = CreateResult(stages);

        Assert.NotNull(result.AggregateTokenUsage);
        Assert.Equal(15, result.AggregateTokenUsage!.InputTokens);
        Assert.Equal(35, result.AggregateTokenUsage.OutputTokens);
        Assert.Equal(50, result.AggregateTokenUsage.TotalTokens);
    }

    [Fact]
    public void AggregateTokenUsage_NoDiagnostics_ReturnsNull()
    {
        var stages = new IAgentStageResult[]
        {
            new AgentStageResult("A", Resp("text"), null),
        };

        var result = CreateResult(stages);

        Assert.Null(result.AggregateTokenUsage);
    }

    [Fact]
    public void NodeResults_ContainsReducerNodes()
    {
        var stages = Array.Empty<IAgentStageResult>();
        var nodeResults = new Dictionary<string, IDagNodeResult>
        {
            ["reducer-1"] = CreateNodeResult("reducer-1", "Merge", NodeKind.Reducer),
        };

        var result = new DagRunResult(
            stages: stages,
            nodeResults: nodeResults,
            branchResults: new Dictionary<string, IReadOnlyList<IAgentStageResult>>(),
            totalDuration: TimeSpan.FromSeconds(1),
            succeeded: true,
            errorMessage: null);

        Assert.Single(result.NodeResults);
        Assert.Equal(NodeKind.Reducer, result.NodeResults["reducer-1"].Kind);
    }

    [Fact]
    public void Failed_PreservesErrorAndException()
    {
        var ex = new InvalidOperationException("graph cycle detected");
        var result = new DagRunResult(
            stages: [],
            nodeResults: new Dictionary<string, IDagNodeResult>(),
            branchResults: new Dictionary<string, IReadOnlyList<IAgentStageResult>>(),
            totalDuration: TimeSpan.FromMilliseconds(50),
            succeeded: false,
            errorMessage: "graph cycle detected",
            exception: ex);

        Assert.False(result.Succeeded);
        Assert.Equal("graph cycle detected", result.ErrorMessage);
        Assert.Same(ex, result.Exception);
    }

    [Fact]
    public void EmptyDag_HasEmptyCollections()
    {
        var result = new DagRunResult(
            stages: [],
            nodeResults: new Dictionary<string, IDagNodeResult>(),
            branchResults: new Dictionary<string, IReadOnlyList<IAgentStageResult>>(),
            totalDuration: TimeSpan.Zero,
            succeeded: true,
            errorMessage: null);

        Assert.Empty(result.Stages);
        Assert.Empty(result.NodeResults);
        Assert.Empty(result.BranchResults);
        Assert.Empty(result.FinalResponses);
        Assert.Null(result.AggregateTokenUsage);
    }

    [Fact]
    public void Implements_IDagRunResult()
    {
        var result = new DagRunResult(
            stages: [],
            nodeResults: new Dictionary<string, IDagNodeResult>(),
            branchResults: new Dictionary<string, IReadOnlyList<IAgentStageResult>>(),
            totalDuration: TimeSpan.Zero,
            succeeded: true,
            errorMessage: null);

        Assert.IsAssignableFrom<IDagRunResult>(result);
        Assert.IsAssignableFrom<IPipelineRunResult>(result);
    }

    private static ChatResponse Resp(string text) =>
        new(new ChatMessage(ChatRole.Assistant, text));

    private static IDagRunResult CreateResult(
        IAgentStageResult[] stages,
        TimeSpan? totalDuration = null,
        bool succeeded = true,
        string? errorMessage = null) =>
        new DagRunResult(
            stages: stages,
            nodeResults: new Dictionary<string, IDagNodeResult>(),
            branchResults: new Dictionary<string, IReadOnlyList<IAgentStageResult>>(),
            totalDuration: totalDuration ?? TimeSpan.FromSeconds(1),
            succeeded: succeeded,
            errorMessage: errorMessage);

    private static IDagNodeResult CreateNodeResult(
        string nodeId,
        string agentName,
        NodeKind kind) =>
        new DagNodeResult(
            nodeId: nodeId,
            agentName: agentName,
            kind: kind,
            diagnostics: null,
            finalResponse: null,
            inboundEdges: [],
            outboundEdges: [],
            startOffset: TimeSpan.Zero,
            duration: TimeSpan.FromMilliseconds(100));

    private static IAgentRunDiagnostics CreateDiagnostics(
        string agentName,
        long inputTokens = 0,
        long outputTokens = 0,
        long totalTokens = 0) =>
        new AgentRunDiagnostics(
            AgentName: agentName,
            TotalDuration: TimeSpan.FromMilliseconds(100),
            AggregateTokenUsage: new TokenUsage(inputTokens, outputTokens, totalTokens, 0, 0),
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
