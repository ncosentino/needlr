using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests;

internal static class ChatCompletionDeduplicationTestsHelpers
{
    internal static IterativeAgentLoop CreateLoop(IChatClient chatClient, IAgentMetrics? metrics = null)
    {
        var accessor = new Mock<IChatClientAccessor>();
        accessor.Setup(a => a.ChatClient).Returns(chatClient);
        return new IterativeAgentLoop(accessor.Object, metrics: metrics);
    }

    internal static IterativeContext CreateContext() =>
        new() { Workspace = new InMemoryWorkspace() };

    internal static IterativeLoopOptions CreateOptions(IReadOnlyList<AITool> tools) =>
        new()
        {
            Instructions = "Test assistant",
            PromptFactory = _ => "Do the thing",
            Tools = tools,
            MaxIterations = 5,
            IsComplete = _ => true,
            LoopName = "dedup-test",
        };

    internal static Mock<IChatClient> CreateToolCallThenDoneChat(
        string toolName, int inputTokens = 50, int outputTokens = 25)
    {
        var callCount = 0;
        var mock = new Mock<IChatClient>();
        mock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var n = Interlocked.Increment(ref callCount);
                if (n == 1)
                {
                    return CreateToolCallResponse(toolName, $"call-{n}", inputTokens, outputTokens);
                }

                return CreateTextResponse("done", inputTokens, outputTokens);
            });
        return mock;
    }

    internal static Mock<IChatClient> CreateTextResponseChat(string text)
    {
        var mock = new Mock<IChatClient>();
        mock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, text)])
            {
                ModelId = "test-model",
                Usage = new UsageDetails
                {
                    InputTokenCount = 10,
                    OutputTokenCount = 20,
                    TotalTokenCount = 30,
                },
            });
        return mock;
    }

    internal static ChatResponse CreateToolCallResponse(
        string toolName, string callId, int inputTokens, int outputTokens)
    {
        var response = new ChatResponse(
        [
            new ChatMessage(ChatRole.Assistant,
            [
                new FunctionCallContent(callId, toolName,
                    new Dictionary<string, object?> { ["arg"] = "val" })
            ])
        ])
        {
            ModelId = "test-model",
            Usage = new UsageDetails
            {
                InputTokenCount = inputTokens,
                OutputTokenCount = outputTokens,
                TotalTokenCount = inputTokens + outputTokens,
            },
        };
        return response;
    }

    internal static ChatResponse CreateTextResponse(string text, int inputTokens, int outputTokens)
    {
        return new ChatResponse([new ChatMessage(ChatRole.Assistant, text)])
        {
            ModelId = "test-model",
            Usage = new UsageDetails
            {
                InputTokenCount = inputTokens,
                OutputTokenCount = outputTokens,
                TotalTokenCount = inputTokens + outputTokens,
            },
        };
    }

    internal static AIFunction CreateTool(string name, Func<object?> execute)
    {
        return AIFunctionFactory.Create(
            () => execute(),
            new AIFunctionFactoryOptions { Name = name });
    }

    /// <summary>
    /// Simple DelegatingChatClient that passes through without modification.
    /// Simulates middleware like UseChatReducer being added on top of an existing chain.
    /// </summary>
    internal sealed class PassthroughClient(IChatClient inner) : DelegatingChatClient(inner);
}
