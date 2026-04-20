using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public sealed partial class IterativeAgentLoopTests
{
    private static IterativeAgentLoop CreateLoop(
        Mock<IChatClient> mockChat,
        IAgentDiagnosticsWriter? diagnosticsWriter = null,
        IAgentExecutionContextAccessor? executionContextAccessor = null)
    {
        var accessor = new Mock<IChatClientAccessor>();
        accessor.Setup(a => a.ChatClient).Returns(mockChat.Object);
        return new IterativeAgentLoop(accessor.Object, diagnosticsWriter, executionContextAccessor);
    }

    private static Mock<IChatClient> CreateMockChat(string responseText)
    {
        var mock = new Mock<IChatClient>();
        mock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, responseText)]));
        return mock;
    }

    private static Mock<IChatClient> CreateMockChatWithTokens(
        string responseText, int inputTokens, int outputTokens)
    {
        var mock = new Mock<IChatClient>();
        mock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var response = new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, responseText)]);
                response.Usage = new UsageDetails
                {
                    InputTokenCount = inputTokens,
                    OutputTokenCount = outputTokens,
                    TotalTokenCount = inputTokens + outputTokens,
                };
                return response;
            });
        return mock;
    }

    private static ChatResponse CreateToolCallResponse(
        params (string name, string callId, IDictionary<string, object?>? args)[] calls)
    {
        var contents = new List<AIContent>();
        foreach (var (name, callId, args) in calls)
        {
            contents.Add(new FunctionCallContent(callId, name, args));
        }

        return new ChatResponse([new ChatMessage(ChatRole.Assistant, contents)]);
    }

    private static AIFunction CreateTool(string name, Func<object?> execute)
    {
        return AIFunctionFactory.Create(
            () => execute(),
            new AIFunctionFactoryOptions
            {
                Name = name,
            });
    }

    private static AIFunction CreateToolWithArgs(
        string name,
        Func<string, object?> execute)
    {
        return AIFunctionFactory.Create(
            (string input) => execute(input),
            new AIFunctionFactoryOptions
            {
                Name = name,
            });
    }

    private static IterativeLoopOptions CreateOptions(
        Func<IterativeContext, string>? promptFactory = null,
        IReadOnlyList<AITool>? tools = null,
        int maxIterations = 10,
        Func<IterativeContext, bool>? isComplete = null,
        ToolResultMode toolResultMode = ToolResultMode.OneRoundTrip,
        int maxToolRoundsPerIteration = 5,
        string? instructions = null)
    {
        return new IterativeLoopOptions
        {
            Instructions = instructions ?? "You are a test assistant.",
            PromptFactory = promptFactory ?? (_ => "Do the thing."),
            Tools = tools ?? Array.Empty<AITool>(),
            MaxIterations = maxIterations,
            IsComplete = isComplete,
            ToolResultMode = toolResultMode,
            MaxToolRoundsPerIteration = maxToolRoundsPerIteration,
            LoopName = "test-loop",
        };
    }

    private static IterativeContext CreateContext(IWorkspace? workspace = null)
    {
        return new IterativeContext { Workspace = workspace ?? new InMemoryWorkspace() };
    }

    private static void AssertDiagnosticsInvariants(IAgentRunDiagnostics diagnostics)
    {
        var chatSequences = diagnostics.ChatCompletions.Select(c => c.Sequence).ToList();
        Assert.Equal(
            chatSequences.Distinct().Count(),
            chatSequences.Count);

        var toolSequences = diagnostics.ToolCalls.Select(t => t.Sequence).ToList();
        Assert.Equal(
            toolSequences.Distinct().Count(),
            toolSequences.Count);

        long chatTotalTokens = diagnostics.ChatCompletions.Sum(c => c.Tokens.TotalTokens);
        Assert.Equal(chatTotalTokens, diagnostics.AggregateTokenUsage.TotalTokens);

        foreach (var chat in diagnostics.ChatCompletions.Where(c => c.Succeeded))
        {
            Assert.NotNull(chat.RequestMessages);
            Assert.NotEmpty(chat.RequestMessages);
            Assert.True(chat.RequestCharCount > 0,
                $"Successful chat completion seq={chat.Sequence} has RequestCharCount=0");
            Assert.NotNull(chat.Response);
        }

        foreach (var tool in diagnostics.ToolCalls.Where(t => t.Succeeded && t.Arguments is { Count: > 0 }))
        {
            Assert.True(tool.ArgumentsCharCount > 0,
                $"Successful tool call seq={tool.Sequence} has ArgumentsCharCount=0 but Arguments has entries");
        }
    }
}
