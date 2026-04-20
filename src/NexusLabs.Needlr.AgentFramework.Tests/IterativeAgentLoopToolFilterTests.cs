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
    public async Task RunAsync_ToolFilter_RestrictsToolsModelSees()
    {
        var toolA = CreateTool("allowed", () => "ok");
        var toolB = CreateTool("blocked", () => "should not run");

        ChatOptions? capturedOptions = null;
        var mockChat = new Mock<IChatClient>();
        var callCount = 0;
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ChatMessage> _, ChatOptions? opts, CancellationToken _) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    capturedOptions = opts;
                    return CreateToolCallResponse(("allowed", "call-1", null));
                }
                return new ChatResponse([new ChatMessage(ChatRole.Assistant, "done")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(tools: [toolA, toolB]);
        options.ToolFilter = (_, _, tools) => tools.Where(t => t.Name == "allowed").ToList();

        await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.NotNull(capturedOptions);
        Assert.Single(capturedOptions!.Tools!);
        Assert.Equal("allowed", capturedOptions.Tools![0].Name);
    }

    [Fact]
    public async Task RunAsync_NullToolFilter_PassesAllTools()
    {
        var toolA = CreateTool("tool_a", () => "a");
        var toolB = CreateTool("tool_b", () => "b");

        ChatOptions? capturedOptions = null;
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ChatMessage> _, ChatOptions? opts, CancellationToken _) =>
            {
                capturedOptions = opts;
                return new ChatResponse([new ChatMessage(ChatRole.Assistant, "done")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(tools: [toolA, toolB]);
        // ToolFilter is null by default

        await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.NotNull(capturedOptions);
        Assert.Equal(2, capturedOptions!.Tools!.Count);
    }

    [Fact]
    public async Task RunAsync_ToolFilter_ReceivesCorrectIterationAndContext()
    {
        var capturedIterations = new List<int>();
        var capturedFileContents = new List<string>();

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
                return new ChatResponse([new ChatMessage(ChatRole.Assistant, "done")]);
            });

        var workspace = new InMemoryWorkspace();
        workspace.SeedFile("phase.txt", "research");

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(maxIterations: 1);
        options.ToolFilter = (iteration, ctx, tools) =>
        {
            capturedIterations.Add(iteration);
            capturedFileContents.Add(ctx.Workspace.TryReadFile("phase.txt").Value.Content);
            return tools;
        };

        await loop.RunAsync(options, CreateContext(workspace), TestContext.Current.CancellationToken);

        Assert.Contains(0, capturedIterations);
        Assert.Contains("research", capturedFileContents);
    }

    [Fact]
    public async Task RunAsync_ToolFilter_ChangesPerIteration()
    {
        var tool1 = CreateTool("search", () => "results");
        var tool2 = CreateTool("build", () => "built");

        var capturedToolNames = new List<List<string>>();
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ChatMessage> _, ChatOptions? opts, CancellationToken _) =>
            {
                capturedToolNames.Add(opts?.Tools?.Select(t => t.Name).ToList() ?? []);
                // Always request a tool call so the loop keeps iterating
                var availableToolName = opts?.Tools?.FirstOrDefault()?.Name ?? "search";
                return CreateToolCallResponse((availableToolName, $"call-{capturedToolNames.Count}", null));
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool1, tool2],
            maxIterations: 2,
            toolResultMode: ToolResultMode.SingleCall);
        options.ToolFilter = (iteration, _, tools) =>
            iteration == 0
                ? tools.Where(t => t.Name == "search").ToList()
                : tools.Where(t => t.Name == "build").ToList();

        await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(2, capturedToolNames.Count);
        Assert.Equal(["search"], capturedToolNames[0]);
        Assert.Equal(["build"], capturedToolNames[1]);
    }
}
