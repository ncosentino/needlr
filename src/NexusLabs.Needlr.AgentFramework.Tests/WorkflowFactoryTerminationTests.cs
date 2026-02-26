using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.AgentFramework.Workflows;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Tests for Layer 1 termination condition wiring in <see cref="WorkflowFactory"/>.
/// Verifies that <see cref="AgentTerminationConditionAttribute"/> declarations on group chat
/// members are read and wired into <c>RoundRobinGroupChatManager</c>.
/// </summary>
public class WorkflowFactoryTerminationTests
{
    private static IWorkflowFactory BuildFactory()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        return new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgent<TermKeywordReviewerAgent>()
                .AddAgent<TermKeywordDeveloperAgent>()
                .AddAgent<TermNoConditionAgentA>()
                .AddAgent<TermNoConditionAgentB>())
            .BuildServiceProvider(config)
            .GetRequiredService<IWorkflowFactory>();
    }

    [Fact]
    public void CreateGroupChatWorkflow_AgentHasTerminationConditionAttribute_ReturnsWorkflow()
    {
        var factory = BuildFactory();

        var workflow = factory.CreateGroupChatWorkflow("term-code-review");

        Assert.NotNull(workflow);
        Assert.IsAssignableFrom<Workflow>(workflow);
    }

    [Fact]
    public void CreateGroupChatWorkflow_NoTerminationConditions_ReturnsWorkflow()
    {
        var factory = BuildFactory();

        var workflow = factory.CreateGroupChatWorkflow("term-no-conditions");

        Assert.NotNull(workflow);
        Assert.IsAssignableFrom<Workflow>(workflow);
    }
}

// ---------------------------------------------------------------------------
// Test agents for termination condition wiring tests
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Review code and output APPROVED when satisfied.")]
[AgentGroupChatMember("term-code-review")]
[AgentTerminationCondition(typeof(KeywordTerminationCondition), "APPROVED")]
public sealed class TermKeywordReviewerAgent { }

[NeedlrAiAgent(Instructions = "Propose code changes.")]
[AgentGroupChatMember("term-code-review")]
public sealed class TermKeywordDeveloperAgent { }

[NeedlrAiAgent(Instructions = "Agent A with no termination condition.")]
[AgentGroupChatMember("term-no-conditions")]
public sealed class TermNoConditionAgentA { }

[NeedlrAiAgent(Instructions = "Agent B with no termination condition.")]
[AgentGroupChatMember("term-no-conditions")]
public sealed class TermNoConditionAgentB { }
