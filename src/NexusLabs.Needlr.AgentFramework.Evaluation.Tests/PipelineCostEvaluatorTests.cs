using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests;

public sealed class PipelineCostEvaluatorTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task EvaluateAsync_WithDiagnostics_EmitsTotalTokens()
    {
        var pipeline = BuildPipeline(
            MakeStage("agent-a", new TokenUsage(100, 50, 150, 0, 0)),
            MakeStage("agent-b", new TokenUsage(200, 100, 300, 0, 0)));

        var result = await RunAsync(pipeline);

        AssertNumeric(result, PipelineCostEvaluator.TotalTokensMetricName, 450);
        AssertNumeric(result, PipelineCostEvaluator.TotalInputTokensMetricName, 300);
        AssertNumeric(result, PipelineCostEvaluator.TotalOutputTokensMetricName, 150);
    }

    [Fact]
    public async Task EvaluateAsync_NoDiagnostics_EmitsZeroTokens()
    {
        var pipeline = BuildPipeline(
            MakeStageNoDiagnostics("agent-a"),
            MakeStageNoDiagnostics("agent-b"));

        var result = await RunAsync(pipeline);

        AssertNumeric(result, PipelineCostEvaluator.TotalTokensMetricName, 0);
        AssertNumeric(result, PipelineCostEvaluator.TotalInputTokensMetricName, 0);
        AssertNumeric(result, PipelineCostEvaluator.TotalOutputTokensMetricName, 0);
        AssertNumeric(result, PipelineCostEvaluator.StagesWithDiagnosticsMetricName, 0);
    }

    [Fact]
    public async Task EvaluateAsync_MixedDiagnostics_CountsOnlyNonNull()
    {
        var pipeline = BuildPipeline(
            MakeStage("agent-a", new TokenUsage(100, 50, 150, 0, 0)),
            MakeStageNoDiagnostics("agent-b"),
            MakeStage("agent-c", new TokenUsage(200, 100, 300, 0, 0)));

        var result = await RunAsync(pipeline);

        AssertNumeric(result, PipelineCostEvaluator.TotalTokensMetricName, 450);
        AssertNumeric(result, PipelineCostEvaluator.StageCountMetricName, 3);
        AssertNumeric(result, PipelineCostEvaluator.StagesWithDiagnosticsMetricName, 2);
    }

    [Fact]
    public async Task EvaluateAsync_FindsMostExpensiveStage()
    {
        var pipeline = BuildPipeline(
            MakeStage("cheap-agent", new TokenUsage(50, 50, 100, 0, 0)),
            MakeStage("expensive-agent", new TokenUsage(400, 200, 600, 0, 0)),
            MakeStage("mid-agent", new TokenUsage(100, 100, 200, 0, 0)));

        var result = await RunAsync(pipeline);

        AssertString(result, PipelineCostEvaluator.MostExpensiveStageMetricName, "expensive-agent");

        // 600 / 900 * 100 ≈ 66.67%
        var pctMetric = Assert.IsType<NumericMetric>(
            result.Metrics[PipelineCostEvaluator.MostExpensiveStagePctMetricName]);
        Assert.True(
            Math.Abs(pctMetric.Value!.Value - 66.667) < 0.1,
            $"Expected ~66.67%, got {pctMetric.Value}");
    }

    [Fact]
    public async Task EvaluateAsync_NoPipelineContext_ReturnsEmptyResult()
    {
        var evaluator = new PipelineCostEvaluator();

        var result = await evaluator.EvaluateAsync(
            messages: Array.Empty<ChatMessage>(),
            modelResponse: new ChatResponse(),
            chatConfiguration: null,
            additionalContext: null,
            cancellationToken: _ct);

        Assert.Empty(result.Metrics);
    }

    private async Task<EvaluationResult> RunAsync(FakePipelineRunResult pipeline)
    {
        var evaluator = new PipelineCostEvaluator();
        return await evaluator.EvaluateAsync(
            messages: Array.Empty<ChatMessage>(),
            modelResponse: new ChatResponse(),
            chatConfiguration: null,
            additionalContext: [new PipelineEvaluationContext(pipeline)],
            cancellationToken: _ct);
    }

    private static FakeAgentStageResult MakeStage(string name, TokenUsage usage)
    {
        return new FakeAgentStageResult
        {
            AgentName = name,
            FinalResponse = null,
            Diagnostics = new FakeAgentRunDiagnostics
            {
                AgentName = name,
                TotalDuration = TimeSpan.FromMilliseconds(1),
                AggregateTokenUsage = usage,
                ChatCompletions = Array.Empty<ChatCompletionDiagnostics>(),
                ToolCalls = Array.Empty<ToolCallDiagnostics>(),
                TotalInputMessages = 1,
                TotalOutputMessages = 0,
                InputMessages = new List<ChatMessage> { new(ChatRole.User, "Hello.") },
                OutputResponse = null,
                Succeeded = true,
                ErrorMessage = null,
                StartedAt = DateTimeOffset.UnixEpoch,
                CompletedAt = DateTimeOffset.UnixEpoch,
                ExecutionMode = null,
            },
        };
    }

    private static FakeAgentStageResult MakeStageNoDiagnostics(string name)
    {
        return new FakeAgentStageResult
        {
            AgentName = name,
            FinalResponse = null,
            Diagnostics = null,
        };
    }

    private static FakePipelineRunResult BuildPipeline(params FakeAgentStageResult[] stages)
    {
        return new FakePipelineRunResult
        {
            Stages = stages,
            FinalResponses = new Dictionary<string, ChatResponse?>(),
            TotalDuration = TimeSpan.FromMilliseconds(500),
            AggregateTokenUsage = null,
            Succeeded = true,
            ErrorMessage = null,
        };
    }

    private static void AssertNumeric(EvaluationResult result, string name, double expected)
    {
        var metric = Assert.IsType<NumericMetric>(result.Metrics[name]);
        Assert.Equal(expected, metric.Value);
    }

    private static void AssertString(EvaluationResult result, string name, string expected)
    {
        var metric = Assert.IsType<StringMetric>(result.Metrics[name]);
        Assert.Equal(expected, metric.Value);
    }
}
