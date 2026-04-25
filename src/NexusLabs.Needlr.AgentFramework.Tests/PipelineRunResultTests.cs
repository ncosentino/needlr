using Microsoft.Extensions.AI;
using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class PipelineRunResultTests
{
    // -------------------------------------------------------------------------
    // Stages and Responses
    // -------------------------------------------------------------------------

    [Fact]
    public void Stages_ArePreserved()
    {
        var stages = new IAgentStageResult[]
        {
            new AgentStageResult("Writer", Resp("wrote something"), null),
            new AgentStageResult("Editor", Resp("edited it"), null),
        };

        var result = CreateResult(stages);

        Assert.Equal(2, result.Stages.Count);
        Assert.Equal("Writer", result.Stages[0].AgentName);
        Assert.Equal("Editor", result.Stages[1].AgentName);
    }

    [Fact]
    public void Responses_BuildsFromStages()
    {
        var stages = new IAgentStageResult[]
        {
            new AgentStageResult("Writer", Resp("text-1"), null),
            new AgentStageResult("Editor", Resp("text-2"), null),
        };

        var result = CreateResult(stages);

        Assert.Equal(2, result.FinalResponses.Count);
        Assert.Equal("text-1", result.FinalResponses["Writer"]?.Text);
        Assert.Equal("text-2", result.FinalResponses["Editor"]?.Text);
    }

    // -------------------------------------------------------------------------
    // AggregateTokenUsage
    // -------------------------------------------------------------------------

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
    public void AggregateTokenUsage_MixedDiagnostics_OnlyCountsNonNull()
    {
        var stages = new IAgentStageResult[]
        {
            new AgentStageResult("A", Resp("text"), CreateDiagnostics("A", inputTokens: 10, outputTokens: 20, totalTokens: 30)),
            new AgentStageResult("B", Resp("text"), null), // no diagnostics
        };

        var result = CreateResult(stages);

        Assert.NotNull(result.AggregateTokenUsage);
        Assert.Equal(10, result.AggregateTokenUsage!.InputTokens);
    }

    // -------------------------------------------------------------------------
    // Duplicate agent names
    // -------------------------------------------------------------------------

    [Fact]
    public void Responses_DuplicateAgentNames_LastStageWins()
    {
        var stages = new IAgentStageResult[]
        {
            new AgentStageResult("Writer", Resp("draft-v1"), null),
            new AgentStageResult("Writer", Resp("draft-v2"), null),
        };

        var result = CreateResult(stages);

        // Stages preserves all entries
        Assert.Equal(2, result.Stages.Count);
        // Responses deduplicates — last wins
        Assert.Single(result.FinalResponses);
        Assert.Equal("draft-v2", result.FinalResponses["Writer"]?.Text);
    }

    // -------------------------------------------------------------------------
    // Success / failure
    // -------------------------------------------------------------------------

    [Fact]
    public void Succeeded_True_WhenNoErrors()
    {
        var result = CreateResult([], succeeded: true);

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Succeeded_False_WithErrorMessage()
    {
        var result = CreateResult([], succeeded: false, errorMessage: "boom");

        Assert.False(result.Succeeded);
        Assert.Equal("boom", result.ErrorMessage);
    }

    // -------------------------------------------------------------------------
    // Duration
    // -------------------------------------------------------------------------

    [Fact]
    public void TotalDuration_IsPreserved()
    {
        var result = CreateResult([], totalDuration: TimeSpan.FromSeconds(5));

        Assert.Equal(TimeSpan.FromSeconds(5), result.TotalDuration);
    }

    // -------------------------------------------------------------------------
    // PlannedStageCount
    // -------------------------------------------------------------------------

    [Fact]
    public void PlannedStageCount_DefaultsToStagesCount()
    {
        var stages = new IAgentStageResult[]
        {
            new AgentStageResult("A", Resp("text"), null),
            new AgentStageResult("B", Resp("text"), null),
        };

        var result = CreateResult(stages);

        Assert.Equal(2, result.PlannedStageCount);
    }

    [Fact]
    public void PlannedStageCount_ExplicitValue_IsPreserved()
    {
        var stages = new IAgentStageResult[]
        {
            new AgentStageResult("A", Resp("text"), null),
        };

        var result = CreateResult(stages, plannedStageCount: 5);

        Assert.Equal(5, result.PlannedStageCount);
        Assert.Single(result.Stages);
    }

    // -------------------------------------------------------------------------
    // Empty pipeline
    // -------------------------------------------------------------------------

    [Fact]
    public void EmptyPipeline_HasEmptyStagesAndResponses()
    {
        var result = CreateResult([]);

        Assert.Empty(result.Stages);
        Assert.Empty(result.FinalResponses);
        Assert.Null(result.AggregateTokenUsage);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ChatResponse Resp(string text) =>
        new(new ChatMessage(ChatRole.Assistant, text));

    private static IPipelineRunResult CreateResult(
        IAgentStageResult[] stages,
        TimeSpan? totalDuration = null,
        bool succeeded = true,
        string? errorMessage = null,
        int? plannedStageCount = null) =>
        new PipelineRunResult(
            stages: stages,
            totalDuration: totalDuration ?? TimeSpan.FromSeconds(1),
            succeeded: succeeded,
            errorMessage: errorMessage,
            plannedStageCount: plannedStageCount);

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
