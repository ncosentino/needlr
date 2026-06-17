using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

using Moq;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseEvaluationScoreExtensionsTests
{
    [Fact]
    public async Task EvaluateAndRecordAsync_RunsEachEvaluatorAndRecordsEachResult()
    {
        var result1 = new EvaluationResult(new NumericMetric("m1", value: 1.0));
        var result2 = new EvaluationResult(new BooleanMetric("m2", value: true));

        var evaluator1 = CreateEvaluator(result1);
        var evaluator2 = CreateEvaluator(result2);

        var scenario = new Mock<ILangfuseScenario>(MockBehavior.Strict);
        scenario
            .Setup(s => s.RecordEvaluationAsync(It.IsAny<EvaluationResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var messages = new[] { new ChatMessage(ChatRole.User, "hi") };
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "yo"));

        var results = await scenario.Object.EvaluateAndRecordAsync(
            [evaluator1.Object, evaluator2.Object],
            messages,
            response,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([result1, result2], results);
        scenario.Verify(s => s.RecordEvaluationAsync(result1, It.IsAny<CancellationToken>()), Times.Once);
        scenario.Verify(s => s.RecordEvaluationAsync(result2, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<IEvaluator> CreateEvaluator(EvaluationResult result)
    {
        var evaluator = new Mock<IEvaluator>(MockBehavior.Strict);
        evaluator
            .Setup(e => e.EvaluateAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatResponse>(),
                It.IsAny<ChatConfiguration?>(),
                It.IsAny<IEnumerable<EvaluationContext>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return evaluator;
    }
}
