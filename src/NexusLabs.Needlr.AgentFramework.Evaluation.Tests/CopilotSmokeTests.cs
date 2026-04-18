using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using NexusLabs.Needlr.Copilot;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests;

/// <summary>
/// Smoke test that exercises a real <see cref="RelevanceEvaluator"/> run against a
/// live Copilot-backed judge. Requires a logged-in Copilot CLI or a
/// <c>GH_TOKEN</c>/<c>GITHUB_TOKEN</c> environment variable. If the token cannot
/// be resolved, the test fails loudly with the underlying exception — there is
/// no skip path.
/// </summary>
[Trait("Category", "Integration")]
public sealed class CopilotSmokeTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task RelevanceEvaluator_WithCopilotJudge_ProducesMetric()
    {
        using var judge = new CopilotChatClient(new CopilotChatClientOptions
        {
            DefaultModel = "claude-sonnet-4",
        });

        var evaluator = new RelevanceEvaluator();
        var userPrompt = new ChatMessage(
            ChatRole.User,
            "What is the capital of France?");
        var response = new ChatResponse(
            [new ChatMessage(ChatRole.Assistant, "The capital of France is Paris.")]);

        var result = await evaluator.EvaluateAsync(
            [userPrompt],
            response,
            new ChatConfiguration(judge),
            cancellationToken: _ct);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Metrics);
    }
}
