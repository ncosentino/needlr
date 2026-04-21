using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests;

public sealed class EfficiencyEvaluatorTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task EvaluateAsync_MissingContext_ReturnsEmptyResult()
    {
        var evaluator = new EfficiencyEvaluator();

        var result = await evaluator.EvaluateAsync(
            messages: Array.Empty<ChatMessage>(),
            modelResponse: new ChatResponse(),
            chatConfiguration: null,
            additionalContext: null,
            cancellationToken: _ct);

        Assert.Empty(result.Metrics);
    }

    [Fact]
    public async Task EvaluateAsync_ZeroTokens_AllMetricsAreZero()
    {
        var diagnostics = BuildDiagnostics(
            new TokenUsage(0, 0, 0, 0, 0),
            toolCallCount: 0);

        var result = await RunAsync(diagnostics);

        AssertNumeric(result, EfficiencyEvaluator.TotalTokensMetricName, 0);
        AssertNumeric(result, EfficiencyEvaluator.InputTokenRatioMetricName, 0);
        AssertNumeric(result, EfficiencyEvaluator.TokensPerToolCallMetricName, 0);
        AssertNumeric(result, EfficiencyEvaluator.CacheHitRatioMetricName, 0);
    }

    [Fact]
    public async Task EvaluateAsync_NormalUsage_ComputesCorrectRatios()
    {
        var diagnostics = BuildDiagnostics(
            new TokenUsage(
                InputTokens: 800,
                OutputTokens: 200,
                TotalTokens: 1000,
                CachedInputTokens: 400,
                ReasoningTokens: 0),
            toolCallCount: 5);

        var result = await RunAsync(diagnostics);

        AssertNumeric(result, EfficiencyEvaluator.TotalTokensMetricName, 1000);
        AssertNumericApprox(result, EfficiencyEvaluator.InputTokenRatioMetricName, 0.8);
        AssertNumeric(result, EfficiencyEvaluator.TokensPerToolCallMetricName, 200);
        AssertNumericApprox(result, EfficiencyEvaluator.CacheHitRatioMetricName, 0.5);
    }

    [Fact]
    public async Task EvaluateAsync_NoToolCalls_TokensPerToolCallIsZero()
    {
        var diagnostics = BuildDiagnostics(
            new TokenUsage(100, 50, 150, 0, 0),
            toolCallCount: 0);

        var result = await RunAsync(diagnostics);

        AssertNumeric(result, EfficiencyEvaluator.TokensPerToolCallMetricName, 0);
    }

    [Fact]
    public async Task EvaluateAsync_NoCachedTokens_CacheHitRatioIsZero()
    {
        var diagnostics = BuildDiagnostics(
            new TokenUsage(500, 100, 600, 0, 0),
            toolCallCount: 2);

        var result = await RunAsync(diagnostics);

        AssertNumeric(result, EfficiencyEvaluator.CacheHitRatioMetricName, 0);
    }

    [Fact]
    public async Task EvaluateAsync_AllInputCached_CacheHitRatioIsOne()
    {
        var diagnostics = BuildDiagnostics(
            new TokenUsage(500, 100, 600, 500, 0),
            toolCallCount: 1);

        var result = await RunAsync(diagnostics);

        AssertNumericApprox(result, EfficiencyEvaluator.CacheHitRatioMetricName, 1.0);
    }

    [Fact]
    public async Task EvaluateAsync_UnderBudget_ReturnsTrue()
    {
        var diagnostics = BuildDiagnostics(
            new TokenUsage(500, 100, 600, 0, 0),
            toolCallCount: 1);

        var evaluator = new EfficiencyEvaluator(tokenBudget: 1000);
        var result = await evaluator.EvaluateAsync(
            messages: Array.Empty<ChatMessage>(),
            modelResponse: new ChatResponse(),
            chatConfiguration: null,
            additionalContext: [new AgentRunDiagnosticsContext(diagnostics)],
            cancellationToken: _ct);

        AssertBoolean(result, EfficiencyEvaluator.UnderBudgetMetricName, true);
    }

    [Fact]
    public async Task EvaluateAsync_OverBudget_ReturnsFalse()
    {
        var diagnostics = BuildDiagnostics(
            new TokenUsage(800, 300, 1100, 0, 0),
            toolCallCount: 2);

        var evaluator = new EfficiencyEvaluator(tokenBudget: 1000);
        var result = await evaluator.EvaluateAsync(
            messages: Array.Empty<ChatMessage>(),
            modelResponse: new ChatResponse(),
            chatConfiguration: null,
            additionalContext: [new AgentRunDiagnosticsContext(diagnostics)],
            cancellationToken: _ct);

        AssertBoolean(result, EfficiencyEvaluator.UnderBudgetMetricName, false);
    }

    [Fact]
    public async Task EvaluateAsync_ExactlyAtBudget_ReturnsFalse()
    {
        var diagnostics = BuildDiagnostics(
            new TokenUsage(700, 300, 1000, 0, 0),
            toolCallCount: 1);

        var evaluator = new EfficiencyEvaluator(tokenBudget: 1000);
        var result = await evaluator.EvaluateAsync(
            messages: Array.Empty<ChatMessage>(),
            modelResponse: new ChatResponse(),
            chatConfiguration: null,
            additionalContext: [new AgentRunDiagnosticsContext(diagnostics)],
            cancellationToken: _ct);

        AssertBoolean(result, EfficiencyEvaluator.UnderBudgetMetricName, false);
    }

    [Fact]
    public async Task EvaluateAsync_NoBudgetConfigured_MetricNotInResult()
    {
        var diagnostics = BuildDiagnostics(
            new TokenUsage(100, 50, 150, 0, 0),
            toolCallCount: 1);

        var result = await RunAsync(diagnostics);

        Assert.False(
            result.Metrics.ContainsKey(EfficiencyEvaluator.UnderBudgetMetricName),
            "UnderBudget metric should not be present when tokenBudget is null");
    }

    [Fact]
    public void EvaluationMetricNames_WithBudget_IncludesUnderBudget()
    {
        var evaluator = new EfficiencyEvaluator(tokenBudget: 5000);

        Assert.Contains(EfficiencyEvaluator.UnderBudgetMetricName, evaluator.EvaluationMetricNames);
        Assert.Equal(5, evaluator.EvaluationMetricNames.Count);
    }

    [Fact]
    public void EvaluationMetricNames_WithoutBudget_ExcludesUnderBudget()
    {
        var evaluator = new EfficiencyEvaluator();

        Assert.DoesNotContain(EfficiencyEvaluator.UnderBudgetMetricName, evaluator.EvaluationMetricNames);
        Assert.Equal(4, evaluator.EvaluationMetricNames.Count);
    }

    private async Task<EvaluationResult> RunAsync(IAgentRunDiagnostics diagnostics)
    {
        var evaluator = new EfficiencyEvaluator();
        return await evaluator.EvaluateAsync(
            messages: Array.Empty<ChatMessage>(),
            modelResponse: new ChatResponse(),
            chatConfiguration: null,
            additionalContext: [new AgentRunDiagnosticsContext(diagnostics)],
            cancellationToken: _ct);
    }

    private static FakeAgentRunDiagnostics BuildDiagnostics(
        TokenUsage usage,
        int toolCallCount)
    {
        var toolCalls = new ToolCallDiagnostics[toolCallCount];
        for (var i = 0; i < toolCallCount; i++)
        {
            toolCalls[i] = new ToolCallDiagnostics(
                Sequence: i,
                ToolName: $"tool-{i}",
                Duration: TimeSpan.FromMilliseconds(1),
                Succeeded: true,
                ErrorMessage: null,
                StartedAt: DateTimeOffset.UnixEpoch,
                CompletedAt: DateTimeOffset.UnixEpoch,
                CustomMetrics: null);
        }

        return new FakeAgentRunDiagnostics
        {
            AgentName = "test-agent",
            TotalDuration = TimeSpan.FromMilliseconds(1),
            AggregateTokenUsage = usage,
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

    private static void AssertNumeric(EvaluationResult result, string name, double expected)
    {
        var metric = Assert.IsType<NumericMetric>(result.Metrics[name]);
        Assert.Equal(expected, metric.Value);
    }

    private static void AssertNumericApprox(EvaluationResult result, string name, double expected)
    {
        var metric = Assert.IsType<NumericMetric>(result.Metrics[name]);
        Assert.True(
            Math.Abs(metric.Value!.Value - expected) < 0.001,
            $"Expected {name} ≈ {expected}, got {metric.Value}");
    }

    private static void AssertBoolean(EvaluationResult result, string name, bool expected)
    {
        var metric = Assert.IsType<BooleanMetric>(result.Metrics[name]);
        Assert.Equal(expected, metric.Value);
    }
}
