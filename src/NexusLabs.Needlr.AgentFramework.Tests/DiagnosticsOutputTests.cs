using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Tests for diagnostics output completeness: configuration echo,
/// execution mode, and per-iteration tool call count.
/// </summary>
public sealed class DiagnosticsOutputTests
{
    [Fact]
    public async Task IterativeLoopResult_CarriesResolvedConfiguration()
    {
        var mockChat = CreateTextResponseChat("done");
        var loop = CreateLoop(mockChat);

        var options = new IterativeLoopOptions
        {
            Instructions = "test",
            Tools = [],
            PromptFactory = _ => "go",
            MaxIterations = 15,
            ToolResultMode = ToolResultMode.MultiRound,
            MaxToolRoundsPerIteration = 8,
            MaxTotalToolCalls = 50,
            BudgetPressureThreshold = 0.75,
            LoopName = "research-stage",
        };

        var result = await loop.RunAsync(
            options,
            new IterativeContext { Workspace = new InMemoryWorkspace() },
            TestContext.Current.CancellationToken);

        Assert.NotNull(result.Configuration);
        Assert.Equal(ToolResultMode.MultiRound, result.Configuration.ToolResultMode);
        Assert.Equal(15, result.Configuration.MaxIterations);
        Assert.Equal(8, result.Configuration.MaxToolRoundsPerIteration);
        Assert.Equal(50, result.Configuration.MaxTotalToolCalls);
        Assert.Equal(0.75, result.Configuration.BudgetPressureThreshold);
        Assert.Equal("research-stage", result.Configuration.LoopName);
    }

    [Fact]
    public async Task IterativeLoopResult_Configuration_DefaultValues()
    {
        var mockChat = CreateTextResponseChat("done");
        var loop = CreateLoop(mockChat);

        var options = new IterativeLoopOptions
        {
            Instructions = "test",
            Tools = [],
            PromptFactory = _ => "go",
        };

        var result = await loop.RunAsync(
            options,
            new IterativeContext { Workspace = new InMemoryWorkspace() },
            TestContext.Current.CancellationToken);

        Assert.NotNull(result.Configuration);
        Assert.Equal(ToolResultMode.OneRoundTrip, result.Configuration.ToolResultMode);
        Assert.Equal(25, result.Configuration.MaxIterations);
        Assert.Equal(5, result.Configuration.MaxToolRoundsPerIteration);
        Assert.Null(result.Configuration.MaxTotalToolCalls);
        Assert.Null(result.Configuration.BudgetPressureThreshold);
        Assert.Equal("iterative-loop", result.Configuration.LoopName);
    }

    [Fact]
    public async Task Diagnostics_ExecutionMode_SetToIterativeLoop()
    {
        var mockChat = CreateTextResponseChat("done");
        var loop = CreateLoop(mockChat);

        var options = new IterativeLoopOptions
        {
            Instructions = "test",
            Tools = [],
            PromptFactory = _ => "go",
        };

        var result = await loop.RunAsync(
            options,
            new IterativeContext { Workspace = new InMemoryWorkspace() },
            TestContext.Current.CancellationToken);

        Assert.NotNull(result.Diagnostics);
        Assert.Equal("IterativeLoop", result.Diagnostics.ExecutionMode);
    }

    [Fact]
    public async Task Diagnostics_ExecutionMode_NullByDefault()
    {
        // Build diagnostics via the builder without setting execution mode
        var builder = AgentRunDiagnosticsBuilder.StartNew("test-agent");
        var diagnostics = builder.Build();
        builder.Dispose();

        Assert.Null(diagnostics.ExecutionMode);
    }

    [Fact]
    public async Task IterationRecord_HasToolCallCount()
    {
        var searchTool = AIFunctionFactory.Create(() => "result", "search");
        int callNum = 0;

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callNum++;
                if (callNum <= 2)
                {
                    return new ChatResponse(
                    [
                        new ChatMessage(ChatRole.Assistant,
                        [
                            new FunctionCallContent($"c{callNum}", "search",
                                new Dictionary<string, object?> { ["q"] = "test" }),
                        ]),
                    ]);
                }

                return new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, "done")]);
            });

        var loop = CreateLoop(mockChat);

        var options = new IterativeLoopOptions
        {
            Instructions = "search",
            Tools = [searchTool],
            PromptFactory = _ => "go",
            ToolResultMode = ToolResultMode.OneRoundTrip,
        };

        var result = await loop.RunAsync(
            options,
            new IterativeContext { Workspace = new InMemoryWorkspace() },
            TestContext.Current.CancellationToken);

        // First iteration has tool calls from both rounds in OneRoundTrip
        Assert.True(result.Iterations.Count >= 1);
        Assert.Equal(result.Iterations[0].ToolCalls.Count, result.Iterations[0].ToolCallCount);
        Assert.True(result.Iterations[0].ToolCallCount > 0);
    }

    #region Helpers

    private static IterativeAgentLoop CreateLoop(Mock<IChatClient> mockChat)
    {
        var accessor = new Mock<IChatClientAccessor>();
        accessor.Setup(a => a.ChatClient).Returns(mockChat.Object);
        return new IterativeAgentLoop(accessor.Object);
    }

    private static Mock<IChatClient> CreateTextResponseChat(string text)
    {
        var mock = new Mock<IChatClient>();
        mock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, text)]));
        return mock;
    }

    #endregion
}
