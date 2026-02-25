using System.ComponentModel;

using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Tests for <see cref="WorkflowFactory"/> covering both the reflection discovery path
/// (no bootstrap registration) and the source-generated bootstrap path (via
/// <see cref="AgentFrameworkGeneratedBootstrap.BeginTestScope"/>), plus parity between them.
/// </summary>
/// <remarks>
/// Because the generator does not run on the test project under project references,
/// <see cref="AgentFrameworkGeneratedBootstrap"/> starts with an empty registration list.
/// This means all tests that do <em>not</em> call
/// <see cref="AgentFrameworkGeneratedBootstrap.BeginTestScope"/> exercise the reflection path.
/// Tests that call <see cref="AgentFrameworkGeneratedBootstrap.BeginTestScope"/> exercise
/// the bootstrap path, simulating what the source generator would emit.
/// </remarks>
public class WorkflowFactoryTests
{
    private static IWorkflowFactory BuildWorkflowFactory(Func<AgentFrameworkSyringe, AgentFrameworkSyringe> configure)
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        return new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => configure(
                af.Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)))
            .BuildServiceProvider(config)
            .GetRequiredService<IWorkflowFactory>();
    }

    private static IWorkflowFactory BuildDefaultWorkflowFactory() =>
        BuildWorkflowFactory(af => af
            .AddAgent<TriageWfAgent>()
            .AddAgent<BillingWfAgent>()
            .AddAgent<TechWfAgent>()
            .AddAgent<ReviewerWfAgent>()
            .AddAgent<RevieweeWfAgent>()
            .AddAgent<NoHandoffWfAgent>()
            .AddAgent<OnlyMemberWfAgent>());

    // -------------------------------------------------------------------------
    // Handoff — reflection path
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateHandoffWorkflow_ReflectionPath_ReturnsNonNullWorkflow()
    {
        var factory = BuildDefaultWorkflowFactory();

        var workflow = factory.CreateHandoffWorkflow<TriageWfAgent>();

        Assert.NotNull(workflow);
        Assert.IsAssignableFrom<Workflow>(workflow);
    }

    [Fact]
    public void CreateHandoffWorkflow_ReflectionPath_NoHandoffAttributes_ThrowsInvalidOperation()
    {
        var factory = BuildDefaultWorkflowFactory();

        Assert.Throws<InvalidOperationException>(() =>
            factory.CreateHandoffWorkflow<NoHandoffWfAgent>());
    }

    // -------------------------------------------------------------------------
    // Handoff — bootstrap path
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateHandoffWorkflow_BootstrapPath_ReturnsNonNullWorkflow()
    {
        var factory = BuildDefaultWorkflowFactory();

        using var scope = AgentFrameworkGeneratedBootstrap.BeginTestScope(
            functionTypes: static () => [],
            groupTypes: static () => new Dictionary<string, IReadOnlyList<Type>>(),
            agentTypes: static () => [typeof(TriageWfAgent), typeof(BillingWfAgent), typeof(TechWfAgent)],
            handoffTopology: static () => new Dictionary<Type, IReadOnlyList<(Type, string?)>>
            {
                [typeof(TriageWfAgent)] =
                [
                    (typeof(BillingWfAgent), null),
                    (typeof(TechWfAgent), null)
                ]
            });

        var workflow = factory.CreateHandoffWorkflow<TriageWfAgent>();

        Assert.NotNull(workflow);
        Assert.IsAssignableFrom<Workflow>(workflow);
    }

    [Fact]
    public void CreateHandoffWorkflow_BootstrapPath_TypeNotInTopology_FallsBackToReflection()
    {
        var factory = BuildDefaultWorkflowFactory();

        // Scope with empty topology — type not in bootstrap, so reflection is used.
        // TriageWfAgent has [AgentHandoffsTo] attributes, so reflection succeeds.
        using var scope = AgentFrameworkGeneratedBootstrap.BeginTestScope(
            functionTypes: static () => [],
            groupTypes: static () => new Dictionary<string, IReadOnlyList<Type>>(),
            agentTypes: static () => [],
            handoffTopology: static () => new Dictionary<Type, IReadOnlyList<(Type, string?)>>());

        var workflow = factory.CreateHandoffWorkflow<TriageWfAgent>();

        Assert.NotNull(workflow);
    }

    // -------------------------------------------------------------------------
    // Handoff — parity
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateHandoffWorkflow_BootstrapAndReflectionPaths_BothReturnWorkflow()
    {
        var factory = BuildDefaultWorkflowFactory();

        // Reflection path (no scope active, bootstrap empty)
        var reflectionWorkflow = factory.CreateHandoffWorkflow<TriageWfAgent>();

        // Bootstrap path (scope active with matching topology)
        using var scope = AgentFrameworkGeneratedBootstrap.BeginTestScope(
            functionTypes: static () => [],
            groupTypes: static () => new Dictionary<string, IReadOnlyList<Type>>(),
            agentTypes: static () => [typeof(TriageWfAgent), typeof(BillingWfAgent), typeof(TechWfAgent)],
            handoffTopology: static () => new Dictionary<Type, IReadOnlyList<(Type, string?)>>
            {
                [typeof(TriageWfAgent)] =
                [
                    (typeof(BillingWfAgent), null),
                    (typeof(TechWfAgent), null)
                ]
            });

        var bootstrapWorkflow = factory.CreateHandoffWorkflow<TriageWfAgent>();

        Assert.NotNull(reflectionWorkflow);
        Assert.NotNull(bootstrapWorkflow);
        Assert.Equal(
            reflectionWorkflow.ReflectExecutors().Count,
            bootstrapWorkflow.ReflectExecutors().Count);
    }

    // -------------------------------------------------------------------------
    // Group chat — reflection path
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateGroupChatWorkflow_ReflectionPath_ReturnsNonNullWorkflow()
    {
        var factory = BuildDefaultWorkflowFactory();

        var workflow = factory.CreateGroupChatWorkflow("wf-code-review");

        Assert.NotNull(workflow);
        Assert.IsAssignableFrom<Workflow>(workflow);
    }

    [Fact]
    public void CreateGroupChatWorkflow_ReflectionPath_SingleMemberGroup_ThrowsInvalidOperation()
    {
        var factory = BuildDefaultWorkflowFactory();

        Assert.Throws<InvalidOperationException>(() =>
            factory.CreateGroupChatWorkflow("wf-solo-group"));
    }

    // -------------------------------------------------------------------------
    // Group chat — bootstrap path
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateGroupChatWorkflow_BootstrapPath_ReturnsNonNullWorkflow()
    {
        var factory = BuildDefaultWorkflowFactory();

        using var scope = AgentFrameworkGeneratedBootstrap.BeginTestScope(
            functionTypes: static () => [],
            groupTypes: static () => new Dictionary<string, IReadOnlyList<Type>>(),
            agentTypes: static () => [typeof(ReviewerWfAgent), typeof(RevieweeWfAgent)],
            groupChatGroups: static () => new Dictionary<string, IReadOnlyList<Type>>
            {
                ["wf-code-review"] = [typeof(ReviewerWfAgent), typeof(RevieweeWfAgent)]
            });

        var workflow = factory.CreateGroupChatWorkflow("wf-code-review");

        Assert.NotNull(workflow);
        Assert.IsAssignableFrom<Workflow>(workflow);
    }

    [Fact]
    public void CreateGroupChatWorkflow_BootstrapPath_UnknownGroup_ThrowsInvalidOperation()
    {
        var factory = BuildDefaultWorkflowFactory();

        using var scope = AgentFrameworkGeneratedBootstrap.BeginTestScope(
            functionTypes: static () => [],
            groupTypes: static () => new Dictionary<string, IReadOnlyList<Type>>(),
            agentTypes: static () => [],
            groupChatGroups: static () => new Dictionary<string, IReadOnlyList<Type>>());

        Assert.Throws<InvalidOperationException>(() =>
            factory.CreateGroupChatWorkflow("no-such-group"));
    }

    // -------------------------------------------------------------------------
    // Group chat — parity
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateGroupChatWorkflow_BootstrapAndReflectionPaths_BothReturnWorkflow()
    {
        var factory = BuildDefaultWorkflowFactory();

        // Reflection path (no scope active)
        var reflectionWorkflow = factory.CreateGroupChatWorkflow("wf-code-review");

        // Bootstrap path (scope active with matching groups)
        using var scope = AgentFrameworkGeneratedBootstrap.BeginTestScope(
            functionTypes: static () => [],
            groupTypes: static () => new Dictionary<string, IReadOnlyList<Type>>(),
            agentTypes: static () => [typeof(ReviewerWfAgent), typeof(RevieweeWfAgent)],
            groupChatGroups: static () => new Dictionary<string, IReadOnlyList<Type>>
            {
                ["wf-code-review"] = [typeof(ReviewerWfAgent), typeof(RevieweeWfAgent)]
            });

        var bootstrapWorkflow = factory.CreateGroupChatWorkflow("wf-code-review");

        Assert.NotNull(reflectionWorkflow);
        Assert.NotNull(bootstrapWorkflow);
        Assert.Equal(
            reflectionWorkflow.ReflectExecutors().Count,
            bootstrapWorkflow.ReflectExecutors().Count);
    }
}

// ---------------------------------------------------------------------------
// Test agents — defined at namespace level to avoid nested class complications
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Route customer queries to the right specialist.")]
[AgentHandoffsTo(typeof(BillingWfAgent))]
[AgentHandoffsTo(typeof(TechWfAgent))]
public sealed class TriageWfAgent { }

[NeedlrAiAgent(Instructions = "Resolve billing and payment queries.")]
public sealed class BillingWfAgent { }

[NeedlrAiAgent(Instructions = "Resolve technical support queries.")]
public sealed class TechWfAgent { }

[NeedlrAiAgent(Instructions = "Review code changes as an expert reviewer.")]
[AgentGroupChatMember("wf-code-review")]
public sealed class ReviewerWfAgent { }

[NeedlrAiAgent(Instructions = "Propose code changes and receive review feedback.")]
[AgentGroupChatMember("wf-code-review")]
public sealed class RevieweeWfAgent { }

[NeedlrAiAgent(Instructions = "Agent with no handoff targets — triggers error path.")]
public sealed class NoHandoffWfAgent { }

[NeedlrAiAgent(Instructions = "Agent in a group with only one member — triggers error path.")]
[AgentGroupChatMember("wf-solo-group")]
public sealed class OnlyMemberWfAgent { }
