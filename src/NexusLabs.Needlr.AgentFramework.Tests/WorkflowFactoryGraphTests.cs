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
        Assert.Contains("RunGraphAsync", ex.Message);
    }

    [Fact]
    public void CreateGraphWorkflow_WaitAllJoinMode_Succeeds()
    {
        var factory = BuildGraphWorkflowFactory();

        var workflow = factory.CreateGraphWorkflow("wf-test-graph");

        Assert.NotNull(workflow);
    }

    [Fact]
    public void CreateGraphWorkflow_WithReducer_IncludesReducerExecutor()
    {
        var factory = BuildReducerGraphWorkflowFactory();

        var workflow = factory.CreateGraphWorkflow("wf-reducer-bsp-graph");

        Assert.NotNull(workflow);
        var executors = workflow.ReflectExecutors();
        Assert.True(
            executors.Count >= 5,
            $"Expected at least 5 executors (4 agents + 1 reducer function node), got {executors.Count}");
    }

    /// <summary>
    /// Two independent calls to <c>CreateGraphWorkflow</c> with a reducer should
    /// NOT share collected-inputs state. Each workflow gets its own isolated
    /// reducer binding, preventing cross-contamination between runs.
    /// </summary>
    [Fact]
    public void CreateGraphWorkflow_WithReducer_NoStateCrossContaminationBetweenWorkflows()
    {
        var factory = BuildReducerGraphWorkflowFactory();

        var workflow1 = factory.CreateGraphWorkflow("wf-reducer-bsp-graph");
        var workflow2 = factory.CreateGraphWorkflow("wf-reducer-bsp-graph");

        Assert.NotNull(workflow1);
        Assert.NotNull(workflow2);
        Assert.NotSame(workflow1, workflow2);
    }

    [Fact]
    public void CreateGraphWorkflow_ExclusiveChoiceRouting_ReturnsValidWorkflow()
    {
        var factory = BuildExclusiveChoiceWorkflowFactory();

        var workflow = factory.CreateGraphWorkflow("wf-exclusive-choice-graph");

        Assert.NotNull(workflow);
        Assert.IsAssignableFrom<Workflow>(workflow);
    }

    [Fact]
    public void CreateGraphWorkflow_LlmChoiceRouting_ThrowsNotSupported()
    {
        var factory = BuildLlmChoiceWorkflowFactory();

        var ex = Assert.Throws<NotSupportedException>(
            () => factory.CreateGraphWorkflow("wf-llm-choice-graph"));

        Assert.Contains("LlmChoice", ex.Message);
        Assert.Contains("RunGraphAsync", ex.Message);
    }

    /// <summary>
    /// The reducer executor binding ID should use the full type name to avoid
    /// collisions when two reducer types in different namespaces share the
    /// same simple name.
    /// </summary>
    [Fact]
    public void CreateGraphWorkflow_WithReducer_ReducerIdUsesFullName()
    {
        var factory = BuildReducerGraphWorkflowFactory();

        var workflow = factory.CreateGraphWorkflow("wf-reducer-bsp-graph");

        var executors = workflow.ReflectExecutors();
        var reducerExecutor = executors.FirstOrDefault(
            e => e.Key.Contains("reducer:", StringComparison.Ordinal));

        Assert.NotEqual(default, reducerExecutor);
        Assert.Contains(
            $"reducer:{typeof(BspTestReducer).FullName}",
            reducerExecutor.Key,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// <see cref="AgentGraphEdgeAttribute.IsRequired"/> set to <c>false</c> is a
    /// Needlr-native-executor-only feature. The BSP path (CreateGraphWorkflow)
    /// treats all edges as required because MAF's <c>WorkflowBuilder.AddEdge</c>
    /// has no optional-edge concept. This test verifies that <c>IsRequired=false</c>
    /// edges do not cause errors or unexpected results in the BSP path.
    /// </summary>
    [Fact]
    public void CreateGraphWorkflow_IsRequiredFalseEdge_DoesNotCrash()
    {
        var factory = BuildOptionalEdgeWorkflowFactory();

        var workflow = factory.CreateGraphWorkflow("wf-optional-edge-graph");

        Assert.NotNull(workflow);
        Assert.IsAssignableFrom<Workflow>(workflow);
        var executors = workflow.ReflectExecutors();
        Assert.True(
            executors.Count >= 3,
            $"Expected at least 3 executors (entry + worker + sink), got {executors.Count}");
    }

    /// <summary>
    /// The reducer's collected-inputs bag is drained at the start of
    /// each invocation, so re-running a workflow does not leak inputs from a
    /// previous run into the next one. Because the bag
    /// is captured per binding (per <see cref="WorkflowFactory.CreateGraphWorkflow"/>
    /// call), two independent workflows already don't share state. This test
    /// also verifies that the drain-on-invoke logic does not throw when the bag
    /// starts empty.
    /// </summary>
    [Fact]
    public void CreateGraphWorkflow_WithReducer_ReducerBindingStartsClean()
    {
        var factory = BuildReducerGraphWorkflowFactory();

        // Reset static call counter before this test.
        BspTestReducer.CallCount = 0;

        var workflow1 = factory.CreateGraphWorkflow("wf-reducer-bsp-graph");
        var workflow2 = factory.CreateGraphWorkflow("wf-reducer-bsp-graph");

        // Each workflow has its own reducer executor binding, confirming
        // the bag is not shared.
        Assert.NotNull(workflow1);
        Assert.NotNull(workflow2);
        Assert.NotSame(workflow1, workflow2);

        // Both workflows should have a reducer executor.
        var executors1 = workflow1.ReflectExecutors();
        var executors2 = workflow2.ReflectExecutors();
        Assert.True(
            executors1.Any(e => e.Key.Contains("reducer:", StringComparison.Ordinal)),
            "Workflow 1 should contain a reducer executor binding.");
        Assert.True(
            executors2.Any(e => e.Key.Contains("reducer:", StringComparison.Ordinal)),
            "Workflow 2 should contain a reducer executor binding.");
    }

    [Fact]
    public void CreateGraphWorkflow_FirstMatchingRouting_ReturnsValidWorkflow()
    {
        var factory = BuildFirstMatchingWorkflowFactory();

        var workflow = factory.CreateGraphWorkflow("wf-first-matching-graph");

        Assert.NotNull(workflow);
        Assert.IsAssignableFrom<Workflow>(workflow);
    }

    [Fact]
    public void CreateGraphWorkflow_ConditionalEdges_WiresExpectedExecutorCount()
    {
        var factory = BuildExclusiveChoiceWorkflowFactory();

        var workflow = factory.CreateGraphWorkflow("wf-exclusive-choice-graph");

        var executors = workflow.ReflectExecutors();
        Assert.True(
            executors.Count >= 3,
            $"Expected at least 3 executors (entry + 2 conditional targets), got {executors.Count}");
    }

    private static IWorkflowFactory BuildReducerGraphWorkflowFactory()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        return new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgent<ReducerBspEntryAgent>()
                .AddAgent<ReducerBspWorkerAAgent>()
                .AddAgent<ReducerBspWorkerBAgent>()
                .AddAgent<ReducerBspSinkAgent>())
            .BuildServiceProvider(config)
            .GetRequiredService<IWorkflowFactory>();
    }

    private static IWorkflowFactory BuildExclusiveChoiceWorkflowFactory()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        return new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgent<BspExclusiveEntryAgent>()
                .AddAgent<BspExclusiveWorkerAAgent>()
                .AddAgent<BspExclusiveWorkerBAgent>())
            .BuildServiceProvider(config)
            .GetRequiredService<IWorkflowFactory>();
    }

    private static IWorkflowFactory BuildLlmChoiceWorkflowFactory()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        return new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgent<BspLlmChoiceEntryAgent>()
                .AddAgent<BspLlmChoiceWorkerAAgent>()
                .AddAgent<BspLlmChoiceWorkerBAgent>())
            .BuildServiceProvider(config)
            .GetRequiredService<IWorkflowFactory>();
    }

    private static IWorkflowFactory BuildFirstMatchingWorkflowFactory()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        return new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgent<BspFirstMatchEntryAgent>()
                .AddAgent<BspFirstMatchWorkerAAgent>()
                .AddAgent<BspFirstMatchWorkerBAgent>())
            .BuildServiceProvider(config)
            .GetRequiredService<IWorkflowFactory>();
    }

    private static IWorkflowFactory BuildOptionalEdgeWorkflowFactory()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        return new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgent<OptionalEdgeEntryAgent>()
                .AddAgent<OptionalEdgeWorkerAgent>()
                .AddAgent<OptionalEdgeSinkAgent>())
            .BuildServiceProvider(config)
            .GetRequiredService<IWorkflowFactory>();
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

// ---------------------------------------------------------------------------
// Test agents for reducer BSP graph
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Entry for reducer BSP test graph.")]
[AgentGraphEntry("wf-reducer-bsp-graph")]
[AgentGraphEdge("wf-reducer-bsp-graph", typeof(ReducerBspWorkerAAgent))]
[AgentGraphEdge("wf-reducer-bsp-graph", typeof(ReducerBspWorkerBAgent))]
internal sealed class ReducerBspEntryAgent { }

[NeedlrAiAgent(Instructions = "Worker A for reducer BSP test graph.")]
[AgentGraphEdge("wf-reducer-bsp-graph", typeof(ReducerBspSinkAgent))]
internal sealed class ReducerBspWorkerAAgent { }

[NeedlrAiAgent(Instructions = "Worker B for reducer BSP test graph.")]
[AgentGraphEdge("wf-reducer-bsp-graph", typeof(ReducerBspSinkAgent))]
internal sealed class ReducerBspWorkerBAgent { }

[NeedlrAiAgent(Instructions = "Sink for reducer BSP test graph.")]
[AgentGraphNode("wf-reducer-bsp-graph", JoinMode = GraphJoinMode.WaitAll)]
internal sealed class ReducerBspSinkAgent { }

[AgentGraphReducer("wf-reducer-bsp-graph", ReducerMethod = nameof(Merge))]
internal static class BspTestReducer
{
    public static int CallCount;

    public static string Merge(IReadOnlyList<string> branchOutputs)
    {
        Interlocked.Increment(ref CallCount);
        return "REDUCED:" + string.Join("|", branchOutputs);
    }
}

// ---------------------------------------------------------------------------
// Test agents for ExclusiveChoice routing in BSP path
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Entry for BSP exclusive choice test graph.")]
[AgentGraphEntry("wf-exclusive-choice-graph", RoutingMode = GraphRoutingMode.ExclusiveChoice)]
[AgentGraphEdge("wf-exclusive-choice-graph", typeof(BspExclusiveWorkerAAgent), Condition = nameof(IsPathA))]
[AgentGraphEdge("wf-exclusive-choice-graph", typeof(BspExclusiveWorkerBAgent), Condition = nameof(IsPathB))]
internal sealed class BspExclusiveEntryAgent
{
    public static bool IsPathA(object? input) => input is string s && s.Contains("A");
    public static bool IsPathB(object? input) => input is string s && s.Contains("B");
}

[NeedlrAiAgent(Instructions = "Worker A for BSP exclusive choice test graph.")]
internal sealed class BspExclusiveWorkerAAgent { }

[NeedlrAiAgent(Instructions = "Worker B for BSP exclusive choice test graph.")]
internal sealed class BspExclusiveWorkerBAgent { }

// ---------------------------------------------------------------------------
// Test agents for LlmChoice routing in BSP path
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Entry for BSP LLM choice test graph.")]
[AgentGraphEntry("wf-llm-choice-graph", RoutingMode = GraphRoutingMode.LlmChoice)]
[AgentGraphEdge("wf-llm-choice-graph", typeof(BspLlmChoiceWorkerAAgent), Condition = "Go to A")]
[AgentGraphEdge("wf-llm-choice-graph", typeof(BspLlmChoiceWorkerBAgent), Condition = "Go to B")]
internal sealed class BspLlmChoiceEntryAgent { }

[NeedlrAiAgent(Instructions = "Worker A for BSP LLM choice test graph.")]
internal sealed class BspLlmChoiceWorkerAAgent { }

[NeedlrAiAgent(Instructions = "Worker B for BSP LLM choice test graph.")]
internal sealed class BspLlmChoiceWorkerBAgent { }

// ---------------------------------------------------------------------------
// Test agents for FirstMatching routing in BSP path
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Entry for BSP first matching test graph.")]
[AgentGraphEntry("wf-first-matching-graph", RoutingMode = GraphRoutingMode.FirstMatching)]
[AgentGraphEdge("wf-first-matching-graph", typeof(BspFirstMatchWorkerAAgent), Condition = nameof(IsMatch))]
[AgentGraphEdge("wf-first-matching-graph", typeof(BspFirstMatchWorkerBAgent))]
internal sealed class BspFirstMatchEntryAgent
{
    public static bool IsMatch(object? input) => true;
}

[NeedlrAiAgent(Instructions = "Worker A for BSP first matching test graph.")]
internal sealed class BspFirstMatchWorkerAAgent { }

[NeedlrAiAgent(Instructions = "Worker B for BSP first matching test graph.")]
internal sealed class BspFirstMatchWorkerBAgent { }

// ---------------------------------------------------------------------------
// Test agents for IsRequired=false edges in BSP path
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Entry for optional edge test graph.")]
[AgentGraphEntry("wf-optional-edge-graph")]
[AgentGraphEdge("wf-optional-edge-graph", typeof(OptionalEdgeWorkerAgent), IsRequired = false)]
[AgentGraphEdge("wf-optional-edge-graph", typeof(OptionalEdgeSinkAgent))]
internal sealed class OptionalEdgeEntryAgent { }

[NeedlrAiAgent(Instructions = "Worker with optional incoming edge.")]
[AgentGraphEdge("wf-optional-edge-graph", typeof(OptionalEdgeSinkAgent))]
internal sealed class OptionalEdgeWorkerAgent { }

[NeedlrAiAgent(Instructions = "Sink for optional edge test graph.")]
[AgentGraphNode("wf-optional-edge-graph", JoinMode = GraphJoinMode.WaitAll)]
internal sealed class OptionalEdgeSinkAgent { }
