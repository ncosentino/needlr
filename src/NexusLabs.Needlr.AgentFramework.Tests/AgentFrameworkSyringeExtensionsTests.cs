using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.AgentFramework.Budget;
using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Progress;
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

    [Fact]
    public void CreateAgent_WithName_AgentNameMatchesGivenName()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();

        var factory = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object))
            .BuildServiceProvider(config)
            .GetRequiredService<IAgentFactory>();

        var agent = factory.CreateAgent(opts =>
        {
            opts.Name = "ResearchAgent";
            opts.FunctionTypes = [];
        });

        Assert.Equal("ResearchAgent", agent.Name);
    }

    [Fact]
    public void CreateAgent_WithoutName_AgentIdIsPlainGuid()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();

        var factory = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object))
            .BuildServiceProvider(config)
            .GetRequiredService<IAgentFactory>();

        var agent = factory.CreateAgent(opts => opts.FunctionTypes = []);

        Assert.True(Guid.TryParse(agent.Id, out _));
    }

    // -------------------------------------------------------------------------
    // C1: All overloads register the same infrastructure singletons
    // -------------------------------------------------------------------------

    [Fact]
    public void UsingAgentFramework_CallbackOverload_RegistersAllInfrastructureSingletons()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();

        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object))
            .BuildServiceProvider(config);

        Assert.NotNull(sp.GetService<ITokenBudgetTracker>());
        Assert.NotNull(sp.GetService<IAgentExecutionContextAccessor>());
        Assert.NotNull(sp.GetService<IAgentDiagnosticsAccessor>());
        Assert.NotNull(sp.GetService<IToolMetricsAccessor>());
        Assert.NotNull(sp.GetService<IAgentMetrics>());
        Assert.NotNull(sp.GetService<IChatCompletionCollector>());
        Assert.NotNull(sp.GetService<IProgressSequence>());
        Assert.NotNull(sp.GetService<IProgressReporterFactory>());
        Assert.NotNull(sp.GetService<IProgressReporterAccessor>());
    }

    [Fact]
    public void UsingAgentFramework_FactoryOverload_RegistersAllInfrastructureSingletons()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();

        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(() => new AgentFrameworkSyringe
            {
                ServiceProvider = new Syringe()
                    .UsingReflection()
                    .BuildServiceProvider(config),
            })
            .BuildServiceProvider(config);

        Assert.NotNull(sp.GetService<ITokenBudgetTracker>());
        Assert.NotNull(sp.GetService<IAgentExecutionContextAccessor>());
        Assert.NotNull(sp.GetService<IAgentDiagnosticsAccessor>());
        Assert.NotNull(sp.GetService<IToolMetricsAccessor>());
        Assert.NotNull(sp.GetService<IAgentMetrics>());
        Assert.NotNull(sp.GetService<IChatCompletionCollector>());
        Assert.NotNull(sp.GetService<IProgressSequence>());
        Assert.NotNull(sp.GetService<IProgressReporterFactory>());
        Assert.NotNull(sp.GetService<IProgressReporterAccessor>());
    }

    [Fact]
    public void UsingAgentFramework_NoArgOverload_RegistersAllInfrastructureSingletons()
    {
        var config = new ConfigurationBuilder().Build();

        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework()
            .BuildServiceProvider(config);

        Assert.NotNull(sp.GetService<ITokenBudgetTracker>());
        Assert.NotNull(sp.GetService<IAgentExecutionContextAccessor>());
        Assert.NotNull(sp.GetService<IAgentDiagnosticsAccessor>());
        Assert.NotNull(sp.GetService<IToolMetricsAccessor>());
        Assert.NotNull(sp.GetService<IAgentMetrics>());
        Assert.NotNull(sp.GetService<IChatCompletionCollector>());
        Assert.NotNull(sp.GetService<IProgressSequence>());
        Assert.NotNull(sp.GetService<IProgressReporterFactory>());
        Assert.NotNull(sp.GetService<IProgressReporterAccessor>());
    }
}

