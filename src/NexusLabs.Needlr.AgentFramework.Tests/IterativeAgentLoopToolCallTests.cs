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
    public async Task RunAsync_ToolCall_ExecutesToolAndContinues()
    {
        var toolExecuted = false;
        var tool = CreateTool("do_work", () =>
        {
            toolExecuted = true;
            return "work done";
        });

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
                if (callCount == 1)
                {
                    // First call: request tool
                    return CreateToolCallResponse(("do_work", "call-1", null));
                }

                // Second call (OneRoundTrip feedback): text response
                return new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, "Finished!")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(tools: [tool]);
        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.True(toolExecuted);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task RunAsync_MultipleToolCalls_AllExecuted()
    {
        var executed = new List<string>();
        var tool1 = CreateTool("tool_a", () => { executed.Add("a"); return "a-result"; });
        var tool2 = CreateTool("tool_b", () => { executed.Add("b"); return "b-result"; });

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
                if (callCount == 1)
                {
                    return CreateToolCallResponse(
                        ("tool_a", "call-a", null),
                        ("tool_b", "call-b", null));
                }

                return new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, "Done")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(tools: [tool1, tool2]);
        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(["a", "b"], executed);
        Assert.Equal(2, result.Iterations[0].ToolCalls.Count);
        Assert.All(result.Iterations[0].ToolCalls, tc => Assert.True(tc.Succeeded));
    }

    [Fact]
    public async Task RunAsync_UnknownTool_RecordedAsFailedToolCall()
    {
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
                if (callCount == 1)
                {
                    return CreateToolCallResponse(("nonexistent_tool", "c1", null));
                }

                return new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, "done")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(toolResultMode: ToolResultMode.SingleCall);
        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        var toolCall = Assert.Single(result.Iterations[0].ToolCalls);
        Assert.False(toolCall.Succeeded);
        Assert.Contains("Unknown tool", toolCall.ErrorMessage);
        Assert.Equal("nonexistent_tool", toolCall.FunctionName);
    }

    [Fact]
    public async Task RunAsync_ToolException_RecordedAndLoopContinues()
    {
        var tool = CreateTool("explode", () =>
            throw new InvalidOperationException("boom!"));

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
                if (callCount == 1)
                {
                    return CreateToolCallResponse(("explode", "c1", null));
                }

                return new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, "handled")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            toolResultMode: ToolResultMode.SingleCall);

        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        var toolCall = Assert.Single(result.Iterations[0].ToolCalls);
        Assert.False(toolCall.Succeeded);
        Assert.Equal("boom!", toolCall.ErrorMessage);
    }
}
