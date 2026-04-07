using System.Reflection;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.AgentFramework.Workflows.Middleware;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

using Polly;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class AgentResiliencePluginTests
{
    [Fact]
    public void Constructor_WithNullPipeline_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AgentResiliencePlugin(null!));
    }

    [Fact]
    public void Implements_IAIAgentBuilderPlugin()
    {
        var pipeline = new ResiliencePipelineBuilder<AgentResponse>().Build();
        var plugin = new AgentResiliencePlugin(pipeline);

        Assert.IsAssignableFrom<IAIAgentBuilderPlugin>(plugin);
    }

    [Fact]
    public void Configure_WithNullOptions_ThrowsArgumentNull()
    {
        var pipeline = new ResiliencePipelineBuilder<AgentResponse>().Build();
        var plugin = new AgentResiliencePlugin(pipeline);

        Assert.Throws<ArgumentNullException>(() =>
            plugin.Configure(null!));
    }

    [Fact]
    public void Configure_WithValidBuilder_DoesNotThrow()
    {
        var pipeline = new ResiliencePipelineBuilder<AgentResponse>().Build();
        var plugin = new AgentResiliencePlugin(pipeline);

        var chatClient = new Mock<IChatClient>();
        var baseAgent = chatClient.Object.AsAIAgent(name: "test", instructions: "test");
        var builder = new AIAgentBuilder(baseAgent);
        var options = new AIAgentBuilderPluginOptions { AgentBuilder = builder };

        plugin.Configure(options);

        var agent = builder.Build(null);
        Assert.NotNull(agent);
    }

    // -------------------------------------------------------------------------
    // M4: Behavioral test — resilience retries on transient failure
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Agent_WithResilience_RetriesOnHttpRequestException()
    {
        var callCount = 0;
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((_, _, _) =>
            {
                callCount++;
                if (callCount == 1)
                    throw new HttpRequestException("transient failure");

                return Task.FromResult(
                    new ChatResponse([new ChatMessage(ChatRole.Assistant, "recovered")]));
            });

        var factory = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChat.Object)
                .UsingResilience())
            .BuildServiceProvider(new ConfigurationBuilder().Build())
            .GetRequiredService<IAgentFactory>();

        var agent = factory.CreateAgent(opts => opts.Name = "RetryAgent");

        var response = await agent.RunAsync(
            "hello",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        // First call threw, second succeeded — retry worked
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task Agent_WithResilience_SucceedsOnFirstTry_NoRetry()
    {
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "ok")]));

        var factory = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChat.Object)
                .UsingResilience())
            .BuildServiceProvider(new ConfigurationBuilder().Build())
            .GetRequiredService<IAgentFactory>();

        var agent = factory.CreateAgent(opts => opts.Name = "NoRetryAgent");

        var response = await agent.RunAsync(
            "hello",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        mockChat.Verify(
            c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
