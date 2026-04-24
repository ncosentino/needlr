using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.AgentFramework.Workflows;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public sealed class GraphWorkflowServiceExtensionsTests
{
    private readonly MockRepository _mockRepository = new(MockBehavior.Strict);

    [Fact]
    public void AddGraphWorkflowRunner_RegistersIGraphWorkflowRunner()
    {
        var services = new ServiceCollection();
        RegisterPrerequisites(services);

        services.AddGraphWorkflowRunner();

        using var sp = services.BuildServiceProvider();
        var runner = sp.GetService<IGraphWorkflowRunner>();
        Assert.NotNull(runner);
    }

    [Fact]
    public void AddGraphWorkflowRunner_RegistersGraphTopologyProvider()
    {
        var services = new ServiceCollection();
        RegisterPrerequisites(services);

        services.AddGraphWorkflowRunner();

        using var sp = services.BuildServiceProvider();
        var provider = sp.GetService<GraphTopologyProvider>();
        Assert.NotNull(provider);
    }

    [Fact]
    public void AddGraphWorkflowRunner_RegistersGraphEdgeRouter()
    {
        var services = new ServiceCollection();
        RegisterPrerequisites(services);

        services.AddGraphWorkflowRunner();

        using var sp = services.BuildServiceProvider();
        var router = sp.GetService<GraphEdgeRouter>();
        Assert.NotNull(router);
    }

    [Fact]
    public void AddGraphWorkflowRunner_CalledTwice_DoesNotThrow()
    {
        var services = new ServiceCollection();
        RegisterPrerequisites(services);

        services.AddGraphWorkflowRunner();
        services.AddGraphWorkflowRunner();

        using var sp = services.BuildServiceProvider();
        var runner = sp.GetService<IGraphWorkflowRunner>();
        Assert.NotNull(runner);
    }

    [Fact]
    public void AddGraphWorkflowRunner_CalledTwice_ResolvesSameInstance()
    {
        var services = new ServiceCollection();
        RegisterPrerequisites(services);

        services.AddGraphWorkflowRunner();
        services.AddGraphWorkflowRunner();

        using var sp = services.BuildServiceProvider();
        var runner1 = sp.GetRequiredService<IGraphWorkflowRunner>();
        var runner2 = sp.GetRequiredService<IGraphWorkflowRunner>();
        Assert.Same(runner1, runner2);
    }

    [Fact]
    public void AddGraphWorkflowRunner_CalledTwice_GraphTopologyProviderIsSingleton()
    {
        var services = new ServiceCollection();
        RegisterPrerequisites(services);

        services.AddGraphWorkflowRunner();
        services.AddGraphWorkflowRunner();

        using var sp = services.BuildServiceProvider();
        var p1 = sp.GetRequiredService<GraphTopologyProvider>();
        var p2 = sp.GetRequiredService<GraphTopologyProvider>();
        Assert.Same(p1, p2);
    }

    [Fact]
    public void AddGraphWorkflowRunner_CalledTwice_GraphEdgeRouterIsSingleton()
    {
        var services = new ServiceCollection();
        RegisterPrerequisites(services);

        services.AddGraphWorkflowRunner();
        services.AddGraphWorkflowRunner();

        using var sp = services.BuildServiceProvider();
        var r1 = sp.GetRequiredService<GraphEdgeRouter>();
        var r2 = sp.GetRequiredService<GraphEdgeRouter>();
        Assert.Same(r1, r2);
    }

    [Fact]
    public void AddGraphWorkflowRunner_WithoutIWorkflowFactory_ThrowsOnResolve()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_mockRepository.Create<IAgentFactory>().Object);
        services.AddSingleton(_mockRepository.Create<IChatClientAccessor>().Object);

        services.AddGraphWorkflowRunner();

        using var sp = services.BuildServiceProvider();
        Assert.Throws<InvalidOperationException>(
            () => sp.GetRequiredService<IGraphWorkflowRunner>());
    }

    [Fact]
    public void AddGraphWorkflowRunner_WithoutIAgentFactory_ThrowsOnResolve()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_mockRepository.Create<IWorkflowFactory>().Object);
        services.AddSingleton(_mockRepository.Create<IChatClientAccessor>().Object);

        services.AddGraphWorkflowRunner();

        using var sp = services.BuildServiceProvider();
        Assert.Throws<InvalidOperationException>(
            () => sp.GetRequiredService<IGraphWorkflowRunner>());
    }

    [Fact]
    public void AddGraphWorkflowRunner_WithoutIChatClientAccessor_ThrowsOnResolve()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_mockRepository.Create<IWorkflowFactory>().Object);
        services.AddSingleton(_mockRepository.Create<IAgentFactory>().Object);

        services.AddGraphWorkflowRunner();

        using var sp = services.BuildServiceProvider();
        Assert.Throws<InvalidOperationException>(
            () => sp.GetRequiredService<IGraphWorkflowRunner>());
    }

    [Fact]
    public void AddGraphWorkflowRunner_WithoutIAgentDiagnosticsAccessor_ResolvesSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_mockRepository.Create<IWorkflowFactory>().Object);
        services.AddSingleton(_mockRepository.Create<IAgentFactory>().Object);
        services.AddSingleton(_mockRepository.Create<IChatClientAccessor>().Object);

        services.AddGraphWorkflowRunner();

        using var sp = services.BuildServiceProvider();
        var runner = sp.GetRequiredService<IGraphWorkflowRunner>();
        Assert.NotNull(runner);
    }

    [Fact]
    public void AddGraphWorkflowRunner_ReturnsServiceCollectionForChaining()
    {
        var services = new ServiceCollection();

        var returned = services.AddGraphWorkflowRunner();

        Assert.Same(services, returned);
    }

    [Fact]
    public void UsingGraphWorkflows_WithSyringe_RegistersIGraphWorkflowRunner()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = _mockRepository.Create<IChatClient>();

        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object))
            .UsingGraphWorkflows()
            .BuildServiceProvider(config);

        var runner = sp.GetService<IGraphWorkflowRunner>();
        Assert.NotNull(runner);
    }

    [Fact]
    public void UsingGraphWorkflows_WithSyringe_RegistersGraphTopologyProvider()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = _mockRepository.Create<IChatClient>();

        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object))
            .UsingGraphWorkflows()
            .BuildServiceProvider(config);

        var provider = sp.GetService<GraphTopologyProvider>();
        Assert.NotNull(provider);
    }

    [Fact]
    public void UsingGraphWorkflows_WithSyringe_RegistersGraphEdgeRouter()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = _mockRepository.Create<IChatClient>();

        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object))
            .UsingGraphWorkflows()
            .BuildServiceProvider(config);

        var router = sp.GetService<GraphEdgeRouter>();
        Assert.NotNull(router);
    }

    [Fact]
    public void UsingGraphWorkflows_CalledTwice_DoesNotThrow()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = _mockRepository.Create<IChatClient>();

        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object))
            .UsingGraphWorkflows()
            .UsingGraphWorkflows()
            .BuildServiceProvider(config);

        var runner = sp.GetService<IGraphWorkflowRunner>();
        Assert.NotNull(runner);
    }

    private void RegisterPrerequisites(IServiceCollection services)
    {
        services.AddSingleton(_mockRepository.Create<IWorkflowFactory>().Object);
        services.AddSingleton(_mockRepository.Create<IAgentFactory>().Object);
        services.AddSingleton(_mockRepository.Create<IChatClientAccessor>().Object);
    }
}
