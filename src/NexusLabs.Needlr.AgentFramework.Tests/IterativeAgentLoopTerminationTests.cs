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
    public async Task RunAsync_TextResponse_TerminatesAfterOneIteration()
    {
        var mockChat = CreateMockChat("All done!");
        var loop = CreateLoop(mockChat);
        var options = CreateOptions();
        var context = CreateContext();

        var result = await loop.RunAsync(options, context, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("All done!", result.FinalResponse?.Text);
        Assert.Single(result.Iterations);
        Assert.Equal(0, result.Iterations[0].Iteration);
    }

    [Fact]
    public async Task RunAsync_TextResponse_IterationHasNoToolCalls()
    {
        var mockChat = CreateMockChat("Done");
        var loop = CreateLoop(mockChat);

        var result = await loop.RunAsync(CreateOptions(), CreateContext(), TestContext.Current.CancellationToken);

        Assert.Empty(result.Iterations[0].ToolCalls);
        Assert.Equal("Done", result.Iterations[0].FinalResponse?.Text);
    }

    [Fact]
    public async Task RunAsync_MaxIterations_StopsAfterLimit()
    {
        // Model always requests tools — loop must stop at MaxIterations
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateToolCallResponse(("noop", "c1", null)));

        var tool = CreateTool("noop", () => "ok");
        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            maxIterations: 3,
            toolResultMode: ToolResultMode.SingleCall);

        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Iterations.Count);
    }

    [Fact]
    public async Task RunAsync_IsComplete_StopsLoopEarly()
    {
        var workspace = new InMemoryWorkspace();
        var tool = CreateTool("mark_done", () =>
        {
            workspace.TryWriteFile("done.txt", "yes");
            return "marked";
        });

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateToolCallResponse(("mark_done", "c1", null)));

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            maxIterations: 100,
            isComplete: ctx => ctx.Workspace.FileExists("done.txt"),
            toolResultMode: ToolResultMode.SingleCall);

        var result = await loop.RunAsync(options, new IterativeContext { Workspace = workspace }, TestContext.Current.CancellationToken);

        // Should stop after the iteration where "done.txt" was written
        Assert.True(result.Iterations.Count < 100);
        Assert.True(workspace.FileExists("done.txt"));
    }

    [Fact]
    public async Task RunAsync_Cancellation_ExitsCleanly()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var mockChat = CreateMockChat("should not reach");
        var loop = CreateLoop(mockChat);

        var result = await loop.RunAsync(
            CreateOptions(), CreateContext(), cts.Token);

        Assert.False(result.Succeeded);
        Assert.Contains("cancelled", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_CancellationMidLoop_Stops()
    {
        var cts = new CancellationTokenSource();
        var tool = CreateTool("cancel_trigger", () =>
        {
            cts.Cancel();
            return "ok";
        });

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateToolCallResponse(("cancel_trigger", "c1", null)));

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            maxIterations: 100,
            toolResultMode: ToolResultMode.SingleCall);

        var result = await loop.RunAsync(options, CreateContext(), cts.Token);

        Assert.False(result.Succeeded);
        Assert.True(result.Iterations.Count < 100);
    }
}
