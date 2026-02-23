using System.ComponentModel;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Tests for <see cref="NeedlrAiAgentAttribute"/>-based agent declaration and resolution,
/// including parity tests between <see cref="IAgentFactory.CreateAgent{TAgent}"/> and
/// <see cref="IAgentFactory.CreateAgent(string)"/>.
/// </summary>
public class NeedlrAiAgentDeclarationTests
{
    private static IAgentFactory BuildFactory(Func<AgentFrameworkSyringe, AgentFrameworkSyringe> configure)
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        return new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af =>
            {
                af = af.Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object);
                return configure(af);
            })
            .BuildServiceProvider(config)
            .GetRequiredService<IAgentFactory>();
    }

    [Fact]
    public void CreateAgent_ByType_WithNeedlrAiAgentAttribute_ReturnsNonNullAgent()
    {
        var factory = BuildFactory(af => af.AddAgent<TriageTestAgent>());

        var agent = factory.CreateAgent<TriageTestAgent>();

        Assert.NotNull(agent);
    }

    [Fact]
    public void CreateAgent_ByType_UsesAttributeName()
    {
        var factory = BuildFactory(af => af.AddAgent<TriageTestAgent>());

        var agent = factory.CreateAgent<TriageTestAgent>();

        Assert.Equal(nameof(TriageTestAgent), agent.Name);
    }

    [Fact]
    public void CreateAgent_ByType_WithNoNeedlrAiAgentAttribute_ThrowsInvalidOperation()
    {
        var factory = BuildFactory(af => af);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            factory.CreateAgent<UndecoratedAgent>());

        Assert.Contains(nameof(UndecoratedAgent), ex.Message);
        Assert.Contains("[NeedlrAiAgent]", ex.Message);
    }

    [Fact]
    public void CreateAgent_ByClassName_MatchesCreateAgentByType()
    {
        var factory = BuildFactory(af => af.AddAgent<TriageTestAgent>());

        var byType = factory.CreateAgent<TriageTestAgent>();
        var byName = factory.CreateAgent(nameof(TriageTestAgent));

        Assert.Equal(byType.Name, byName.Name);
    }

    [Fact]
    public void CreateAgent_ByClassName_NotRegistered_ThrowsInvalidOperation()
    {
        var factory = BuildFactory(af => af);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            factory.CreateAgent("NonExistentAgent"));

        Assert.Contains("NonExistentAgent", ex.Message);
    }

    [Fact]
    public void AddAgentsFromGenerated_MatchesAddAgentPerType()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();

        var perTypeFactory = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgent<TriageTestAgent>()
                .AddAgent<BillingTestAgent>())
            .BuildServiceProvider(config)
            .GetRequiredService<IAgentFactory>();

        var fromGeneratedFactory = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgentsFromGenerated([typeof(TriageTestAgent), typeof(BillingTestAgent)]))
            .BuildServiceProvider(config)
            .GetRequiredService<IAgentFactory>();

        var triagePerType = perTypeFactory.CreateAgent<TriageTestAgent>();
        var triageFromGenerated = fromGeneratedFactory.CreateAgent<TriageTestAgent>();

        var billingPerType = perTypeFactory.CreateAgent(nameof(BillingTestAgent));
        var billingFromGenerated = fromGeneratedFactory.CreateAgent(nameof(BillingTestAgent));

        Assert.Equal(triagePerType.Name, triageFromGenerated.Name);
        Assert.Equal(billingPerType.Name, billingFromGenerated.Name);
    }
}

[NeedlrAiAgent(Instructions = "Route customer queries to the right specialist.")]
public sealed class TriageTestAgent { }

[NeedlrAiAgent(
    Instructions = "Resolve billing queries.",
    FunctionTypes = [typeof(TestBillingFunctions)])]
public sealed class BillingTestAgent { }

public sealed class UndecoratedAgent { }

public sealed class TestBillingFunctions
{
    [AgentFunction]
    [Description("Gets a billing summary.")]
    public string GetBillingSummary() => "billing-summary";
}
