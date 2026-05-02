using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.AgentFramework.Budget;
using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.AgentFramework.Progress;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Tests for <see cref="ServiceCollectionAgentFrameworkExtensions.AddNeedlrAgentFramework"/>,
/// the <see cref="IServiceCollection"/>-based entry point for agent framework registration.
/// This enables <see cref="IServiceCollectionPlugin"/> implementations to self-register
/// the agent framework without requiring the composition root to call
/// <c>UsingAgentFramework</c> on the syringe builder.
/// </summary>
public sealed class ServiceCollectionAgentFrameworkExtensionsTests
{
    [Fact]
    public void AddNeedlrAgentFramework_RegistersIAgentFactory()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddNeedlrAgentFramework();

        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IAgentFactory>());
    }

    [Fact]
    public void AddNeedlrAgentFramework_RegistersIWorkflowFactory()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddNeedlrAgentFramework();

        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IWorkflowFactory>());
    }

    [Fact]
    public void AddNeedlrAgentFramework_RegistersInfrastructureServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddNeedlrAgentFramework();

        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<ITokenBudgetTracker>());
        Assert.NotNull(provider.GetService<IAgentExecutionContextAccessor>());
        Assert.NotNull(provider.GetService<IAgentDiagnosticsAccessor>());
        Assert.NotNull(provider.GetService<IToolMetricsAccessor>());
        Assert.NotNull(provider.GetService<IProgressReporterAccessor>());
        Assert.NotNull(provider.GetService<IProgressReporterFactory>());
        Assert.NotNull(provider.GetService<IIterativeAgentLoop>());
    }

    [Fact]
    public void AddNeedlrAgentFramework_SameCodePath_AsSyringeBuilder()
    {
        var syringeServices = new ServiceCollection();
        syringeServices.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var config = new ConfigurationBuilder().Build();
        var syringeProvider = new Syringe()
            .UsingReflection()
            .UsingAgentFramework()
            .BuildServiceProvider(config);

        var extensionServices = new ServiceCollection();
        extensionServices.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        extensionServices.AddNeedlrAgentFramework();
        var extensionProvider = extensionServices.BuildServiceProvider();

        var syringeTypes = new[]
        {
            typeof(IAgentFactory),
            typeof(IWorkflowFactory),
            typeof(ITokenBudgetTracker),
            typeof(IAgentExecutionContextAccessor),
            typeof(IAgentDiagnosticsAccessor),
            typeof(IToolMetricsAccessor),
            typeof(IProgressReporterAccessor),
            typeof(IIterativeAgentLoop),
        };

        foreach (var type in syringeTypes)
        {
            var fromSyringe = syringeProvider.GetService(type);
            var fromExtension = extensionProvider.GetService(type);
            Assert.NotNull(fromSyringe);
            Assert.NotNull(fromExtension);
        }
    }

    [Fact]
    public void AddNeedlrAgentFramework_CalledTwice_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddNeedlrAgentFramework();
        services.AddNeedlrAgentFramework();

        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IAgentFactory>());
    }

    [Fact]
    public void WithoutAgentFramework_InfrastructureTypesNotResolvable()
    {
        var config = new ConfigurationBuilder().Build();
        var provider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider(config);

        Assert.Null(provider.GetService<IAgentFactory>());
        Assert.Null(provider.GetService<IWorkflowFactory>());
        Assert.Null(provider.GetService<ITokenBudgetTracker>());
    }

    [Fact]
    public void UsingAgentFramework_And_AddNeedlrAgentFramework_BothCalled_AllTypesResolve()
    {
        // Simulates the real scenario: a plugin calls AddNeedlrAgentFramework() during
        // IServiceCollectionPlugin.Configure (step 2 in the pipeline), and the syringe
        // builder also calls UsingAgentFramework() (step 3 — post-plugin callback).
        // Both use TryAddSingleton, so calling both is idempotent. All types must resolve
        // and there must be no duplicate registrations.
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<Microsoft.Extensions.AI.IChatClient>();

        var provider = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object))
            .UsingPreRegistrationCallback(services =>
            {
                // Simulate a plugin registering AF before the post-plugin callback runs
                services.AddNeedlrAgentFramework();
            })
            .BuildServiceProvider(config);

        Assert.NotNull(provider.GetService<IAgentFactory>());
        Assert.NotNull(provider.GetService<IWorkflowFactory>());
        Assert.NotNull(provider.GetService<ITokenBudgetTracker>());
        Assert.NotNull(provider.GetService<IAgentDiagnosticsAccessor>());
        Assert.NotNull(provider.GetService<IToolMetricsAccessor>());

        // Verify no duplicate IAgentFactory registrations
        var factories = provider.GetServices<IAgentFactory>().ToList();
        Assert.Single(factories);
    }
}
