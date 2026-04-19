using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Tests;

internal static class MafAgentDiagnosticsDeduplicationTestsHelpers
{
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

    internal static Mock<IChatClient> CreateMockChatWithTokens(
        string text, int inputTokens, int outputTokens)
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
                    InputTokenCount = inputTokens,
                    OutputTokenCount = outputTokens,
                    TotalTokenCount = inputTokens + outputTokens,
                },
            });
        return mock;
    }

    internal static IAsyncEnumerable<ChatResponseUpdate> CreateStreamingResponse(string text)
    {
        return CreateStreamingResponseAsync(text);

        static async IAsyncEnumerable<ChatResponseUpdate> CreateStreamingResponseAsync(string text)
        {
            yield return new ChatResponseUpdate
            {
                Role = ChatRole.Assistant,
                Contents = [new TextContent(text)],
                ModelId = "test-model",
            };
            await Task.CompletedTask;
        }
    }
}
