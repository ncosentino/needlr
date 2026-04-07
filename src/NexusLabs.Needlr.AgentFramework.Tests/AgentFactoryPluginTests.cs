using System.ComponentModel;
using System.Reflection;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.AgentFramework.Workflows.Middleware;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Tests for plugin wiring in <see cref="AgentFactory"/>, covering global plugins,
/// per-agent <see cref="AgentResilienceAttribute"/> overrides, and syringe extension methods.
/// </summary>
public class AgentFactoryPluginTests
{
    private static IAgentFactory CreateFactory(
        Func<AgentFrameworkSyringe, Assembly, AgentFrameworkSyringe>? configure = null)
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        var assembly = Assembly.GetExecutingAssembly();

        return new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af =>
            {
                af = af.Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object);
                if (configure != null)
                    af = configure(af, assembly);
                return af;
            })
            .BuildServiceProvider(config)
            .GetRequiredService<IAgentFactory>();
    }

    // -------------------------------------------------------------------------
    // Global plugins — no plugins = raw agent returned
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateAgent_WithNoPlugins_ReturnsAgent()
    {
        var factory = CreateFactory((af, asm) => af
            .AddAgentFunctionsFromAssemblies([asm])
            .AddAgent<PluginTestAgent>());

        var agent = factory.CreateAgent<PluginTestAgent>();

        Assert.NotNull(agent);
        Assert.IsAssignableFrom<AIAgent>(agent);
    }

    // -------------------------------------------------------------------------
    // Global plugins — UsingToolResultMiddleware
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateAgent_WithToolResultMiddleware_ReturnsAgent()
    {
        var factory = CreateFactory((af, asm) => af
            .AddAgentFunctionsFromAssemblies([asm])
            .AddAgent<PluginTestAgent>()
            .UsingToolResultMiddleware());

        var agent = factory.CreateAgent<PluginTestAgent>();

        Assert.NotNull(agent);
        Assert.IsAssignableFrom<AIAgent>(agent);
    }

    // -------------------------------------------------------------------------
    // Global plugins — UsingResilience
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateAgent_WithResilience_ReturnsAgent()
    {
        var factory = CreateFactory((af, asm) => af
            .AddAgentFunctionsFromAssemblies([asm])
            .AddAgent<PluginTestAgent>()
            .UsingResilience());

        var agent = factory.CreateAgent<PluginTestAgent>();

        Assert.NotNull(agent);
        Assert.IsAssignableFrom<AIAgent>(agent);
    }

    // -------------------------------------------------------------------------
    // Combined plugins
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateAgent_WithMultiplePlugins_ReturnsAgent()
    {
        var factory = CreateFactory((af, asm) => af
            .AddAgentFunctionsFromAssemblies([asm])
            .AddAgent<PluginTestAgent>()
            .UsingToolResultMiddleware()
            .UsingResilience());

        var agent = factory.CreateAgent<PluginTestAgent>();

        Assert.NotNull(agent);
        Assert.IsAssignableFrom<AIAgent>(agent);
    }

    // -------------------------------------------------------------------------
    // Per-agent [AgentResilience] attribute override
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateAgent_WithResilienceAttribute_ReturnsAgent()
    {
        var factory = CreateFactory((af, asm) => af
            .AddAgentFunctionsFromAssemblies([asm])
            .AddAgent<ResilientPluginTestAgent>()
            .UsingResilience());

        var agent = factory.CreateAgent<ResilientPluginTestAgent>();

        Assert.NotNull(agent);
        Assert.IsAssignableFrom<AIAgent>(agent);
    }

    [Fact]
    public void CreateAgent_WithResilienceAttribute_WithoutUsingResilience_IgnoresAttribute()
    {
        // [AgentResilience] without UsingResilience() on syringe — no PerAgentResilienceFactory
        // should not throw, just ignore the attribute
        var factory = CreateFactory((af, asm) => af
            .AddAgentFunctionsFromAssemblies([asm])
            .AddAgent<ResilientPluginTestAgent>());

        var agent = factory.CreateAgent<ResilientPluginTestAgent>();

        Assert.NotNull(agent);
        Assert.IsAssignableFrom<AIAgent>(agent);
    }

    // -------------------------------------------------------------------------
    // Custom plugin via syringe
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateAgent_WithCustomPlugin_PluginConfigureIsCalled()
    {
        var pluginMock = new Mock<IAIAgentBuilderPlugin>();

        var factory = CreateFactory((af, asm) =>
        {
            af = af
                .AddAgentFunctionsFromAssemblies([asm])
                .AddAgent<PluginTestAgent>();

            // Manually wire a custom plugin via the Plugins property
            return af with
            {
                Plugins = [pluginMock.Object]
            };
        });

        factory.CreateAgent<PluginTestAgent>();

        pluginMock.Verify(
            p => p.Configure(It.IsAny<AIAgentBuilderPluginOptions>()),
            Times.Once);
    }

    [Fact]
    public void CreateAgent_WithMultipleCustomPlugins_AllConfigureCalled()
    {
        var plugin1 = new Mock<IAIAgentBuilderPlugin>();
        var plugin2 = new Mock<IAIAgentBuilderPlugin>();

        var factory = CreateFactory((af, asm) =>
        {
            af = af
                .AddAgentFunctionsFromAssemblies([asm])
                .AddAgent<PluginTestAgent>();

            return af with
            {
                Plugins = [plugin1.Object, plugin2.Object]
            };
        });

        factory.CreateAgent<PluginTestAgent>();

        plugin1.Verify(p => p.Configure(It.IsAny<AIAgentBuilderPluginOptions>()), Times.Once);
        plugin2.Verify(p => p.Configure(It.IsAny<AIAgentBuilderPluginOptions>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Plugin receives valid AIAgentBuilder
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateAgent_PluginReceives_NonNullBuilder()
    {
        AIAgentBuilderPluginOptions? capturedOptions = null;
        var pluginMock = new Mock<IAIAgentBuilderPlugin>();
        pluginMock
            .Setup(p => p.Configure(It.IsAny<AIAgentBuilderPluginOptions>()))
            .Callback<AIAgentBuilderPluginOptions>(opts => capturedOptions = opts);

        var factory = CreateFactory((af, asm) =>
        {
            af = af
                .AddAgentFunctionsFromAssemblies([asm])
                .AddAgent<PluginTestAgent>();

            return af with
            {
                Plugins = [pluginMock.Object]
            };
        });

        factory.CreateAgent<PluginTestAgent>();

        Assert.NotNull(capturedOptions);
        Assert.NotNull(capturedOptions!.AgentBuilder);
    }

    // -------------------------------------------------------------------------
    // CreateAgent(configure) — manual agent without attribute — plugins applied
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateAgent_ManualConfigure_WithPlugins_PluginIsCalled()
    {
        var pluginMock = new Mock<IAIAgentBuilderPlugin>();

        var factory = CreateFactory((af, asm) =>
        {
            af = af.AddAgentFunctionsFromAssemblies([asm]);
            return af with
            {
                Plugins = [pluginMock.Object]
            };
        });

        factory.CreateAgent(opts =>
        {
            opts.Name = "ManualAgent";
            opts.Instructions = "test";
        });

        pluginMock.Verify(
            p => p.Configure(It.IsAny<AIAgentBuilderPluginOptions>()),
            Times.Once);
    }
}

// ---------------------------------------------------------------------------
// Test agents for plugin tests — at namespace level
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Plugin test agent.")]
public sealed class PluginTestAgent { }

[NeedlrAiAgent(Instructions = "Resilient plugin test agent.")]
[AgentResilience(maxRetries: 3, timeoutSeconds: 60)]
public sealed class ResilientPluginTestAgent { }
