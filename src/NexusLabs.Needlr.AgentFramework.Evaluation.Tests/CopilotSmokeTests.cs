using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using NexusLabs.Needlr.AgentFramework.Evaluation.Testing;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests;

public sealed class CopilotSmokeTests : IClassFixture<NeedlrEvaluationFixture>
{
    private readonly NeedlrEvaluationFixture _fixture;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public CopilotSmokeTests(NeedlrEvaluationFixture fixture)
    {
        _fixture = fixture;
    }

    [NeedlrEvaluationFact]
    public async Task RelevanceEvaluator_WithCopilotJudge_ProducesMetric()
    {
        var evaluator = new RelevanceEvaluator();
        var userPrompt = new ChatMessage(
            ChatRole.User,
            "What is the capital of France?");
        var response = new ChatResponse(
            [new ChatMessage(ChatRole.Assistant, "The capital of France is Paris.")]);

        var result = await evaluator.EvaluateAsync(
            [userPrompt],
            response,
            new ChatConfiguration(_fixture.Judge),
            cancellationToken: _ct);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Metrics);
    }
}
