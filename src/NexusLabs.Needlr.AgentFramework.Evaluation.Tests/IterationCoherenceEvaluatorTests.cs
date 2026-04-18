using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests;

public sealed class IterationCoherenceEvaluatorTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task EvaluateAsync_MissingContext_ReturnsEmptyResult()
    {
        var result = await RunAsync(context: null);

        Assert.Empty(result.Metrics);
    }

    [Fact]
    public async Task EvaluateAsync_ExecutionModeNull_ReturnsEmptyResult()
    {
        var diagnostics = Build(executionMode: null, succeeded: true);

        var result = await RunAsync(diagnostics);

        Assert.Empty(result.Metrics);
    }

    [Fact]
    public async Task EvaluateAsync_ExecutionModeFunctionInvoking_ReturnsEmptyResult()
    {
        var diagnostics = Build(
            executionMode: "FunctionInvokingChatClient",
            succeeded: true,
            completions: [MakeCompletion(0, responseCharCount: 10)]);

        var result = await RunAsync(diagnostics);

        Assert.Empty(result.Metrics);
    }

    [Fact]
    public async Task EvaluateAsync_IterativeLoopSuccessfulWithFinalOutput_TerminatedCoherentlyTrue()
    {
        var diagnostics = Build(
            executionMode: IterationCoherenceEvaluator.IterativeLoopExecutionMode,
            succeeded: true,
            completions: [
                MakeCompletion(0, responseCharCount: 50),
                MakeCompletion(1, responseCharCount: 75),
            ]);

        var result = await RunAsync(diagnostics);

        AssertNumeric(result, IterationCoherenceEvaluator.IterationCountMetricName, 2);
        AssertNumeric(result, IterationCoherenceEvaluator.EmptyOutputsMetricName, 0);
        AssertBoolean(result, IterationCoherenceEvaluator.TerminatedCoherentlyMetricName, true);
    }

    [Fact]
    public async Task EvaluateAsync_FinalIterationEmpty_TerminatedCoherentlyFalse()
    {
        var diagnostics = Build(
            executionMode: IterationCoherenceEvaluator.IterativeLoopExecutionMode,
            succeeded: true,
            completions: [
                MakeCompletion(0, responseCharCount: 50),
                MakeCompletion(1, responseCharCount: 0),
            ]);

        var result = await RunAsync(diagnostics);

        AssertNumeric(result, IterationCoherenceEvaluator.EmptyOutputsMetricName, 1);
        AssertBoolean(result, IterationCoherenceEvaluator.TerminatedCoherentlyMetricName, false);
    }

    [Fact]
    public async Task EvaluateAsync_SucceededFalse_TerminatedCoherentlyFalse()
    {
        var diagnostics = Build(
            executionMode: IterationCoherenceEvaluator.IterativeLoopExecutionMode,
            succeeded: false,
            completions: [MakeCompletion(0, responseCharCount: 50)]);

        var result = await RunAsync(diagnostics);

        AssertBoolean(result, IterationCoherenceEvaluator.TerminatedCoherentlyMetricName, false);
    }

    [Fact]
    public async Task EvaluateAsync_EmptyCompletions_CountZeroAndCoherentFalse()
    {
        var diagnostics = Build(
            executionMode: IterationCoherenceEvaluator.IterativeLoopExecutionMode,
            succeeded: true,
            completions: Array.Empty<ChatCompletionDiagnostics>());

        var result = await RunAsync(diagnostics);

        AssertNumeric(result, IterationCoherenceEvaluator.IterationCountMetricName, 0);
        AssertBoolean(result, IterationCoherenceEvaluator.TerminatedCoherentlyMetricName, false);
    }

    private async Task<EvaluationResult> RunAsync(IAgentRunDiagnostics? context)
    {
        var evaluator = new IterationCoherenceEvaluator();
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
        string? executionMode,
        bool succeeded,
        IReadOnlyList<ChatCompletionDiagnostics>? completions = null)
    {
        return new FakeAgentRunDiagnostics
        {
            AgentName = "test-agent",
            TotalDuration = TimeSpan.FromMilliseconds(1),
            AggregateTokenUsage = new TokenUsage(0, 0, 0, 0, 0),
            ChatCompletions = completions ?? Array.Empty<ChatCompletionDiagnostics>(),
            ToolCalls = Array.Empty<ToolCallDiagnostics>(),
            TotalInputMessages = 1,
            TotalOutputMessages = 0,
            InputMessages = new List<ChatMessage> { new(ChatRole.User, "Hello.") },
            OutputResponse = null,
            Succeeded = succeeded,
            ErrorMessage = succeeded ? null : "boom",
            StartedAt = DateTimeOffset.UnixEpoch,
            CompletedAt = DateTimeOffset.UnixEpoch,
            ExecutionMode = executionMode,
        };
    }

    private static ChatCompletionDiagnostics MakeCompletion(int sequence, long responseCharCount)
    {
        return new ChatCompletionDiagnostics(
            Sequence: sequence,
            Model: "test-model",
            Tokens: new TokenUsage(0, 0, 0, 0, 0),
            InputMessageCount: 1,
            Duration: TimeSpan.FromMilliseconds(1),
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UnixEpoch,
            CompletedAt: DateTimeOffset.UnixEpoch)
        {
            ResponseCharCount = responseCharCount,
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
}
