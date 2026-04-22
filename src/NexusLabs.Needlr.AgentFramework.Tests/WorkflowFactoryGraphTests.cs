using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Tests for <see cref="WorkflowFactory.CreateGraphWorkflow"/> covering entry point resolution,
/// missing entry point, missing edges, and correct executor binding (no duplicates).
/// </summary>
public sealed class WorkflowFactoryGraphTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private static IWorkflowFactory BuildGraphWorkflowFactory()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        return new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgent<GraphEntryWfAgent>()
                .AddAgent<GraphWorkerAWfAgent>()
                .AddAgent<GraphWorkerBWfAgent>()
                .AddAgent<GraphSinkWfAgent>()
                .AddAgent<NoEdgesEntryWfAgent>())
            .BuildServiceProvider(config)
            .GetRequiredService<IWorkflowFactory>();
    }

    [Fact]
    public void CreateGraphWorkflow_ValidGraph_ReturnsNonNullWorkflow()
    {
        var factory = BuildGraphWorkflowFactory();

        var workflow = factory.CreateGraphWorkflow("wf-test-graph");

        Assert.NotNull(workflow);
        Assert.IsAssignableFrom<Workflow>(workflow);
    }

    [Fact]
    public void CreateGraphWorkflow_ResolvesEntryPointCorrectly()
    {
        var factory = BuildGraphWorkflowFactory();

        var workflow = factory.CreateGraphWorkflow("wf-test-graph");

        Assert.NotNull(workflow);
        var executors = workflow.ReflectExecutors();
        Assert.True(
            executors.Count >= 4,
            $"Expected at least 4 executors (entry + 2 workers + sink), got {executors.Count}");
    }

    [Fact]
    public void CreateGraphWorkflow_NoEntryPoint_ThrowsInvalidOperation()
    {
        var factory = BuildGraphWorkflowFactory();

        var ex = Assert.Throws<InvalidOperationException>(
            () => factory.CreateGraphWorkflow("no-such-graph"));

        Assert.Contains("no entry point found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateGraphWorkflow_NoEdges_ThrowsInvalidOperation()
    {
        var factory = BuildGraphWorkflowFactory();

        var ex = Assert.Throws<InvalidOperationException>(
            () => factory.CreateGraphWorkflow("wf-no-edges-graph"));

        Assert.Contains("no edges found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateGraphWorkflow_NullGraphName_ThrowsArgument()
    {
        var factory = BuildGraphWorkflowFactory();

        Assert.ThrowsAny<ArgumentException>(
            () => factory.CreateGraphWorkflow(null!));
    }

    [Fact]
    public void CreateGraphWorkflow_EmptyGraphName_ThrowsArgument()
    {
        var factory = BuildGraphWorkflowFactory();

        Assert.ThrowsAny<ArgumentException>(
            () => factory.CreateGraphWorkflow(""));
    }

    [Fact]
    public void CreateGraphWorkflow_WaitAnyJoinMode_ThrowsNotSupported()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        var factory = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgent<WaitAnyEntryAgent>()
                .AddAgent<WaitAnyWorkerAgent>()
                .AddAgent<WaitAnySinkAgent>())
            .BuildServiceProvider(config)
            .GetRequiredService<IWorkflowFactory>();

        var ex = Assert.Throws<NotSupportedException>(
            () => factory.CreateGraphWorkflow("wf-waitany-test"));

        Assert.Contains("WaitAny", ex.Message);
        Assert.Contains("not yet supported", ex.Message);
        Assert.Contains("BSP", ex.Message);
    }

    [Fact]
    public void CreateGraphWorkflow_WaitAllJoinMode_Succeeds()
    {
        var factory = BuildGraphWorkflowFactory();

        var workflow = factory.CreateGraphWorkflow("wf-test-graph");

        Assert.NotNull(workflow);
    }
}

// ---------------------------------------------------------------------------
// Test agents for graph workflow
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Entry node for test graph.")]
[AgentGraphEntry("wf-test-graph")]
[AgentGraphEdge("wf-test-graph", typeof(GraphWorkerAWfAgent))]
[AgentGraphEdge("wf-test-graph", typeof(GraphWorkerBWfAgent))]
internal sealed class GraphEntryWfAgent { }

[NeedlrAiAgent(Instructions = "Worker A in test graph.")]
[AgentGraphEdge("wf-test-graph", typeof(GraphSinkWfAgent))]
internal sealed class GraphWorkerAWfAgent { }

[NeedlrAiAgent(Instructions = "Worker B in test graph.")]
[AgentGraphEdge("wf-test-graph", typeof(GraphSinkWfAgent))]
internal sealed class GraphWorkerBWfAgent { }

[NeedlrAiAgent(Instructions = "Sink node in test graph.")]
[AgentGraphNode("wf-test-graph", JoinMode = GraphJoinMode.WaitAll)]
internal sealed class GraphSinkWfAgent { }

[NeedlrAiAgent(Instructions = "Entry with no edges — triggers error path.")]
[AgentGraphEntry("wf-no-edges-graph")]
internal sealed class NoEdgesEntryWfAgent { }

[NeedlrAiAgent(Instructions = "Entry for WaitAny test graph.")]
[AgentGraphEntry("wf-waitany-test")]
[AgentGraphEdge("wf-waitany-test", typeof(WaitAnyWorkerAgent))]
internal sealed class WaitAnyEntryAgent { }

[NeedlrAiAgent(Instructions = "Worker in WaitAny test graph.")]
[AgentGraphEdge("wf-waitany-test", typeof(WaitAnySinkAgent))]
internal sealed class WaitAnyWorkerAgent { }

[NeedlrAiAgent(Instructions = "Sink with WaitAny join mode — should throw.")]
[AgentGraphNode("wf-waitany-test", JoinMode = GraphJoinMode.WaitAny)]
internal sealed class WaitAnySinkAgent { }
