using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public sealed partial class IterativeAgentLoopTests
{
    [Fact]
    public async Task RunAsync_GenuineCancellation_ReturnsCancelledTermination()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        cts.Cancel();

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var loop = CreateLoop(mockChat);
        var options = CreateOptions();
        var context = CreateContext();

        var result = await loop.RunAsync(options, context, cts.Token);

        Assert.False(result.Succeeded, "Should not succeed on genuine cancellation");
        Assert.Equal(TerminationReason.Cancelled, result.Termination);
        Assert.Contains("cancelled", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_HttpTimeout_ReturnsErrorTermination()
    {
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException(
                "The operation was canceled.",
                new TimeoutException("The operation timed out.")));

        var loop = CreateLoop(mockChat);
        var options = CreateOptions();
        var context = CreateContext();

        var result = await loop.RunAsync(options, context, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded, "Should not succeed on timeout");
        Assert.Equal(TerminationReason.Error, result.Termination);
        Assert.Contains("timed out", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_NonTimeoutOperationCancelledException_ReturnsErrorTermination()
    {
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException("Some internal cancellation"));

        var loop = CreateLoop(mockChat);
        var options = CreateOptions();
        var context = CreateContext();

        var result = await loop.RunAsync(options, context, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded, "Should not succeed");
        Assert.Equal(TerminationReason.Error, result.Termination);
        Assert.DoesNotContain("Loop was cancelled", result.ErrorMessage);
    }

    [Fact]
    public async Task RunAsync_HttpTimeout_ErrorMessageIncludesIterationCount()
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
                if (callCount >= 2)
                {
                    throw new TaskCanceledException(
                        "Timeout",
                        new TimeoutException("The operation timed out."));
                }

                return CreateToolCallResponse(("noop", "c1", null));
            });

        var tool = CreateTool("noop", () => "ok");
        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            maxIterations: 5,
            toolResultMode: ToolResultMode.SingleCall);
        var context = CreateContext();

        var result = await loop.RunAsync(options, context, TestContext.Current.CancellationToken);

        Assert.Equal(TerminationReason.Error, result.Termination);
        Assert.Contains("iteration", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_HttpTimeout_CompletedIterationsPreserved()
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
                    return new ChatResponse([new ChatMessage(ChatRole.Assistant, "First done")]);
                }

                throw new TaskCanceledException(
                    "Timeout",
                    new TimeoutException("The operation timed out."));
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(maxIterations: 5);
        var context = CreateContext();

        var result = await loop.RunAsync(options, context, TestContext.Current.CancellationToken);

        Assert.Equal(TerminationReason.NaturalCompletion, result.Termination);
        Assert.True(result.Succeeded, "First iteration completes with text → natural completion");
    }

    [Fact]
    public async Task RunAsync_HttpTimeoutMidIteration_PartialIterationRecorded()
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
                    return CreateToolCallResponse(("noop", "c1", null));
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
        Assert.True(
            result.Iterations.Count >= 1,
            $"Expected at least 1 iteration record (partial), got {result.Iterations.Count}");
        Assert.True(
            result.Iterations[0].ToolCallCount >= 1,
            $"Expected at least 1 tool call in partial iteration, got {result.Iterations[0].ToolCallCount}");
    }

    [Fact]
    public async Task RunAsync_GenuineCancellation_PartialIterationRecorded()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);

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
                    return CreateToolCallResponse(("noop", "c1", null));
                }

                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            });

        var tool = CreateTool("noop", () => "ok");
        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            maxIterations: 5,
            toolResultMode: ToolResultMode.MultiRound,
            maxToolRoundsPerIteration: 5);
        var context = CreateContext();

        var result = await loop.RunAsync(options, context, cts.Token);

        Assert.Equal(TerminationReason.Cancelled, result.Termination);
        Assert.True(
            result.Iterations.Count >= 1,
            $"Expected partial iteration with tool calls, got {result.Iterations.Count}");
    }
}
