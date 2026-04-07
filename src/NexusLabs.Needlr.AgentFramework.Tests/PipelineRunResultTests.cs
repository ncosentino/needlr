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
            new AgentStageResult("Writer", "wrote something", null),
            new AgentStageResult("Editor", "edited it", null),
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
            new AgentStageResult("Writer", "text-1", null),
            new AgentStageResult("Editor", "text-2", null),
        };

        var result = CreateResult(stages);

        Assert.Equal(2, result.Responses.Count);
        Assert.Equal("text-1", result.Responses["Writer"]);
        Assert.Equal("text-2", result.Responses["Editor"]);
    }

    // -------------------------------------------------------------------------
    // AggregateTokenUsage
    // -------------------------------------------------------------------------

    [Fact]
    public void AggregateTokenUsage_SumsAcrossStages()
    {
        var stages = new IAgentStageResult[]
        {
            new AgentStageResult("A", "text", CreateDiagnostics("A", inputTokens: 10, outputTokens: 20, totalTokens: 30)),
            new AgentStageResult("B", "text", CreateDiagnostics("B", inputTokens: 5, outputTokens: 15, totalTokens: 20)),
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
            new AgentStageResult("A", "text", null),
        };

        var result = CreateResult(stages);

        Assert.Null(result.AggregateTokenUsage);
    }

    [Fact]
    public void AggregateTokenUsage_MixedDiagnostics_OnlyCountsNonNull()
    {
        var stages = new IAgentStageResult[]
        {
            new AgentStageResult("A", "text", CreateDiagnostics("A", inputTokens: 10, outputTokens: 20, totalTokens: 30)),
            new AgentStageResult("B", "text", null), // no diagnostics
        };

        var result = CreateResult(stages);

        Assert.NotNull(result.AggregateTokenUsage);
        Assert.Equal(10, result.AggregateTokenUsage!.InputTokens);
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
    // Empty pipeline
    // -------------------------------------------------------------------------

    [Fact]
    public void EmptyPipeline_HasEmptyStagesAndResponses()
    {
        var result = CreateResult([]);

        Assert.Empty(result.Stages);
        Assert.Empty(result.Responses);
        Assert.Null(result.AggregateTokenUsage);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static IPipelineRunResult CreateResult(
        IAgentStageResult[] stages,
        TimeSpan? totalDuration = null,
        bool succeeded = true,
        string? errorMessage = null) =>
        new PipelineRunResult(
            stages: stages,
            totalDuration: totalDuration ?? TimeSpan.FromSeconds(1),
            succeeded: succeeded,
            errorMessage: errorMessage);

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
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow);
}
