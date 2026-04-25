using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests;

public sealed class PipelineEvaluationContextTests
{
    [Fact]
    public void Constructor_SetsContextName()
    {
        var result = CreatePipelineResult();

        var context = new PipelineEvaluationContext(result);

        Assert.Equal(PipelineEvaluationContext.ContextName, context.Name);
    }

    [Fact]
    public void Constructor_SetsPipelineResult()
    {
        var result = CreatePipelineResult();

        var context = new PipelineEvaluationContext(result);

        Assert.Same(result, context.PipelineResult);
    }

    [Fact]
    public void Constructor_BuildsContentsSummary()
    {
        var result = CreatePipelineResult(
            succeeded: true,
            stageCount: 2,
            durationMs: 1234,
            totalTokens: 500);

        var context = new PipelineEvaluationContext(result);

        Assert.Single(context.Contents);
        var text = Assert.IsType<TextContent>(context.Contents[0]);
        Assert.Contains("Succeeded=True", text.Text);
        Assert.Contains("Stages=2", text.Text);
        Assert.Contains("Duration=1234ms", text.Text);
        Assert.Contains("TotalTokens=500", text.Text);
    }

    [Fact]
    public void Constructor_NullResult_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PipelineEvaluationContext(null!));
    }

    [Fact]
    public void ForStage_WithDiagnostics_ReturnsContext()
    {
        var diagnostics = FakeAgentRunDiagnostics.Create(agentName: "stage-agent");
        var stage = new FakeAgentStageResult
        {
            AgentName = "stage-agent",
            FinalResponse = null,
            Diagnostics = diagnostics,
        };

        var context = PipelineEvaluationContext.ForStage(stage);

        Assert.NotNull(context);
        Assert.Same(diagnostics, context.Diagnostics);
    }

    [Fact]
    public void ForStage_NullDiagnostics_ReturnsNull()
    {
        var stage = new FakeAgentStageResult
        {
            AgentName = "stage-agent",
            FinalResponse = null,
            Diagnostics = null,
        };

        var context = PipelineEvaluationContext.ForStage(stage);

        Assert.Null(context);
    }

    [Fact]
    public void ForPipeline_ReturnsPipelineContext()
    {
        var result = CreatePipelineResult();

        var context = PipelineEvaluationContext.ForPipeline(result);

        Assert.NotNull(context);
        Assert.Same(result, context.PipelineResult);
        Assert.Equal(PipelineEvaluationContext.ContextName, context.Name);
    }

    private static FakePipelineRunResult CreatePipelineResult(
        bool succeeded = true,
        int stageCount = 1,
        double durationMs = 100,
        long totalTokens = 42)
    {
        var stages = new List<IAgentStageResult>();
        for (var i = 0; i < stageCount; i++)
        {
            stages.Add(new FakeAgentStageResult
            {
                AgentName = $"agent-{i}",
                FinalResponse = null,
                Diagnostics = FakeAgentRunDiagnostics.Create(agentName: $"agent-{i}"),
            });
        }

        return new FakePipelineRunResult
        {
            Stages = stages,
            PlannedStageCount = stages.Count,
            FinalResponses = new Dictionary<string, ChatResponse?>(),
            TotalDuration = TimeSpan.FromMilliseconds(durationMs),
            AggregateTokenUsage = new TokenUsage(0, 0, totalTokens, 0, 0),
            Succeeded = succeeded,
            ErrorMessage = null,
        };
    }
}
