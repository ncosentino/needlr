using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public sealed class AgentFrameworkSyringeExtensionsTests
{
    [Fact]
    public void UsingAgentFramework_WithDefaultConfiguration_RegistersIAgentFactory()
    {
        var config = new ConfigurationBuilder().Build();

        var serviceProvider = new Syringe()
            .UsingReflection()
            .UsingAgentFramework()
            .BuildServiceProvider(config);

        var agentFactory = serviceProvider.GetService<IAgentFactory>();
        Assert.NotNull(agentFactory);
    }

    [Fact]
    public void UsingAgentFramework_WithConfigureCallback_CallsCallbackWhenAgentCreated()
    {
        var config = new ConfigurationBuilder().Build();
        var configCalled = false;
        var mockChatClient = new Mock<IChatClient>();

        var serviceProvider = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af.Configure(opts =>
            {
                configCalled = true;
                Assert.NotNull(opts.ServiceProvider);
                opts.ChatClientFactory = _ => mockChatClient.Object;
            }))
            .BuildServiceProvider(config);

        var agentFactory = serviceProvider.GetRequiredService<IAgentFactory>();
        agentFactory.CreateAgent();

        Assert.True(configCalled);
    }

    [Fact]
    public void UsingAgentFramework_RegisteredInServiceProvider_RegistersAsSingleton()
    {
        var config = new ConfigurationBuilder().Build();

        var serviceProvider = new Syringe()
            .UsingReflection()
            .UsingAgentFramework()
            .BuildServiceProvider(config);

        var factory1 = serviceProvider.GetRequiredService<IAgentFactory>();
        var factory2 = serviceProvider.GetRequiredService<IAgentFactory>();

        Assert.Same(factory1, factory2);
    }

    [Fact]
    public void CreateAgent_WithConfigureCallback_CallsCallback()
    {
        var config = new ConfigurationBuilder().Build();
        var agentOptionsCalled = false;
        var mockChatClient = new Mock<IChatClient>();

        var factory = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object))
            .BuildServiceProvider(config)
            .GetRequiredService<IAgentFactory>();

        factory.CreateAgent(opts =>
        {
            agentOptionsCalled = true;
        });

        Assert.True(agentOptionsCalled);
    }
}
