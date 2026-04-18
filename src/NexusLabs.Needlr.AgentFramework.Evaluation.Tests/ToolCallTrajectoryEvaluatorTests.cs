using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests;

public sealed class ToolCallTrajectoryEvaluatorTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task EvaluateAsync_MissingContext_ReturnsEmptyResult()
    {
        var evaluator = new ToolCallTrajectoryEvaluator();

        var result = await evaluator.EvaluateAsync(
            messages: Array.Empty<ChatMessage>(),
            modelResponse: new ChatResponse(),
            chatConfiguration: null,
            additionalContext: null,
            cancellationToken: _ct);

        Assert.Empty(result.Metrics);
    }

    [Fact]
    public async Task EvaluateAsync_EmptyToolCalls_ReturnsZerosAndAllSucceededTrue()
    {
        var diagnostics = FakeAgentRunDiagnostics.Create();

        var result = await RunAsync(diagnostics);

        AssertNumeric(result, ToolCallTrajectoryEvaluator.TotalMetricName, 0);
        AssertNumeric(result, ToolCallTrajectoryEvaluator.FailedMetricName, 0);
        AssertNumeric(result, ToolCallTrajectoryEvaluator.SequenceGapsMetricName, 0);
        AssertBoolean(result, ToolCallTrajectoryEvaluator.AllSucceededMetricName, true);
    }

    [Fact]
    public async Task EvaluateAsync_ContiguousSuccessfulCalls_ReturnsExpectedMetrics()
    {
        var diagnostics = BuildDiagnostics(
            MakeCall(0, true),
            MakeCall(1, true),
            MakeCall(2, true));

        var result = await RunAsync(diagnostics);

        AssertNumeric(result, ToolCallTrajectoryEvaluator.TotalMetricName, 3);
        AssertNumeric(result, ToolCallTrajectoryEvaluator.FailedMetricName, 0);
        AssertNumeric(result, ToolCallTrajectoryEvaluator.SequenceGapsMetricName, 0);
        AssertBoolean(result, ToolCallTrajectoryEvaluator.AllSucceededMetricName, true);
    }

    [Fact]
    public async Task EvaluateAsync_OneFailedCall_AllSucceededFalse()
    {
        var diagnostics = BuildDiagnostics(
            MakeCall(0, true),
            MakeCall(1, false),
            MakeCall(2, true));

        var result = await RunAsync(diagnostics);

        AssertNumeric(result, ToolCallTrajectoryEvaluator.TotalMetricName, 3);
        AssertNumeric(result, ToolCallTrajectoryEvaluator.FailedMetricName, 1);
        AssertBoolean(result, ToolCallTrajectoryEvaluator.AllSucceededMetricName, false);
    }

    [Fact]
    public async Task EvaluateAsync_SequenceZeroThenTwo_OneGap()
    {
        var diagnostics = BuildDiagnostics(
            MakeCall(0, true),
            MakeCall(2, true));

        var result = await RunAsync(diagnostics);

        AssertNumeric(result, ToolCallTrajectoryEvaluator.SequenceGapsMetricName, 1);
    }

    [Fact]
    public async Task EvaluateAsync_SequenceOneThenThree_OneGap()
    {
        var diagnostics = BuildDiagnostics(
            MakeCall(1, true),
            MakeCall(3, true));

        var result = await RunAsync(diagnostics);

        AssertNumeric(result, ToolCallTrajectoryEvaluator.SequenceGapsMetricName, 1);
    }

    private async Task<EvaluationResult> RunAsync(IAgentRunDiagnostics diagnostics)
    {
        var evaluator = new ToolCallTrajectoryEvaluator();
        return await evaluator.EvaluateAsync(
            messages: Array.Empty<ChatMessage>(),
            modelResponse: new ChatResponse(),
            chatConfiguration: null,
            additionalContext: [new AgentRunDiagnosticsContext(diagnostics)],
            cancellationToken: _ct);
    }

    private static FakeAgentRunDiagnostics BuildDiagnostics(params ToolCallDiagnostics[] toolCalls)
    {
        return new FakeAgentRunDiagnostics
        {
            AgentName = "test-agent",
            TotalDuration = TimeSpan.FromMilliseconds(1),
            AggregateTokenUsage = new TokenUsage(0, 0, 0, 0, 0),
            ChatCompletions = Array.Empty<ChatCompletionDiagnostics>(),
            ToolCalls = toolCalls,
            TotalInputMessages = 1,
            TotalOutputMessages = 0,
            InputMessages = new List<ChatMessage> { new(ChatRole.User, "Hello.") },
            OutputResponse = null,
            Succeeded = true,
            ErrorMessage = null,
            StartedAt = DateTimeOffset.UnixEpoch,
            CompletedAt = DateTimeOffset.UnixEpoch,
            ExecutionMode = null,
        };
    }

    private static ToolCallDiagnostics MakeCall(int sequence, bool succeeded)
    {
        return new ToolCallDiagnostics(
            Sequence: sequence,
            ToolName: $"tool-{sequence}",
            Duration: TimeSpan.FromMilliseconds(1),
            Succeeded: succeeded,
            ErrorMessage: succeeded ? null : "boom",
            StartedAt: DateTimeOffset.UnixEpoch,
            CompletedAt: DateTimeOffset.UnixEpoch,
            CustomMetrics: null);
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
}
