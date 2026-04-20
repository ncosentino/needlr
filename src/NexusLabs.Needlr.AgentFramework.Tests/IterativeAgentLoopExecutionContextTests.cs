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
    public async Task RunAsync_ToolsCanReadWorkspaceViaExecutionContextAccessor()
    {
        var contextAccessor = new AgentExecutionContextAccessor();
        IWorkspace? capturedWorkspace = null;

        var tool = CreateTool("read_workspace", () =>
        {
            capturedWorkspace = contextAccessor.Current?.GetRequiredWorkspace();
            return "ok";
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
                    return CreateToolCallResponse(("read_workspace", "c1", null));
                return new ChatResponse([new ChatMessage(ChatRole.Assistant, "done")]);
            });

        var loop = CreateLoop(mockChat, executionContextAccessor: contextAccessor);

        var ctx = CreateContext();
        ctx.Workspace.TryWriteFile("data.txt", "hello");

        var result = await loop.RunAsync(
            CreateOptions(tools: [tool]),
            ctx,
            TestContext.Current.CancellationToken);

        Assert.NotNull(capturedWorkspace);
        Assert.Equal("hello", capturedWorkspace!.TryReadFile("data.txt").Value.Content);
    }

    [Fact]
    public async Task RunAsync_ExecutionContextScopeRestoredAfterLoopCompletes()
    {
        var contextAccessor = new AgentExecutionContextAccessor();
        var mockChat = CreateMockChat("done");
        var loop = CreateLoop(mockChat, executionContextAccessor: contextAccessor);

        var outerContext = new AgentExecutionContext(UserId: "outer", OrchestrationId: "outer-scope");
        using var outerScope = contextAccessor.BeginScope(outerContext);

        var result = await loop.RunAsync(CreateOptions(), CreateContext(), TestContext.Current.CancellationToken);

        Assert.NotNull(contextAccessor.Current);
        Assert.Equal("outer", contextAccessor.Current!.UserId);
    }

    [Fact]
    public async Task RunAsync_NullExecutionContextAccessor_DoesNotThrow()
    {
        var mockChat = CreateMockChat("done");
        var loop = CreateLoop(mockChat, executionContextAccessor: null);

        var result = await loop.RunAsync(CreateOptions(), CreateContext(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task RunAsync_ExplicitExecutionContext_UsedOverAutoCreated()
    {
        var contextAccessor = new AgentExecutionContextAccessor();
        string? capturedUserId = null;

        var tool = CreateTool("check_context", () =>
        {
            capturedUserId = contextAccessor.Current?.UserId;
            return "ok";
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
                    return CreateToolCallResponse(("check_context", "c1", null));
                return new ChatResponse([new ChatMessage(ChatRole.Assistant, "done")]);
            });

        var loop = CreateLoop(mockChat, executionContextAccessor: contextAccessor);

        var ctx = CreateContext();
        var options = CreateOptions(tools: [tool]);
        options.ExecutionContext = new AgentExecutionContext(
            UserId: "explicit-user",
            OrchestrationId: "explicit-orch",
            Workspace: ctx.Workspace);

        var result = await loop.RunAsync(options, ctx, TestContext.Current.CancellationToken);

        Assert.Equal("explicit-user", capturedUserId);
    }
}
