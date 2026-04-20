using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public sealed partial class IterativeAgentLoopTests
{
    [Fact]
    public async Task RunAsync_SingleCallMode_ExactlyOneLlmCallPerIteration()
    {
        var tool = CreateTool("noop", () => "ok");

        var mockChat = new Mock<IChatClient>();
        var callCount = 0;
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount <= 2)
                {
                    return CreateToolCallResponse(("noop", $"c{callCount}", null));
                }

                return new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, "done")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            toolResultMode: ToolResultMode.SingleCall);

        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        // Each iteration should have exactly 1 LLM call
        foreach (var iter in result.Iterations.Where(i => i.ToolCalls.Count > 0))
        {
            Assert.Equal(1, iter.LlmCallCount);
        }
    }

    [Fact]
    public async Task RunAsync_OneRoundTripMode_SendsResultsBackOnce()
    {
        var tool = CreateTool("ping", () => "pong");
        List<IEnumerable<ChatMessage>>? capturedMessages = [];

        var mockChat = new Mock<IChatClient>();
        var callCount = 0;
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ChatMessage> messages, ChatOptions? _, CancellationToken _) =>
            {
                capturedMessages.Add(messages);
                callCount++;
                if (callCount == 1)
                {
                    // First call: request tool
                    return CreateToolCallResponse(("ping", "c1", null));
                }

                // Second call (round-trip): should include tool result messages
                return new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, "got it")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            toolResultMode: ToolResultMode.OneRoundTrip);

        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        // Should have been called twice in one iteration
        Assert.Equal(2, callCount);

        // Second call should have more messages (system + user + assistant tool call + tool result)
        var secondCallMessages = capturedMessages[1].ToList();
        Assert.True(secondCallMessages.Count > 2,
            $"Expected >2 messages in round-trip call, got {secondCallMessages.Count}");
    }

    [Fact]
    public async Task RunAsync_OneRoundTripMode_MaxTwoLlmCallsPerIteration()
    {
        var tool = CreateTool("chain", () => "chained");

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                // Always return tool calls — but OneRoundTrip should stop at 2
                return CreateToolCallResponse(("chain", "c1", null));
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            maxIterations: 1,
            toolResultMode: ToolResultMode.OneRoundTrip);

        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        var iter = Assert.Single(result.Iterations);
        Assert.Equal(2, iter.LlmCallCount);
    }

    [Fact]
    public async Task RunAsync_MultiRoundMode_ChainsToolCallsUpToMax()
    {
        var tool = CreateTool("step", () => "stepped");

        var mockChat = new Mock<IChatClient>();
        var callCount = 0;
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                // Always request tools — MultiRound should stop at max
                return CreateToolCallResponse(("step", $"c{callCount}", null));
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            maxIterations: 1,
            toolResultMode: ToolResultMode.MultiRound,
            maxToolRoundsPerIteration: 3);

        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        var iter = Assert.Single(result.Iterations);
        Assert.Equal(3, iter.LlmCallCount);
        Assert.Equal(3, iter.ToolCalls.Count);
    }

    [Fact]
    public async Task RunAsync_MultiRoundMode_StopsOnTextResponse()
    {
        var tool = CreateTool("step", () => "ok");

        var mockChat = new Mock<IChatClient>();
        var callCount = 0;
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount <= 2)
                {
                    return CreateToolCallResponse(("step", $"c{callCount}", null));
                }

                return new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, "done chaining")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            maxIterations: 1,
            toolResultMode: ToolResultMode.MultiRound,
            maxToolRoundsPerIteration: 10);

        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        var iter = Assert.Single(result.Iterations);
        Assert.Equal(3, iter.LlmCallCount);
        Assert.Equal("done chaining", iter.FinalResponse?.Text);
    }
}
