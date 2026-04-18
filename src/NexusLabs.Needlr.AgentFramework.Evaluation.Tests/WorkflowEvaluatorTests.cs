using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests;

public sealed class WorkflowEvaluatorTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task EvaluateAsync_NullDiagnostics_ThrowsArgumentNullException()
    {
        var config = new ChatConfiguration(new ThrowingChatClient());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => WorkflowEvaluator.EvaluateAsync(
                diagnostics: null!,
                evaluators: new[] { new FakeEvaluator() },
                chatConfiguration: config,
                cancellationToken: _ct));
    }

    [Fact]
    public async Task EvaluateAsync_NullEvaluators_ThrowsArgumentNullException()
    {
        var diagnostics = FakeAgentRunDiagnostics.Create();
        var config = new ChatConfiguration(new ThrowingChatClient());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => WorkflowEvaluator.EvaluateAsync(
                diagnostics,
                evaluators: null!,
                chatConfiguration: config,
                cancellationToken: _ct));
    }

    [Fact]
    public async Task EvaluateAsync_NullChatConfiguration_ThrowsArgumentNullException()
    {
        var diagnostics = FakeAgentRunDiagnostics.Create();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => WorkflowEvaluator.EvaluateAsync(
                diagnostics,
                evaluators: new[] { new FakeEvaluator() },
                chatConfiguration: null!,
                cancellationToken: _ct));
    }

    [Fact]
    public async Task EvaluateAsync_NullEvaluatorEntry_ThrowsArgumentNullException()
    {
        var diagnostics = FakeAgentRunDiagnostics.Create();
        var config = new ChatConfiguration(new ThrowingChatClient());
        var evaluators = new IEvaluator?[] { new FakeEvaluator(), null };

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => WorkflowEvaluator.EvaluateAsync(
                diagnostics,
                evaluators: evaluators!,
                chatConfiguration: config,
                cancellationToken: _ct));
    }

    [Fact]
    public async Task EvaluateAsync_SingleEvaluator_ProducesOneItem()
    {
        var diagnostics = FakeAgentRunDiagnostics.Create();
        var config = new ChatConfiguration(new ThrowingChatClient());
        var evaluator = new FakeEvaluator("Relevance", 0.9);

        var result = await WorkflowEvaluator.EvaluateAsync(
            diagnostics,
            new[] { evaluator },
            config,
            _ct);

        Assert.Single(result.Items);
        Assert.Equal(1, evaluator.CallCount);
    }

    [Fact]
    public async Task EvaluateAsync_MultipleEvaluators_ProducesItemPerEvaluator()
    {
        var diagnostics = FakeAgentRunDiagnostics.Create();
        var config = new ChatConfiguration(new ThrowingChatClient());
        var a = new FakeEvaluator("A", 0.1);
        var b = new FakeEvaluator("B", 0.2);
        var c = new FakeEvaluator("C", 0.3);

        var result = await WorkflowEvaluator.EvaluateAsync(
            diagnostics,
            new IEvaluator[] { a, b, c },
            config,
            _ct);

        Assert.Equal(3, result.Items.Count);
        Assert.Equal(1, a.CallCount);
        Assert.Equal(1, b.CallCount);
        Assert.Equal(1, c.CallCount);
    }

    [Fact]
    public async Task EvaluateAsync_LabelIsEvaluatorTypeName()
    {
        var diagnostics = FakeAgentRunDiagnostics.Create();
        var config = new ChatConfiguration(new ThrowingChatClient());

        var result = await WorkflowEvaluator.EvaluateAsync(
            diagnostics,
            new[] { new FakeEvaluator() },
            config,
            _ct);

        Assert.Equal(nameof(FakeEvaluator), result.Items[0].Label);
    }

    [Fact]
    public async Task EvaluateAsync_ThreadsDiagnosticsMessagesToEvaluator()
    {
        var inputs = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a helpful assistant."),
            new(ChatRole.User, "Explain DI."),
        };
        var diagnostics = FakeAgentRunDiagnostics.Create(inputMessages: inputs);
        var config = new ChatConfiguration(new ThrowingChatClient());
        var evaluator = new FakeEvaluator();

        await WorkflowEvaluator.EvaluateAsync(
            diagnostics,
            new[] { evaluator },
            config,
            _ct);

        Assert.Same(inputs, evaluator.LastMessages);
        Assert.Same(config, evaluator.LastChatConfiguration);
    }
}
