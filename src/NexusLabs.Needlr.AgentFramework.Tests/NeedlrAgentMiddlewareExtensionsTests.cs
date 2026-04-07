using System.Reflection;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.AgentFramework.Budget;
using NexusLabs.Needlr.AgentFramework.Workflows.Middleware;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Tests for <see cref="NeedlrAgentMiddlewareExtensions"/> — the syringe extension
/// methods <c>UsingToolResultMiddleware()</c>, <c>UsingResilience()</c>, and
/// <c>UsingTokenBudget()</c>.
/// </summary>
public class NeedlrAgentMiddlewareExtensionsTests
{
    // -------------------------------------------------------------------------
    // UsingToolResultMiddleware
    // -------------------------------------------------------------------------

    [Fact]
    public void UsingToolResultMiddleware_WithNullSyringe_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            NeedlrAgentMiddlewareExtensions.UsingToolResultMiddleware(null!));
    }

    [Fact]
    public void UsingToolResultMiddleware_ReturnsNewSyringeInstance()
    {
        var syringe = CreateSyringe();

        var result = syringe.UsingToolResultMiddleware();

        Assert.NotSame(syringe, result);
    }

    // -------------------------------------------------------------------------
    // UsingResilience (default)
    // -------------------------------------------------------------------------

    [Fact]
    public void UsingResilience_WithNullSyringe_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            NeedlrAgentMiddlewareExtensions.UsingResilience(null!));
    }

    [Fact]
    public void UsingResilience_ReturnsNewSyringeInstance()
    {
        var syringe = CreateSyringe();

        var result = syringe.UsingResilience();

        Assert.NotSame(syringe, result);
    }

    // -------------------------------------------------------------------------
    // UsingResilience (custom pipeline)
    // -------------------------------------------------------------------------

    [Fact]
    public void UsingResilience_CustomPipeline_WithNullSyringe_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            NeedlrAgentMiddlewareExtensions.UsingResilience(null!, _ => { }));
    }

    [Fact]
    public void UsingResilience_CustomPipeline_WithNullConfigure_ThrowsArgumentNull()
    {
        var syringe = CreateSyringe();

        Assert.Throws<ArgumentNullException>(() =>
            syringe.UsingResilience(null!));
    }

    // -------------------------------------------------------------------------
    // ITokenBudgetTracker registration
    // -------------------------------------------------------------------------

    [Fact]
    public void UsingAgentFramework_RegistersTokenBudgetTracker()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();

        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object))
            .BuildServiceProvider(config);

        var tracker = sp.GetService<ITokenBudgetTracker>();

        Assert.NotNull(tracker);
        Assert.IsType<TokenBudgetTracker>(tracker);
    }

    // -------------------------------------------------------------------------
    // Full integration — factory with all middleware resolves successfully
    // -------------------------------------------------------------------------

    [Fact]
    public void FullStack_UsingAllMiddleware_CreatesAgentSuccessfully()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        var assembly = Assembly.GetExecutingAssembly();

        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgentFunctionsFromAssemblies([assembly])
                .AddAgent<PluginTestAgent>()
                .UsingToolResultMiddleware()
                .UsingResilience())
            .BuildServiceProvider(config);

        var factory = sp.GetRequiredService<IAgentFactory>();
        var agent = factory.CreateAgent<PluginTestAgent>();

        Assert.NotNull(agent);
        Assert.IsAssignableFrom<Microsoft.Agents.AI.AIAgent>(agent);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static AgentFrameworkSyringe CreateSyringe()
    {
        var config = new ConfigurationBuilder().Build();
        var sp = new Syringe()
            .UsingReflection()
            .BuildServiceProvider(config);

        return new AgentFrameworkSyringe { ServiceProvider = sp };
    }
}
