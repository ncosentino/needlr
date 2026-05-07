using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Needlr.AgentFramework.Iterative;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public sealed partial class IterativeAgentLoopTests
{
    [Fact]
    public async Task RunAsync_PartialIterationAfterLlmCall_PreservesTokensAndLlmCallCount()
    {
        var callCount = 0;
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    var response = CreateToolCallResponse(("noop", "c1", null));
                    response.Usage = new UsageDetails
                    {
                        InputTokenCount = 5000,
                        OutputTokenCount = 200,
                        TotalTokenCount = 5200,
                        CachedInputTokenCount = 3000,
                        ReasoningTokenCount = 50,
                    };
                    return response;
                }

                throw new TaskCanceledException(
                    "Timeout",
                    new TimeoutException("The operation timed out."));
            });

        var tool = CreateTool("noop", () => "ok");
        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            maxIterations: 5,
            toolResultMode: ToolResultMode.MultiRound,
            maxToolRoundsPerIteration: 5);
        var context = CreateContext();

        var result = await loop.RunAsync(options, context, TestContext.Current.CancellationToken);

        Assert.Equal(TerminationReason.Error, result.Termination);
        Assert.NotEmpty(result.Iterations);

        var partial = result.Iterations[0];
        Assert.Equal(5000, partial.Tokens.InputTokens);
        Assert.Equal(200, partial.Tokens.OutputTokens);
        Assert.Equal(5200, partial.Tokens.TotalTokens);
        Assert.Equal(3000, partial.Tokens.CachedInputTokens);
        Assert.Equal(50, partial.Tokens.ReasoningTokens);
        Assert.Equal(1, partial.LlmCallCount);
    }
}
