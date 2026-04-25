using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests;

public sealed class PipelineStageEvaluatorTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task EvaluateAsync_SucceededPipeline_EmitsSucceeded()
    {
        var pipeline = BuildPipeline(
            succeeded: true,
            durationMs: 1000,
            stages: MakeCompletedStage("agent-a"));

        var result = await RunAsync(pipeline);

        AssertBoolean(result, PipelineStageEvaluator.SucceededMetricName, true);
    }

    [Fact]
    public async Task EvaluateAsync_FailedPipeline_EmitsNotSucceeded()
    {
        var pipeline = BuildPipeline(
            succeeded: false,
            durationMs: 500,
            errorMessage: "Stage 'agent-b' threw an exception.",
            stages: MakeCompletedStage("agent-a"));

        var result = await RunAsync(pipeline);

        AssertBoolean(result, PipelineStageEvaluator.SucceededMetricName, false);
        AssertString(result, PipelineStageEvaluator.ErrorMessageMetricName, "Stage 'agent-b' threw an exception.");
    }

    [Fact]
    public async Task EvaluateAsync_CountsCompletedAndSkippedStages()
    {
        var pipeline = BuildPipeline(
            succeeded: true,
            durationMs: 1000,
            errorMessage: null,
            MakeCompletedStage("agent-a"),
            MakeSkippedStage("agent-b"),
            MakeCompletedStage("agent-c"),
            MakeSkippedStage("agent-d"));

        var result = await RunAsync(pipeline);

        AssertNumeric(result, PipelineStageEvaluator.TotalStagesMetricName, 4);
        AssertNumeric(result, PipelineStageEvaluator.CompletedStagesMetricName, 2);
        AssertNumeric(result, PipelineStageEvaluator.SkippedStagesMetricName, 2);
    }

    [Fact]
    public async Task EvaluateAsync_EmitsDuration()
    {
        var pipeline = BuildPipeline(
            succeeded: true,
            durationMs: 1234,
            stages: MakeCompletedStage("agent-a"));

        var result = await RunAsync(pipeline);

        AssertNumeric(result, PipelineStageEvaluator.TotalDurationMsMetricName, 1234);
    }

    [Fact]
    public async Task EvaluateAsync_NoPipelineContext_ReturnsEmptyResult()
    {
        var evaluator = new PipelineStageEvaluator();

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
        var evaluator = new PipelineStageEvaluator();
        return await evaluator.EvaluateAsync(
            messages: Array.Empty<ChatMessage>(),
            modelResponse: new ChatResponse(),
            chatConfiguration: null,
            additionalContext: [new PipelineEvaluationContext(pipeline)],
            cancellationToken: _ct);
    }

    private static FakeAgentStageResult MakeCompletedStage(string name)
    {
        return new FakeAgentStageResult
        {
            AgentName = name,
            FinalResponse = null,
            Diagnostics = FakeAgentRunDiagnostics.Create(agentName: name),
        };
    }

    private static FakeAgentStageResult MakeSkippedStage(string name)
    {
        return new FakeAgentStageResult
        {
            AgentName = name,
            FinalResponse = null,
            Diagnostics = null,
        };
    }

    private static FakePipelineRunResult BuildPipeline(
        bool succeeded,
        double durationMs,
        string? errorMessage = null,
        params FakeAgentStageResult[] stages)
    {
        return new FakePipelineRunResult
        {
            Stages = stages,
            FinalResponses = new Dictionary<string, ChatResponse?>(),
            TotalDuration = TimeSpan.FromMilliseconds(durationMs),
            AggregateTokenUsage = null,
            Succeeded = succeeded,
            ErrorMessage = errorMessage,
        };
    }

    private static void AssertNumeric(EvaluationResult result, string name, double expected)
    {
        var metric = Assert.IsType<NumericMetric>(result.Metrics[name]);
        Assert.Equal(expected, metric.Value);
    }

    private static void AssertBoolean(EvaluationResult result, string name, bool expected)
    {
        var metric = Assert.IsType<BooleanMetric>(result.Metrics[name]);
        Assert.Equal(expected, metric.Value);
    }

    private static void AssertString(EvaluationResult result, string name, string expected)
    {
        var metric = Assert.IsType<StringMetric>(result.Metrics[name]);
        Assert.Equal(expected, metric.Value);
    }
}
