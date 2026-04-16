using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Tests for stall detection and per-loop ChatClientFactory.
/// </summary>
public sealed class StallDetectionAndLoopFactoryTests
{
    #region Stall Detection

    [Fact]
    public async Task StallDetection_FiresAfterThreshold()
    {
        // Mock returns tool calls with identical token usage every time
        var searchTool = AIFunctionFactory.Create(() => "result", "search");
        var mockChat = CreateConstantTokenChat("search", inputTokens: 1000, outputTokens: 200);

        var loop = CreateLoop(mockChat);

        var options = new IterativeLoopOptions
        {
            Instructions = "search",
            Tools = [searchTool],
            PromptFactory = _ => "go",
            MaxIterations = 20,
            ToolResultMode = ToolResultMode.OneRoundTrip,
            StallDetection = new StallDetectionOptions
            {
                ConsecutiveThreshold = 3,
                TolerancePercent = 0.10,
            },
        };

        var result = await loop.RunAsync(
            options,
            new IterativeContext { Workspace = new InMemoryWorkspace() },
            TestContext.Current.CancellationToken);

        Assert.Equal(TerminationReason.StallDetected, result.Termination);
        Assert.False(result.Succeeded);
        Assert.Contains("Stall detected", result.ErrorMessage);
        Assert.Equal(3, result.Iterations.Count);
    }

    [Fact]
    public async Task StallDetection_NoFalsePositiveOnProgress()
    {
        // Mock returns increasing token counts — should NOT trigger stall
        int callNum = 0;
        var searchTool = AIFunctionFactory.Create(() => "result", "search");

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callNum++;
                // Increasing tokens each call — 30%+ growth prevents stall detection
                var inputTokens = 1000 + (callNum * 500);
                return new ChatResponse(
                [
                    new ChatMessage(ChatRole.Assistant,
                    [
                        new FunctionCallContent($"c{callNum}", "search",
                            new Dictionary<string, object?> { ["q"] = "test" }),
                    ]),
                ])
                {
                    Usage = new UsageDetails
                    {
                        InputTokenCount = inputTokens,
                        OutputTokenCount = 200,
                        TotalTokenCount = inputTokens + 200,
                    },
                };
            });

        var loop = CreateLoop(mockChat);

        var options = new IterativeLoopOptions
        {
            Instructions = "search",
            Tools = [searchTool],
            PromptFactory = _ => "go",
            MaxIterations = 5,
            ToolResultMode = ToolResultMode.OneRoundTrip,
            StallDetection = new StallDetectionOptions
            {
                ConsecutiveThreshold = 3,
                TolerancePercent = 0.10,
            },
        };

        var result = await loop.RunAsync(
            options,
            new IterativeContext { Workspace = new InMemoryWorkspace() },
            TestContext.Current.CancellationToken);

        // Should NOT be StallDetected — tokens are growing
        Assert.NotEqual(TerminationReason.StallDetected, result.Termination);
        Assert.Equal(TerminationReason.MaxIterationsReached, result.Termination);
    }

    [Fact]
    public async Task StallDetection_ToleranceBoundary_DoesNotTrigger()
    {
        // Token counts differ by >10% between iterations — should NOT trigger
        int callNum = 0;
        var searchTool = AIFunctionFactory.Create(() => "result", "search");

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callNum++;
                // Each iteration gets 2 calls (OneRoundTrip). Make odd iterations
                // have ~1200 total and even iterations have ~1600 total (33% diff).
                var iterIdx = (callNum - 1) / 2;
                var total = iterIdx % 2 == 0 ? 600 : 800;
                return new ChatResponse(
                [
                    new ChatMessage(ChatRole.Assistant,
                    [
                        new FunctionCallContent($"c{callNum}", "search",
                            new Dictionary<string, object?> { ["q"] = "test" }),
                    ]),
                ])
                {
                    Usage = new UsageDetails
                    {
                        InputTokenCount = total - 100,
                        OutputTokenCount = 100,
                        TotalTokenCount = total,
                    },
                };
            });

        var loop = CreateLoop(mockChat);

        var options = new IterativeLoopOptions
        {
            Instructions = "search",
            Tools = [searchTool],
            PromptFactory = _ => "go",
            MaxIterations = 6,
            ToolResultMode = ToolResultMode.OneRoundTrip,
            StallDetection = new StallDetectionOptions
            {
                ConsecutiveThreshold = 3,
                TolerancePercent = 0.10,
            },
        };

        var result = await loop.RunAsync(
            options,
            new IterativeContext { Workspace = new InMemoryWorkspace() },
            TestContext.Current.CancellationToken);

        Assert.NotEqual(TerminationReason.StallDetected, result.Termination);
    }

    [Fact]
    public async Task StallDetection_NullDisabled_NeverTriggers()
    {
        var searchTool = AIFunctionFactory.Create(() => "result", "search");
        var mockChat = CreateConstantTokenChat("search", inputTokens: 1000, outputTokens: 200);

        var loop = CreateLoop(mockChat);

        var options = new IterativeLoopOptions
        {
            Instructions = "search",
            Tools = [searchTool],
            PromptFactory = _ => "go",
            MaxIterations = 5,
            ToolResultMode = ToolResultMode.OneRoundTrip,
            StallDetection = null, // explicitly disabled
        };

        var result = await loop.RunAsync(
            options,
            new IterativeContext { Workspace = new InMemoryWorkspace() },
            TestContext.Current.CancellationToken);

        // Should hit MaxIterations, not StallDetected
        Assert.Equal(TerminationReason.MaxIterationsReached, result.Termination);
        Assert.Equal(5, result.Iterations.Count);
    }

    [Fact]
    public async Task StallDetection_OnIterationEndStillFires()
    {
        var searchTool = AIFunctionFactory.Create(() => "result", "search");
        var mockChat = CreateConstantTokenChat("search", inputTokens: 1000, outputTokens: 200);

        var loop = CreateLoop(mockChat);
        var endCallCount = 0;

        var options = new IterativeLoopOptions
        {
            Instructions = "search",
            Tools = [searchTool],
            PromptFactory = _ => "go",
            MaxIterations = 20,
            ToolResultMode = ToolResultMode.OneRoundTrip,
            StallDetection = new StallDetectionOptions
            {
                ConsecutiveThreshold = 3,
                TolerancePercent = 0.10,
            },
            OnIterationEnd = _ =>
            {
                endCallCount++;
                return Task.CompletedTask;
            },
        };

        var result = await loop.RunAsync(
            options,
            new IterativeContext { Workspace = new InMemoryWorkspace() },
            TestContext.Current.CancellationToken);

        Assert.Equal(TerminationReason.StallDetected, result.Termination);
        // OnIterationEnd should have fired for all 3 iterations including the stall trigger
        Assert.Equal(3, endCallCount);
    }

    #endregion

    #region ChatClientFactory on IterativeLoopOptions

    [Fact]
    public async Task ChatClientFactory_WrapsInnerClient()
    {
        var wrapperCalled = false;
        var innerMock = new Mock<IChatClient>();
        innerMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "done")]));

        var accessor = new Mock<IChatClientAccessor>();
        accessor.Setup(a => a.ChatClient).Returns(innerMock.Object);
        var loop = new IterativeAgentLoop(accessor.Object);

        var options = new IterativeLoopOptions
        {
            Instructions = "test",
            Tools = [],
            PromptFactory = _ => "go",
            ChatClientFactory = inner =>
            {
                wrapperCalled = true;
                Assert.Same(innerMock.Object, inner);
                return inner; // pass through
            },
        };

        await loop.RunAsync(
            options,
            new IterativeContext { Workspace = new InMemoryWorkspace() },
            TestContext.Current.CancellationToken);

        Assert.True(wrapperCalled, "ChatClientFactory should be invoked");
    }

    [Fact]
    public async Task ChatClientFactory_Null_UsesGlobalClient()
    {
        var innerMock = new Mock<IChatClient>();
        innerMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "done")]));

        var accessor = new Mock<IChatClientAccessor>();
        accessor.Setup(a => a.ChatClient).Returns(innerMock.Object);
        var loop = new IterativeAgentLoop(accessor.Object);

        var options = new IterativeLoopOptions
        {
            Instructions = "test",
            Tools = [],
            PromptFactory = _ => "go",
            // ChatClientFactory is null by default
        };

        await loop.RunAsync(
            options,
            new IterativeContext { Workspace = new InMemoryWorkspace() },
            TestContext.Current.CancellationToken);

        // Verify the global client was used (it received the call)
        innerMock.Verify(c => c.GetResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    #endregion

    #region Helpers

    private static IterativeAgentLoop CreateLoop(Mock<IChatClient> mockChat)
    {
        var accessor = new Mock<IChatClientAccessor>();
        accessor.Setup(a => a.ChatClient).Returns(mockChat.Object);
        return new IterativeAgentLoop(accessor.Object);
    }

    private static Mock<IChatClient> CreateConstantTokenChat(
        string toolName, int inputTokens, int outputTokens)
    {
        int callNum = 0;
        var mock = new Mock<IChatClient>();
        mock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callNum++;
                return new ChatResponse(
                [
                    new ChatMessage(ChatRole.Assistant,
                    [
                        new FunctionCallContent($"c{callNum}", toolName,
                            new Dictionary<string, object?> { ["q"] = "test" }),
                    ]),
                ])
                {
                    Usage = new UsageDetails
                    {
                        InputTokenCount = inputTokens,
                        OutputTokenCount = outputTokens,
                        TotalTokenCount = inputTokens + outputTokens,
                    },
                };
            });
        return mock;
    }

    #endregion
}
