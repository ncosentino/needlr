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

    [Fact]
    public async Task EvaluateAsync_ThreeConsecutiveSameToolCalls_ConsecutiveCountTwo()
    {
        var diagnostics = BuildDiagnostics(
            MakeNamedCall(0, "search", true),
            MakeNamedCall(1, "search", true),
            MakeNamedCall(2, "search", true));

        var result = await RunAsync(diagnostics);

        AssertNumeric(result, ToolCallTrajectoryEvaluator.ConsecutiveSameToolMetricName, 2);
    }

    [Fact]
    public async Task EvaluateAsync_AlternatingToolNames_ConsecutiveCountZero()
    {
        var diagnostics = BuildDiagnostics(
            MakeNamedCall(0, "search", true),
            MakeNamedCall(1, "write", true),
            MakeNamedCall(2, "search", true));

        var result = await RunAsync(diagnostics);

        AssertNumeric(result, ToolCallTrajectoryEvaluator.ConsecutiveSameToolMetricName, 0);
    }

    [Fact]
    public async Task EvaluateAsync_EmptyToolCalls_ConsecutiveCountZero()
    {
        var diagnostics = BuildDiagnostics();

        var result = await RunAsync(diagnostics);

        AssertNumeric(result, ToolCallTrajectoryEvaluator.ConsecutiveSameToolMetricName, 0);
    }

    [Fact]
    public async Task EvaluateAsync_MixedFailures_PerToolFailureRateHasCorrectJson()
    {
        var diagnostics = BuildDiagnostics(
            MakeNamedCall(0, "search", true),
            MakeNamedCall(1, "search", false),
            MakeNamedCall(2, "search", true),
            MakeNamedCall(3, "write", false));

        var result = await RunAsync(diagnostics);

        var metric = Assert.IsType<StringMetric>(
            result.Metrics[ToolCallTrajectoryEvaluator.PerToolFailureRateMetricName]);
        var parsed = System.Text.Json.JsonSerializer
            .Deserialize<Dictionary<string, double>>(metric.Value!);
        Assert.NotNull(parsed);
        Assert.Equal(2, parsed.Count);
        Assert.True(
            Math.Abs(parsed["search"] - (1.0 / 3.0)) < 0.001,
            $"Expected search failure rate ~0.333, got {parsed["search"]}");
        Assert.Equal(1.0, parsed["write"]);
    }

    [Fact]
    public async Task EvaluateAsync_AllSucceed_PerToolFailureRateAllZero()
    {
        var diagnostics = BuildDiagnostics(
            MakeNamedCall(0, "search", true),
            MakeNamedCall(1, "write", true));

        var result = await RunAsync(diagnostics);

        var metric = Assert.IsType<StringMetric>(
            result.Metrics[ToolCallTrajectoryEvaluator.PerToolFailureRateMetricName]);
        var parsed = System.Text.Json.JsonSerializer
            .Deserialize<Dictionary<string, double>>(metric.Value!);
        Assert.NotNull(parsed);
        Assert.Equal(0.0, parsed["search"]);
        Assert.Equal(0.0, parsed["write"]);
    }

    [Fact]
    public async Task EvaluateAsync_NoToolCalls_PerToolFailureRateEmptyJson()
    {
        var diagnostics = BuildDiagnostics();

        var result = await RunAsync(diagnostics);

        var metric = Assert.IsType<StringMetric>(
            result.Metrics[ToolCallTrajectoryEvaluator.PerToolFailureRateMetricName]);
        Assert.Equal("{}", metric.Value);
    }

    [Fact]
    public async Task EvaluateAsync_FiveToolCalls_LatencyP50IsMedian()
    {
        var diagnostics = BuildDiagnostics(
            MakeTimedCall(0, TimeSpan.FromMilliseconds(10)),
            MakeTimedCall(1, TimeSpan.FromMilliseconds(50)),
            MakeTimedCall(2, TimeSpan.FromMilliseconds(30)),
            MakeTimedCall(3, TimeSpan.FromMilliseconds(20)),
            MakeTimedCall(4, TimeSpan.FromMilliseconds(40)));

        var result = await RunAsync(diagnostics);

        AssertNumeric(result, ToolCallTrajectoryEvaluator.LatencyP50MetricName, 30);
    }

    [Fact]
    public async Task EvaluateAsync_FiveToolCalls_LatencyP95IsHighEnd()
    {
        var diagnostics = BuildDiagnostics(
            MakeTimedCall(0, TimeSpan.FromMilliseconds(10)),
            MakeTimedCall(1, TimeSpan.FromMilliseconds(50)),
            MakeTimedCall(2, TimeSpan.FromMilliseconds(30)),
            MakeTimedCall(3, TimeSpan.FromMilliseconds(20)),
            MakeTimedCall(4, TimeSpan.FromMilliseconds(40)));

        var result = await RunAsync(diagnostics);

        AssertNumeric(result, ToolCallTrajectoryEvaluator.LatencyP95MetricName, 50);
    }

    [Fact]
    public async Task EvaluateAsync_SingleToolCall_LatencyP50AndP95AreSameValue()
    {
        var diagnostics = BuildDiagnostics(
            MakeTimedCall(0, TimeSpan.FromMilliseconds(42)));

        var result = await RunAsync(diagnostics);

        AssertNumeric(result, ToolCallTrajectoryEvaluator.LatencyP50MetricName, 42);
        AssertNumeric(result, ToolCallTrajectoryEvaluator.LatencyP95MetricName, 42);
    }

    [Fact]
    public async Task EvaluateAsync_NoToolCalls_LatencyP50AndP95AreZero()
    {
        var diagnostics = BuildDiagnostics();

        var result = await RunAsync(diagnostics);

        AssertNumeric(result, ToolCallTrajectoryEvaluator.LatencyP50MetricName, 0);
        AssertNumeric(result, ToolCallTrajectoryEvaluator.LatencyP95MetricName, 0);
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

    private static ToolCallDiagnostics MakeNamedCall(int sequence, string toolName, bool succeeded)
    {
        return new ToolCallDiagnostics(
            Sequence: sequence,
            ToolName: toolName,
            Duration: TimeSpan.FromMilliseconds(1),
            Succeeded: succeeded,
            ErrorMessage: succeeded ? null : "boom",
            StartedAt: DateTimeOffset.UnixEpoch,
            CompletedAt: DateTimeOffset.UnixEpoch,
            CustomMetrics: null);
    }

    private static ToolCallDiagnostics MakeTimedCall(int sequence, TimeSpan duration)
    {
        return new ToolCallDiagnostics(
            Sequence: sequence,
            ToolName: $"tool-{sequence}",
            Duration: duration,
            Succeeded: true,
            ErrorMessage: null,
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
