using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

using Moq;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseEvaluationScoreExtensionsTests
{
    private readonly MockRepository _mocks = new(MockBehavior.Strict);
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordLangfuseScoresAsync_RequiredOnly_ForwardsNullOptionsAndNoCancellation()
    {
        var result = new EvaluationResult(new NumericMetric("m1", value: 1.0));
        var scenario = _mocks.Create<ILangfuseScenario>();
        scenario
            .Setup(value => value.RecordEvaluationAsync(
                result,
                null,
                CancellationToken.None))
            .Returns(Task.CompletedTask);

#pragma warning disable xUnit1051 // This test intentionally exercises the tokenless extension overload.
        await result.RecordLangfuseScoresAsync(scenario.Object);
#pragma warning restore xUnit1051

        _mocks.VerifyAll();
    }

    [Fact]
    public async Task RecordLangfuseScoresAsync_FullOverload_ForwardsOptionsAndCancellation()
    {
        var result = new EvaluationResult(new NumericMetric("m1", value: 1.0));
        var options = new LangfuseEvaluationScoreOptions
        {
            ScoreIdProvider = metric => $"score:{metric.Name}",
        };
        var scenario = _mocks.Create<ILangfuseScenario>();
        scenario
            .Setup(value => value.RecordEvaluationAsync(
                result,
                options,
                _cancellationToken))
            .Returns(Task.CompletedTask);

        await result.RecordLangfuseScoresAsync(
            scenario.Object,
            options,
            _cancellationToken);

        _mocks.VerifyAll();
    }

    [Fact]
    public async Task EvaluateAndRecordAsync_RequiredOnly_UsesDefaultsAndPreservesOrder()
    {
        var result1 = new EvaluationResult(new NumericMetric("m1", value: 1.0));
        var result2 = new EvaluationResult(new BooleanMetric("m2", value: true));
        var messages = new[] { new ChatMessage(ChatRole.User, "hi") };
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "yo"));
        var sequence = new MockSequence();
        var evaluator1 = CreateEvaluator(
            sequence,
            messages,
            response,
            chatConfiguration: null,
            additionalContext: null,
            CancellationToken.None,
            result1);
        var scenario = _mocks.Create<ILangfuseScenario>();
        scenario
            .InSequence(sequence)
            .Setup(value => value.RecordEvaluationAsync(
                result1,
                null,
                CancellationToken.None))
            .Returns(Task.CompletedTask);
        var evaluator2 = CreateEvaluator(
            sequence,
            messages,
            response,
            chatConfiguration: null,
            additionalContext: null,
            CancellationToken.None,
            result2);
        scenario
            .InSequence(sequence)
            .Setup(value => value.RecordEvaluationAsync(
                result2,
                null,
                CancellationToken.None))
            .Returns(Task.CompletedTask);

#pragma warning disable xUnit1051 // This test intentionally exercises the tokenless extension overload.
        var results = await scenario.Object.EvaluateAndRecordAsync(
            [evaluator1.Object, evaluator2.Object],
            messages,
            response);
#pragma warning restore xUnit1051

        Assert.Equal([result1, result2], results);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task EvaluateAndRecordAsync_FullOverload_ForwardsConfigurationAndCancellation()
    {
        var result1 = new EvaluationResult(new NumericMetric("m1", value: 1.0));
        var result2 = new EvaluationResult(new BooleanMetric("m2", value: true));
        var messages = new[] { new ChatMessage(ChatRole.User, "hi") };
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "yo"));
        var chatConfiguration = new ChatConfiguration(_mocks.Create<IChatClient>().Object);
        var context = new TestEvaluationContext("context");
        var additionalContext = new[] { context };
        var scoreOptions = new LangfuseEvaluationScoreOptions
        {
            ScoreIdProvider = metric => $"score:{metric.Name}",
        };
        var options = new LangfuseEvaluateAndRecordOptions(
            chatConfiguration,
            additionalContext,
            scoreOptions);
        var sequence = new MockSequence();
        var evaluator1 = CreateEvaluator(
            sequence,
            messages,
            response,
            chatConfiguration,
            additionalContext,
            _cancellationToken,
            result1);
        var scenario = _mocks.Create<ILangfuseScenario>();
        scenario
            .InSequence(sequence)
            .Setup(value => value.RecordEvaluationAsync(
                result1,
                scoreOptions,
                _cancellationToken))
            .Returns(Task.CompletedTask);
        var evaluator2 = CreateEvaluator(
            sequence,
            messages,
            response,
            chatConfiguration,
            additionalContext,
            _cancellationToken,
            result2);
        scenario
            .InSequence(sequence)
            .Setup(value => value.RecordEvaluationAsync(
                result2,
                scoreOptions,
                _cancellationToken))
            .Returns(Task.CompletedTask);

        var results = await scenario.Object.EvaluateAndRecordAsync(
            [evaluator1.Object, evaluator2.Object],
            messages,
            response,
            options,
            _cancellationToken);

        Assert.Equal([result1, result2], results);
        _mocks.VerifyAll();
    }

    [Fact]
    public void EvaluateAndRecordAsync_FullOverload_RejectsNullOptions()
    {
        var scenario = _mocks.Create<ILangfuseScenario>();
        var messages = new[] { new ChatMessage(ChatRole.User, "hi") };
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "yo"));

        Assert.Throws<ArgumentNullException>(
            () =>
            {
                _ = scenario.Object.EvaluateAndRecordAsync(
                    [],
                    messages,
                    response,
                    null!,
                    _cancellationToken);
            });
    }

    [Fact]
    public void EvaluateAndRecordOptions_SnapshotsAdditionalContext()
    {
        var first = new TestEvaluationContext("first");
        var second = new TestEvaluationContext("second");
        var source = new List<EvaluationContext> { first };

        var options = new LangfuseEvaluateAndRecordOptions(
            chatConfiguration: null,
            source,
            scoreOptions: null);
        source[0] = second;
        source.Add(second);

        Assert.Equal([first], options.AdditionalContext);
        Assert.Empty(new LangfuseEvaluateAndRecordOptions(null, null, null).AdditionalContext);
    }

    [Fact]
    public void EvaluateAndRecordOptions_SnapshotsScoreOptions()
    {
        Func<EvaluationMetric, string?> originalProvider =
            metric => $"original:{metric.Name}";
        var source = new LangfuseEvaluationScoreOptions
        {
            ScoreIdProvider = originalProvider,
        };
        var options = new LangfuseEvaluateAndRecordOptions(
            chatConfiguration: null,
            additionalContext: null,
            source);

        source.ScoreIdProvider = metric => $"changed:{metric.Name}";

        Assert.NotSame(source, options.ScoreOptions);
        Assert.Same(originalProvider, options.ScoreOptions!.ScoreIdProvider);
    }

    [Fact]
    public async Task EvaluateAndRecordAsync_PropagatesEvaluatorFailureAndStops()
    {
        var messages = new[] { new ChatMessage(ChatRole.User, "hi") };
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "yo"));
        var failure = new InvalidOperationException("evaluation failed");
        var evaluator = _mocks.Create<IEvaluator>();
        evaluator
            .Setup(value => value.EvaluateAsync(
                messages,
                response,
                null,
                null,
                CancellationToken.None))
            .ThrowsAsync(failure);
        var uncalledEvaluator = _mocks.Create<IEvaluator>();
        var scenario = _mocks.Create<ILangfuseScenario>();

#pragma warning disable xUnit1051 // This test intentionally exercises the tokenless extension overload.
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => scenario.Object.EvaluateAndRecordAsync(
                [evaluator.Object, uncalledEvaluator.Object],
                messages,
                response));
#pragma warning restore xUnit1051

        Assert.Same(failure, exception);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task EvaluateAndRecordAsync_DisabledScenario_StillRunsEvaluators()
    {
        var result = new EvaluationResult(new NumericMetric("m1", value: 1.0));
        var messages = new[] { new ChatMessage(ChatRole.User, "hi") };
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "yo"));
        var evaluator = CreateEvaluator(
            sequence: null,
            messages,
            response,
            chatConfiguration: null,
            additionalContext: null,
            CancellationToken.None,
            result);
        using var scenario = new DisabledLangfuseScenario();

#pragma warning disable xUnit1051 // This test intentionally exercises the tokenless extension overload.
        var results = await scenario.EvaluateAndRecordAsync(
            [evaluator.Object],
            messages,
            response);
#pragma warning restore xUnit1051

        Assert.Equal([result], results);
        _mocks.VerifyAll();
    }

    private Mock<IEvaluator> CreateEvaluator(
        MockSequence? sequence,
        IReadOnlyList<ChatMessage> messages,
        ChatResponse response,
        ChatConfiguration? chatConfiguration,
        IEnumerable<EvaluationContext>? additionalContext,
        CancellationToken cancellationToken,
        EvaluationResult result)
    {
        var evaluator = _mocks.Create<IEvaluator>();
        var setup = sequence is null
            ? evaluator.Setup(e => e.EvaluateAsync(
                messages,
                response,
                chatConfiguration,
                It.Is<IEnumerable<EvaluationContext>?>(value =>
                    ContextsEqual(value, additionalContext)),
                cancellationToken))
            : evaluator
                .InSequence(sequence)
                .Setup(e => e.EvaluateAsync(
                    messages,
                    response,
                    chatConfiguration,
                    It.Is<IEnumerable<EvaluationContext>?>(value =>
                        ContextsEqual(value, additionalContext)),
                    cancellationToken));
        setup.ReturnsAsync(result);
        return evaluator;
    }

    private static bool ContextsEqual(
        IEnumerable<EvaluationContext>? actual,
        IEnumerable<EvaluationContext>? expected) =>
        actual is null
            ? expected is null
            : expected is not null && actual.SequenceEqual(expected);

    private sealed class TestEvaluationContext(string name)
        : EvaluationContext(name, [])
    {
    }
}
