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
    public async Task EvaluateAndRecordAsync_RunsEachEvaluatorAndRecordsEachResult()
    {
        var result1 = new EvaluationResult(new NumericMetric("m1", value: 1.0));
        var result2 = new EvaluationResult(new BooleanMetric("m2", value: true));
        var messages = new[] { new ChatMessage(ChatRole.User, "hi") };
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "yo"));

        var evaluator1 = CreateEvaluator(messages, response, result1);
        var evaluator2 = CreateEvaluator(messages, response, result2);
        using var listener = LangfuseTestFactory.StartListener();
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseHttpStub.Create(
            LangfuseHttpStub.ScoreAccepted,
            captured);
        var scoreApiClient = LangfuseTestFactory.CreateScoreApiClient(httpClient);
        var failureSink = new LangfuseScoreFailureSink(LangfuseScoreFailureMode.Strict, null);
        var recorder = new LangfuseScoreRecorder(scoreApiClient, failureSink, normalizeNames: false);
        using var scenario = new LangfuseScenario(
            recorder,
            "extension-test",
            sessionId: null,
            userId: null,
            tags: null,
            metadata: null);

        var results = await scenario.EvaluateAndRecordAsync(
            [evaluator1.Object, evaluator2.Object],
            messages,
            response,
            cancellationToken: _cancellationToken);

        Assert.Equal([result1, result2], results);
        Assert.Equal(2, captured.Count);
        _mocks.VerifyAll();
    }

    private Mock<IEvaluator> CreateEvaluator(
        IReadOnlyList<ChatMessage> messages,
        ChatResponse response,
        EvaluationResult result)
    {
        var evaluator = _mocks.Create<IEvaluator>();
        evaluator
            .Setup(e => e.EvaluateAsync(
                messages,
                response,
                null,
                null,
                _cancellationToken))
            .ReturnsAsync(result);
        return evaluator;
    }
}
