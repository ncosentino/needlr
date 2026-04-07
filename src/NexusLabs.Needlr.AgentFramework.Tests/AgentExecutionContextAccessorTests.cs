using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class AgentExecutionContextAccessorTests
{
    // -------------------------------------------------------------------------
    // Current — no scope
    // -------------------------------------------------------------------------

    [Fact]
    public void Current_WithoutScope_ReturnsNull()
    {
        var accessor = new AgentExecutionContextAccessor();

        Assert.Null(accessor.Current);
    }

    // -------------------------------------------------------------------------
    // BeginScope — sets Current
    // -------------------------------------------------------------------------

    [Fact]
    public void BeginScope_SetsCurrent()
    {
        var accessor = new AgentExecutionContextAccessor();
        var context = new AgentExecutionContext("user-1", "orch-1");

        using var scope = accessor.BeginScope(context);

        Assert.Same(context, accessor.Current);
    }

    [Fact]
    public void BeginScope_WithNullContext_ThrowsArgumentNull()
    {
        var accessor = new AgentExecutionContextAccessor();

        Assert.Throws<ArgumentNullException>(() =>
            accessor.BeginScope(null!));
    }

    // -------------------------------------------------------------------------
    // Dispose — restores previous context
    // -------------------------------------------------------------------------

    [Fact]
    public void Dispose_RestoresPreviousContext()
    {
        var accessor = new AgentExecutionContextAccessor();
        var outer = new AgentExecutionContext("user-outer", "orch-outer");
        var inner = new AgentExecutionContext("user-inner", "orch-inner");

        using (accessor.BeginScope(outer))
        {
            using (accessor.BeginScope(inner))
            {
                Assert.Same(inner, accessor.Current);
            }

            Assert.Same(outer, accessor.Current);
        }

        Assert.Null(accessor.Current);
    }

    [Fact]
    public void Dispose_CalledTwice_IsIdempotent()
    {
        var accessor = new AgentExecutionContextAccessor();
        var outer = new AgentExecutionContext("user-1", "orch-1");
        var inner = new AgentExecutionContext("user-2", "orch-2");

        using (accessor.BeginScope(outer))
        {
            var scope = accessor.BeginScope(inner);
            scope.Dispose();
            scope.Dispose(); // second dispose should be no-op

            Assert.Same(outer, accessor.Current);
        }
    }

    // -------------------------------------------------------------------------
    // AsyncLocal isolation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ConcurrentScopes_AreIsolated()
    {
        var accessor = new AgentExecutionContextAccessor();
        var ct = TestContext.Current.CancellationToken;

        string? userInTask1 = null;
        string? userInTask2 = null;

        var task1 = Task.Run(() =>
        {
            var ctx = new AgentExecutionContext("alice", "orch-a");
            using var scope = accessor.BeginScope(ctx);
            Thread.Sleep(10); // let task2 start
            userInTask1 = accessor.Current?.UserId;
        }, ct);

        var task2 = Task.Run(() =>
        {
            var ctx = new AgentExecutionContext("bob", "orch-b");
            using var scope = accessor.BeginScope(ctx);
            Thread.Sleep(10);
            userInTask2 = accessor.Current?.UserId;
        }, ct);

        await Task.WhenAll(task1, task2);

        Assert.Equal("alice", userInTask1);
        Assert.Equal("bob", userInTask2);
        Assert.Null(accessor.Current); // main thread has no scope
    }

    // -------------------------------------------------------------------------
    // GetRequired extension
    // -------------------------------------------------------------------------

    [Fact]
    public void GetRequired_WithScope_ReturnsContext()
    {
        var accessor = new AgentExecutionContextAccessor();
        var context = new AgentExecutionContext("user-1", "orch-1");

        using var scope = accessor.BeginScope(context);

        Assert.Same(context, accessor.GetRequired());
    }

    [Fact]
    public void GetRequired_WithoutScope_ThrowsInvalidOperation()
    {
        var accessor = new AgentExecutionContextAccessor();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            accessor.GetRequired());

        Assert.Contains("execution scope", ex.Message);
    }

    [Fact]
    public void GetRequired_WithNullAccessor_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AgentExecutionContextExtensions.GetRequired(null!));
    }

    // -------------------------------------------------------------------------
    // AgentExecutionContext record
    // -------------------------------------------------------------------------

    [Fact]
    public void Context_UserId_And_OrchestrationId_ArePreserved()
    {
        var ctx = new AgentExecutionContext("user-42", "orch-99");

        Assert.Equal("user-42", ctx.UserId);
        Assert.Equal("orch-99", ctx.OrchestrationId);
    }

    [Fact]
    public void Context_Properties_DefaultToEmpty()
    {
        IAgentExecutionContext ctx = new AgentExecutionContext("u", "o");

        Assert.NotNull(ctx.Properties);
        Assert.Empty(ctx.Properties);
    }

    [Fact]
    public void Context_Properties_WhenProvided_AreAccessible()
    {
        var props = new Dictionary<string, object> { ["tenant"] = "acme" };
        IAgentExecutionContext ctx = new AgentExecutionContext("u", "o", props);

        Assert.Equal("acme", ctx.Properties["tenant"]);
    }

    [Fact]
    public void Context_WithExpression_CreatesNewInstance()
    {
        var original = new AgentExecutionContext("user-1", "orch-1");
        var derived = original with { UserId = "user-2" };

        Assert.Equal("user-2", derived.UserId);
        Assert.Equal("orch-1", derived.OrchestrationId);
        Assert.NotSame(original, derived);
    }

    // -------------------------------------------------------------------------
    // DI registration
    // -------------------------------------------------------------------------

    [Fact]
    public void UsingAgentFramework_RegistersAccessor()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();

        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object))
            .BuildServiceProvider(config);

        var accessor = sp.GetService<IAgentExecutionContextAccessor>();

        Assert.NotNull(accessor);
        Assert.IsType<AgentExecutionContextAccessor>(accessor);
    }
}
