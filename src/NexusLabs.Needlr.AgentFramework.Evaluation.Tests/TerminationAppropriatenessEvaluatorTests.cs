using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests;

public sealed class TerminationAppropriatenessEvaluatorTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task EvaluateAsync_MissingContext_ReturnsEmptyResult()
    {
        var result = await RunAsync(context: null);

        Assert.Empty(result.Metrics);
    }

    [Fact]
    public async Task EvaluateAsync_SucceededAndNoError_TerminationConsistentTrue()
    {
        var diagnostics = Build(succeeded: true, errorMessage: null, executionMode: "IterativeLoop");

        var result = await RunAsync(diagnostics);

        AssertBoolean(result, TerminationAppropriatenessEvaluator.RunSucceededMetricName, true);
        AssertBoolean(result, TerminationAppropriatenessEvaluator.TerminationConsistentMetricName, true);
    }

    [Fact]
    public async Task EvaluateAsync_FailedWithError_TerminationConsistentTrue()
    {
        var diagnostics = Build(succeeded: false, errorMessage: "boom", executionMode: "IterativeLoop");

        var result = await RunAsync(diagnostics);

        AssertBoolean(result, TerminationAppropriatenessEvaluator.RunSucceededMetricName, false);
        AssertBoolean(result, TerminationAppropriatenessEvaluator.TerminationConsistentMetricName, true);
    }

    [Fact]
    public async Task EvaluateAsync_SucceededWithError_TerminationConsistentFalse()
    {
        var diagnostics = Build(succeeded: true, errorMessage: "boom", executionMode: "IterativeLoop");

        var result = await RunAsync(diagnostics);

        AssertBoolean(result, TerminationAppropriatenessEvaluator.RunSucceededMetricName, true);
        AssertBoolean(result, TerminationAppropriatenessEvaluator.TerminationConsistentMetricName, false);
    }

    [Fact]
    public async Task EvaluateAsync_FailedWithoutError_TerminationConsistentFalse()
    {
        var diagnostics = Build(succeeded: false, errorMessage: null, executionMode: "IterativeLoop");

        var result = await RunAsync(diagnostics);

        AssertBoolean(result, TerminationAppropriatenessEvaluator.RunSucceededMetricName, false);
        AssertBoolean(result, TerminationAppropriatenessEvaluator.TerminationConsistentMetricName, false);
    }

    [Fact]
    public async Task EvaluateAsync_ExecutionModeNull_EmitsUnknown()
    {
        var diagnostics = Build(succeeded: true, errorMessage: null, executionMode: null);

        var result = await RunAsync(diagnostics);

        AssertString(
            result,
            TerminationAppropriatenessEvaluator.ExecutionModeMetricName,
            TerminationAppropriatenessEvaluator.UnknownExecutionMode);
    }

    [Fact]
    public async Task EvaluateAsync_ExecutionModeIterativeLoop_EmitsMode()
    {
        var diagnostics = Build(succeeded: true, errorMessage: null, executionMode: "IterativeLoop");

        var result = await RunAsync(diagnostics);

        AssertString(
            result,
            TerminationAppropriatenessEvaluator.ExecutionModeMetricName,
            "IterativeLoop");
    }

    private async Task<EvaluationResult> RunAsync(IAgentRunDiagnostics? context)
    {
        var evaluator = new TerminationAppropriatenessEvaluator();
        IEnumerable<EvaluationContext>? additional = context is null
            ? null
            : [new AgentRunDiagnosticsContext(context)];
        return await evaluator.EvaluateAsync(
            messages: Array.Empty<ChatMessage>(),
            modelResponse: new ChatResponse(),
            chatConfiguration: null,
            additionalContext: additional,
            cancellationToken: _ct);
    }

    private static FakeAgentRunDiagnostics Build(
        bool succeeded,
        string? errorMessage,
        string? executionMode)
    {
        return new FakeAgentRunDiagnostics
        {
            AgentName = "test-agent",
            TotalDuration = TimeSpan.FromMilliseconds(1),
            AggregateTokenUsage = new TokenUsage(0, 0, 0, 0, 0),
            ChatCompletions = Array.Empty<ChatCompletionDiagnostics>(),
            ToolCalls = Array.Empty<ToolCallDiagnostics>(),
            TotalInputMessages = 1,
            TotalOutputMessages = 0,
            InputMessages = new List<ChatMessage> { new(ChatRole.User, "Hello.") },
            OutputResponse = null,
            Succeeded = succeeded,
            ErrorMessage = errorMessage,
            StartedAt = DateTimeOffset.UnixEpoch,
            CompletedAt = DateTimeOffset.UnixEpoch,
            ExecutionMode = executionMode,
        };
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
