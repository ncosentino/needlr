using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workspace;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Tests;

internal static class IterativeLoopDiagnosticsIntegrationTestsHelpers
{
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
            LoopName = "integration-dedup-test",
        };

    internal static IServiceProvider BuildServiceProvider(
        Mock<IChatClient> mockChat,
        bool useDiagnostics)
    {
        var config = new ConfigurationBuilder().Build();

        return new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af =>
            {
                af = af.Configure(opts => opts.ChatClientFactory = _ => mockChat.Object);
                if (useDiagnostics)
                {
                    af = af.UsingDiagnostics();
                }

                return af;
            })
            .BuildServiceProvider(config);
    }

    internal static Mock<IChatClient> CreateMockChat(string text)
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
                Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 20, TotalTokenCount = 30 },
            });
        return mock;
    }

    internal static Mock<IChatClient> CreateToolCallThenDoneChat(
        string toolName, int inputTokens = 100, int outputTokens = 50)
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

    internal static ChatResponse CreateToolCallResponse(
        string toolName, string callId, int inputTokens, int outputTokens)
    {
        return new ChatResponse(
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
}
