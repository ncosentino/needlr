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
/// Tests for the <see cref="ServiceCollectionAgentFrameworkExtensions"/>
/// <c>AddNeedlrAgentFramework</c> overloads, the <see cref="IServiceCollection"/>-based
/// entry point for agent framework registration. Enables
/// <see cref="IServiceCollectionPlugin"/> implementations to self-register the
/// agent framework (and own its full configuration surface) without requiring the
/// composition root to call <c>UsingAgentFramework</c> on the syringe builder.
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
        // Simulates when a plugin calls AddNeedlrAgentFramework() during
        // IServiceCollectionPlugin.Configure, and the syringe
        // builder also calls UsingAgentFramework().
        // Calling SHOULD be idempotent. All types must resolve
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

        // Verify no duplicate registrations
        Assert.Single(provider.GetServices<IAgentFactory>());
        Assert.Single(provider.GetServices<IWorkflowFactory>());
        Assert.Single(provider.GetServices<ITokenBudgetTracker>());
        Assert.Single(provider.GetServices<IAgentDiagnosticsAccessor>());
        Assert.Single(provider.GetServices<IToolMetricsAccessor>());
    }

    [Fact]
    public void AddNeedlrAgentFramework_WithConfigure_InvokesDelegate()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        var configureInvoked = false;
        services.AddNeedlrAgentFramework(af =>
        {
            configureInvoked = true;
            return af;
        });

        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IAgentFactory>());
        Assert.True(configureInvoked, "configure delegate should run when the agent factory is resolved");
    }

    [Fact]
    public void AddNeedlrAgentFramework_WithConfigure_ChatClientFactoryPropagates()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        var mockChatClient = new Mock<Microsoft.Extensions.AI.IChatClient>();
        services.AddNeedlrAgentFramework(af => af
            .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object));

        var provider = services.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IChatClientAccessor>();
        Assert.Same(mockChatClient.Object, accessor.ChatClient);
    }

    [Fact]
    public void AddNeedlrAgentFramework_WithConfigure_MetricsOptionsPropagatesToIAgentMetrics()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddNeedlrAgentFramework(af => af
            .ConfigureMetrics(o => o.MeterName = "Test.Configure.Meter"));

        var provider = services.BuildServiceProvider();
        var metrics = provider.GetRequiredService<IAgentMetrics>();
        Assert.Equal("Test.Configure.Meter", metrics.ActivitySource.Name);
    }

    [Fact]
    public void AddNeedlrAgentFramework_WithConfigure_NullServices_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ServiceCollectionAgentFrameworkExtensions.AddNeedlrAgentFramework(
                services: null!,
                configure: af => af));
    }

    [Fact]
    public void AddNeedlrAgentFramework_WithConfigure_NullConfigure_Throws()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() =>
            services.AddNeedlrAgentFramework(
                configure: (Func<AgentFrameworkSyringe, AgentFrameworkSyringe>)null!));
    }

    [Fact]
    public void AddNeedlrAgentFramework_WithConfigure_RegistersAllInfrastructure()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddNeedlrAgentFramework(af => af);

        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IAgentFactory>());
        Assert.NotNull(provider.GetService<IWorkflowFactory>());
        Assert.NotNull(provider.GetService<ITokenBudgetTracker>());
        Assert.NotNull(provider.GetService<IAgentExecutionContextAccessor>());
        Assert.NotNull(provider.GetService<IAgentDiagnosticsAccessor>());
        Assert.NotNull(provider.GetService<IToolMetricsAccessor>());
        Assert.NotNull(provider.GetService<IProgressReporterAccessor>());
        Assert.NotNull(provider.GetService<IProgressReporterFactory>());
        Assert.NotNull(provider.GetService<IIterativeAgentLoop>());
    }

    [Fact]
    public void AddNeedlrAgentFramework_WithFactoryDelegate_InvokesFactory()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        var factoryInvoked = false;
        services.AddNeedlrAgentFramework(() =>
        {
            factoryInvoked = true;
            return new AgentFrameworkSyringe
            {
                ServiceProvider = new ServiceCollection().BuildServiceProvider(),
            };
        });

        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IAgentFactory>());
        Assert.True(factoryInvoked, "factory delegate should run when the agent factory is resolved");
    }

    [Fact]
    public void AddNeedlrAgentFramework_WithFactoryDelegate_NullConfigure_Throws()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() =>
            services.AddNeedlrAgentFramework(
                configure: (Func<AgentFrameworkSyringe>)null!));
    }

    /// <summary>
    /// The factory overload (Func&lt;AgentFrameworkSyringe&gt;) is documented as routing
    /// through the main configure overload (Func&lt;AgentFrameworkSyringe, AgentFrameworkSyringe&gt;).
    /// This test asserts that both paths produce equivalent service registrations when given
    /// equivalent configurations.
    /// </summary>
    [Fact]
    public void AddNeedlrAgentFramework_WithFactoryDelegate_RoutesThroughMainConfigureOverload()
    {
        var mainOverloadServices = new ServiceCollection();
        mainOverloadServices.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        mainOverloadServices.AddNeedlrAgentFramework(af => af
            .ConfigureMetrics(o => o.MeterName = "Routing.Test"));
        var mainOverloadProvider = mainOverloadServices.BuildServiceProvider();

        var factoryOverloadServices = new ServiceCollection();
        factoryOverloadServices.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        factoryOverloadServices.AddNeedlrAgentFramework(() =>
            new AgentFrameworkSyringe
            {
                ServiceProvider = new ServiceCollection().BuildServiceProvider(),
            }.ConfigureMetrics(o => o.MeterName = "Routing.Test"));
        var factoryOverloadProvider = factoryOverloadServices.BuildServiceProvider();

        var mainOverloadMetrics = mainOverloadProvider.GetRequiredService<IAgentMetrics>();
        var factoryOverloadMetrics = factoryOverloadProvider.GetRequiredService<IAgentMetrics>();

        Assert.Equal(mainOverloadMetrics.ActivitySource.Name, factoryOverloadMetrics.ActivitySource.Name);
        Assert.Equal("Routing.Test", factoryOverloadMetrics.ActivitySource.Name);
    }

    /// <summary>
    /// Documents silent first-wins semantics. When a plugin's
    /// <c>AddNeedlrAgentFramework(configure)</c> registers via
    /// <c>UsingPreRegistrationCallback</c> (runs before plugins / before the post-plugin
    /// callback that <c>UsingAgentFramework</c> uses), the plugin's configure delegate
    /// wins because <c>TryAddSingleton&lt;BuiltAgentFrameworkSyringe&gt;</c> sees the plugin's
    /// registration first. The composition root's configure delegate is silently discarded.
    /// </summary>
    [Fact]
    public void Plugin_AddWithConfigure_Then_Syringe_UsingWithConfigure_PluginConfigureWins()
    {
        var config = new ConfigurationBuilder().Build();

        var provider = new Syringe()
            .UsingReflection()
            .UsingPreRegistrationCallback(services =>
            {
                services.AddNeedlrAgentFramework(af => af
                    .ConfigureMetrics(o => o.MeterName = "Plugin.Wins"));
            })
            .UsingAgentFramework(af => af
                .ConfigureMetrics(o => o.MeterName = "Root.Loses"))
            .BuildServiceProvider(config);

        var metrics = provider.GetRequiredService<IAgentMetrics>();
        Assert.Equal("Plugin.Wins", metrics.ActivitySource.Name);
    }

    /// <summary>
    /// Documents silent first-wins semantics in the inverse order. When the syringe
    /// builder's <c>UsingAgentFramework(configure)</c> registers via
    /// <c>UsingPostPluginRegistrationCallback</c> and a plugin's
    /// <c>AddNeedlrAgentFramework(configure)</c> registers via
    /// <c>UsingPostPluginRegistrationCallback</c> AFTER the syringe builder's
    /// callback was already enqueued, the syringe builder's configure delegate wins.
    /// The plugin's configure delegate is silently discarded.
    /// </summary>
    [Fact]
    public void Syringe_UsingWithConfigure_Then_Plugin_AddWithConfigure_RootConfigureWins()
    {
        var config = new ConfigurationBuilder().Build();

        var provider = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .ConfigureMetrics(o => o.MeterName = "Root.Wins"))
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.AddNeedlrAgentFramework(af => af
                    .ConfigureMetrics(o => o.MeterName = "Plugin.Loses"));
            })
            .BuildServiceProvider(config);

        var metrics = provider.GetRequiredService<IAgentMetrics>();
        Assert.Equal("Root.Wins", metrics.ActivitySource.Name);
    }

    /// <summary>
    /// When a single plugin invokes <c>AddNeedlrAgentFramework(configure)</c> twice with
    /// different configure delegates, only the first registration's configure runs and
    /// only one set of services is registered. Documents that
    /// <c>TryAddSingleton&lt;BuiltAgentFrameworkSyringe&gt;</c> means the second call is
    /// a complete no-op for the configure delegate as well as for the registration itself.
    /// </summary>
    [Fact]
    public void Plugin_AddWithConfigure_BothCalls_OnlyOneRegistration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        var firstInvoked = false;
        var secondInvoked = false;

        services.AddNeedlrAgentFramework(af =>
        {
            firstInvoked = true;
            return af.ConfigureMetrics(o => o.MeterName = "First.Call");
        });
        services.AddNeedlrAgentFramework(af =>
        {
            secondInvoked = true;
            return af.ConfigureMetrics(o => o.MeterName = "Second.Call");
        });

        var provider = services.BuildServiceProvider();
        var metrics = provider.GetRequiredService<IAgentMetrics>();

        Assert.Single(provider.GetServices<IAgentFactory>());
        Assert.Single(provider.GetServices<IWorkflowFactory>());
        Assert.Equal("First.Call", metrics.ActivitySource.Name);
        Assert.True(firstInvoked, "first configure delegate should run");
        Assert.False(secondInvoked, "second configure delegate should be silently discarded");
    }

    [Fact]
    public void AddNeedlrAgentFramework_WithConfigure_SameCodePath_AsSyringeBuilder()
    {
        var config = new ConfigurationBuilder().Build();

        var syringeProvider = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .ConfigureMetrics(o => o.MeterName = "Parity.Test"))
            .BuildServiceProvider(config);

        var extensionServices = new ServiceCollection();
        extensionServices.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        extensionServices.AddNeedlrAgentFramework(af => af
            .ConfigureMetrics(o => o.MeterName = "Parity.Test"));
        var extensionProvider = extensionServices.BuildServiceProvider();

        var syringeMetrics = syringeProvider.GetRequiredService<IAgentMetrics>();
        var extensionMetrics = extensionProvider.GetRequiredService<IAgentMetrics>();

        Assert.Equal(syringeMetrics.ActivitySource.Name, extensionMetrics.ActivitySource.Name);
        Assert.Equal("Parity.Test", extensionMetrics.ActivitySource.Name);

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
            Assert.NotNull(syringeProvider.GetService(type));
            Assert.NotNull(extensionProvider.GetService(type));
        }
    }

    [Fact]
    public void AddNeedlrAgentFramework_RegistersIPipelineMetrics_AsNoOpByDefault()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddNeedlrAgentFramework();

        var provider = services.BuildServiceProvider();
        var metrics = provider.GetService<IPipelineMetrics>();
        Assert.NotNull(metrics);
        Assert.Equal("NexusLabs.Needlr.AgentFramework.Pipelines.NoOp", metrics!.ActivitySource.Name);
    }

    [Fact]
    public void AddNeedlrAgentFramework_WithConfigurePipelineMetrics_ResolvesRealPipelineMetrics()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddNeedlrAgentFramework(af => af
            .ConfigurePipelineMetrics(o => o.MeterName = "Test.Pipelines"));

        var provider = services.BuildServiceProvider();
        var metrics = provider.GetService<IPipelineMetrics>();
        Assert.NotNull(metrics);
        Assert.Equal("Test.Pipelines", metrics!.ActivitySource.Name);
    }

    [Fact]
    public void AddNeedlrAgentFramework_PipelineMetricsAndAgentMetrics_ResolveIndependently()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddNeedlrAgentFramework(af => af
            .ConfigureMetrics(o => o.MeterName = "Test.Agents")
            .ConfigurePipelineMetrics(o => o.MeterName = "Test.Pipelines"));

        var provider = services.BuildServiceProvider();
        var agentMetrics = provider.GetRequiredService<IAgentMetrics>();
        var pipelineMetrics = provider.GetRequiredService<IPipelineMetrics>();

        Assert.Equal("Test.Agents", agentMetrics.ActivitySource.Name);
        Assert.Equal("Test.Pipelines", pipelineMetrics.ActivitySource.Name);
    }
}
