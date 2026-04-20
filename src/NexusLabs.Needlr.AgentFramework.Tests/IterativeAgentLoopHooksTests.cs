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
    public async Task RunAsync_OnIterationStart_FiresBeforePromptFactory()
    {
        var callOrder = new List<string>();

        var mockChat = CreateMockChat("done");
        var loop = CreateLoop(mockChat);
        var options = CreateOptions();
        options.PromptFactory = ctx =>
        {
            callOrder.Add("prompt");
            return "go";
        };
        options.OnIterationStart = (iter, ctx) =>
        {
            callOrder.Add($"start:{iter}");
            return Task.CompletedTask;
        };

        await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(["start:0", "prompt"], callOrder);
    }

    [Fact]
    public async Task RunAsync_OnToolCall_FiresForEachToolInOrderWithIteration()
    {
        var tool = CreateTool("ping", () => "pong");
        var hookCalls = new List<(int Iteration, string ToolName)>();

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
                return callCount == 1
                    ? CreateToolCallResponse(("ping", "c1", null))
                    : new ChatResponse([new ChatMessage(ChatRole.Assistant, "done")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            toolResultMode: ToolResultMode.OneRoundTrip);
        options.OnToolCall = (iter, result) =>
        {
            hookCalls.Add((iter, result.FunctionName));
            return Task.CompletedTask;
        };

        await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.Single(hookCalls);
        Assert.Equal(0, hookCalls[0].Iteration);
        Assert.Equal("ping", hookCalls[0].ToolName);
    }

    [Fact]
    public async Task RunAsync_OnIterationEnd_FiresWithCompleteRecord()
    {
        var endRecords = new List<IterationRecord>();

        var mockChat = CreateMockChat("done");
        var loop = CreateLoop(mockChat);
        var options = CreateOptions();
        options.OnIterationEnd = record =>
        {
            endRecords.Add(record);
            return Task.CompletedTask;
        };

        await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.Single(endRecords);
        Assert.Equal(0, endRecords[0].Iteration);
        Assert.Equal("done", endRecords[0].FinalResponse?.Text);
    }

    [Fact]
    public async Task RunAsync_NullHooks_DoNotThrow()
    {
        var mockChat = CreateMockChat("done");
        var loop = CreateLoop(mockChat);
        var options = CreateOptions();

        // All hooks are null by default — should not throw
        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task RunAsync_HookException_PropagatesToCaller()
    {
        var mockChat = CreateMockChat("done");
        var loop = CreateLoop(mockChat);
        var options = CreateOptions();
        options.OnIterationStart = (_, _) =>
            throw new InvalidOperationException("Hook blew up");

        // Hook exception should NOT be caught by the loop's internal error handling
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RunAsync_WriterPopulatedAfterRun()
    {
        var mockWriter = new Mock<IAgentDiagnosticsWriter>();
        IAgentRunDiagnostics? captured = null;
        mockWriter
            .Setup(w => w.Set(It.IsAny<IAgentRunDiagnostics>()))
            .Callback<IAgentRunDiagnostics>(d => captured = d);

        var mockChat = CreateMockChat("done");
        var loop = CreateLoop(mockChat, mockWriter.Object);

        var result = await loop.RunAsync(CreateOptions(), CreateContext(), TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal(result.Diagnostics!.AgentName, captured!.AgentName);
    }

    [Fact]
    public async Task RunAsync_NullWriter_DoesNotThrow()
    {
        var mockChat = CreateMockChat("done");
        var loop = CreateLoop(mockChat, diagnosticsWriter: null);

        var result = await loop.RunAsync(CreateOptions(), CreateContext(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task RunAsync_AccessorDiagnosticsMatchesResultDiagnostics()
    {
        var mockWriter = new Mock<IAgentDiagnosticsWriter>();
        IAgentRunDiagnostics? captured = null;
        mockWriter
            .Setup(w => w.Set(It.IsAny<IAgentRunDiagnostics>()))
            .Callback<IAgentRunDiagnostics>(d => captured = d);

        var mockChat = CreateMockChatWithTokens("done", 100, 50);
        var loop = CreateLoop(mockChat, mockWriter.Object);

        var result = await loop.RunAsync(CreateOptions(), CreateContext(), TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.NotNull(result.Diagnostics);

        // The diagnostics written to the accessor should be the same object
        Assert.Same(result.Diagnostics, captured);
    }

    [Fact]
    public async Task RunAsync_WriterCalledEvenOnFailure()
    {
        var mockWriter = new Mock<IAgentDiagnosticsWriter>();
        IAgentRunDiagnostics? captured = null;
        mockWriter
            .Setup(w => w.Set(It.IsAny<IAgentRunDiagnostics>()))
            .Callback<IAgentRunDiagnostics>(d => captured = d);

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM error"));

        var loop = CreateLoop(mockChat, mockWriter.Object);
        var result = await loop.RunAsync(CreateOptions(), CreateContext(), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.NotNull(captured);
        Assert.False(captured!.Succeeded);
    }
}
