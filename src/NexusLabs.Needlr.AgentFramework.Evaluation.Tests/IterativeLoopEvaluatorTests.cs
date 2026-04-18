using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Iterative;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests;

public sealed class IterativeLoopEvaluatorTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private static IterativeLoopResult CreateLoopResult(
        FakeAgentRunDiagnostics? diagnostics)
    {
        return new IterativeLoopResult(
            Iterations: Array.Empty<IterationRecord>(),
            FinalResponse: null,
            Diagnostics: diagnostics,
            Succeeded: true,
            ErrorMessage: null,
            Termination: TerminationReason.Completed,
            Configuration: new IterativeLoopConfiguration(
                ToolResultMode: ToolResultMode.MultiRound,
                MaxIterations: 1,
                MaxToolRoundsPerIteration: 1,
                MaxTotalToolCalls: null,
                BudgetPressureThreshold: null,
                LoopName: "test"));
    }

    [Fact]
    public async Task EvaluateAsync_NullLoopResult_ThrowsArgumentNullException()
    {
        var config = new ChatConfiguration(new ThrowingChatClient());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => IterativeLoopEvaluator.EvaluateAsync(
                loopResult: null!,
                evaluators: new[] { new FakeEvaluator() },
                chatConfiguration: config,
                cancellationToken: _ct));
    }

    [Fact]
    public async Task EvaluateAsync_NullEvaluators_ThrowsArgumentNullException()
    {
        var loopResult = CreateLoopResult(FakeAgentRunDiagnostics.Create());
        var config = new ChatConfiguration(new ThrowingChatClient());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => IterativeLoopEvaluator.EvaluateAsync(
                loopResult,
                evaluators: null!,
                chatConfiguration: config,
                cancellationToken: _ct));
    }

    [Fact]
    public async Task EvaluateAsync_NullChatConfiguration_ThrowsArgumentNullException()
    {
        var loopResult = CreateLoopResult(FakeAgentRunDiagnostics.Create());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => IterativeLoopEvaluator.EvaluateAsync(
                loopResult,
                evaluators: new[] { new FakeEvaluator() },
                chatConfiguration: null!,
                cancellationToken: _ct));
    }

    [Fact]
    public async Task EvaluateAsync_NullDiagnostics_ReturnsEmptyResult()
    {
        var loopResult = CreateLoopResult(diagnostics: null);
        var config = new ChatConfiguration(new ThrowingChatClient());
        var evaluator = new FakeEvaluator();

        var result = await IterativeLoopEvaluator.EvaluateAsync(
            loopResult,
            new[] { evaluator },
            config,
            _ct);

        Assert.Empty(result.Items);
        Assert.Equal(0, evaluator.CallCount);
    }

    [Fact]
    public async Task EvaluateAsync_NonNullDiagnostics_DelegatesToWorkflowEvaluator()
    {
        var loopResult = CreateLoopResult(FakeAgentRunDiagnostics.Create());
        var config = new ChatConfiguration(new ThrowingChatClient());
        var a = new FakeEvaluator("A");
        var b = new FakeEvaluator("B");

        var result = await IterativeLoopEvaluator.EvaluateAsync(
            loopResult,
            new IEvaluator[] { a, b },
            config,
            _ct);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, a.CallCount);
        Assert.Equal(1, b.CallCount);
    }
}
