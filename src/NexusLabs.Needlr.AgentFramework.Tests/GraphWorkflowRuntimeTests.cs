using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.AgentFramework.Progress;
using NexusLabs.Needlr.AgentFramework.Workflows;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// End-to-end runtime tests for DAG graph execution via <c>RunGraphAsync</c>.
/// Each test covers a specific gap identified in the DAG audit: condition routing,
/// IsRequired failure propagation, reducer invocation, WaitAny semantics,
/// progress events, routing mode enforcement, and per-node routing overrides.
/// </summary>
public sealed class GraphWorkflowRuntimeTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    // -----------------------------------------------------------------------
    // 1. Condition-based routing
    // -----------------------------------------------------------------------

    /// <summary>
    /// A conditional edge whose predicate returns false should NOT execute
    /// the target agent. Only the unconditional branch should run.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_ConditionalEdgeFalse_SkipsBranch()
    {
        var events = new List<IProgressEvent>();
        var reporter = new TestProgressReporter(events);
        var runner = BuildCondRoutingFactory();

        var result = await runner.RunGraphAsync(
            "cond-routing-graph",
            "input that does NOT match",
            progress: reporter,
            cancellationToken: _ct);

        Assert.True(result.Succeeded, $"Graph should succeed. Error: {result.ErrorMessage}");

        var invokedNames = events.OfType<AgentInvokedEvent>().Select(e => e.NodeId).ToList();
        Assert.Contains(invokedNames, n => n is not null && n.EndsWith(nameof(CondAlwaysWorkerAgent)));
        Assert.DoesNotContain(invokedNames, n => n is not null && n.EndsWith(nameof(CondGatedWorkerAgent)));
    }

    /// <summary>
    /// An unconditional edge should always execute its target.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_UnconditionalEdge_AlwaysExecutes()
    {
        var events = new List<IProgressEvent>();
        var reporter = new TestProgressReporter(events);
        var runner = BuildCondRoutingFactory();

        var result = await runner.RunGraphAsync(
            "cond-routing-graph",
            "input that does NOT match",
            progress: reporter,
            cancellationToken: _ct);

        Assert.True(result.Succeeded, $"Graph should succeed. Error: {result.ErrorMessage}");

        var invokedNames = events.OfType<AgentInvokedEvent>().Select(e => e.NodeId).ToList();
        Assert.Contains(invokedNames, n => n is not null && n.EndsWith(nameof(CondAlwaysWorkerAgent)));
    }

    // -----------------------------------------------------------------------
    // 2. IsRequired failure propagation
    // -----------------------------------------------------------------------

    /// <summary>
    /// When a required edge's target fails, the entire graph fails.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_RequiredEdgeFails_GraphFails()
    {
        var runner = BuildFailingFactory(
            failAgentName: "ReqFailWorkerAgent",
            failingIsRequired: true);

        var result = await runner.RunGraphAsync(
            "req-fail-graph",
            "test input",
            cancellationToken: _ct);

        Assert.False(result.Succeeded, "Graph should fail when a required edge target fails");
        Assert.NotNull(result.ErrorMessage);
    }

    /// <summary>
    /// When an optional edge's target fails, other branches complete successfully.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_OptionalEdgeFails_OtherBranchesComplete()
    {
        var invokedAgents = new List<string>();
        var events = new List<IProgressEvent>();
        var reporter = new TestProgressReporter(events);
        var runner = BuildOptionalFailFactory(invokedAgents);

        var result = await runner.RunGraphAsync(
            "opt-fail-graph",
            "test input",
            progress: reporter,
            cancellationToken: _ct);

        Assert.True(result.Succeeded, $"Graph should succeed when only optional edge fails. Error: {result.ErrorMessage}");

        var completedNames = events.OfType<AgentCompletedEvent>().Select(e => e.AgentName).ToList();
        Assert.True(
            completedNames.Count >= 2,
            $"Expected at least 2 completed agents (entry + ok worker), got {completedNames.Count}");
    }

    /// <summary>
    /// Mix of required and optional edges: required failure kills graph.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_MixedEdges_RequiredFailureKillsGraph()
    {
        var runner = BuildFailingFactory(
            failAgentName: "MixReqFailWorkerAgent",
            failingIsRequired: true);

        var result = await runner.RunGraphAsync(
            "mix-fail-graph",
            "test input",
            cancellationToken: _ct);

        Assert.False(result.Succeeded, "Graph should fail when a required edge target fails");
    }

    // -----------------------------------------------------------------------
    // 3. Reducer invocation
    // -----------------------------------------------------------------------

    /// <summary>
    /// When a reducer is registered for a graph, its method should be invoked
    /// with the branch outputs and its return value passed downstream.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_WithReducer_ReducerMethodIsCalled()
    {
        TestReducer.CallCount = 0;
        TestReducer.ReceivedInputs = null;
        var invokedAgents = new List<string>();
        var runner = BuildReducerFactory(invokedAgents);

        var result = await runner.RunGraphAsync(
            "reducer-graph",
            "test input",
            cancellationToken: _ct);

        Assert.True(result.Succeeded, $"Graph should succeed. Error: {result.ErrorMessage}");
        Assert.Equal(1, TestReducer.CallCount);
        Assert.NotNull(TestReducer.ReceivedInputs);
        Assert.True(
            TestReducer.ReceivedInputs!.Count >= 2,
            $"Reducer should receive outputs from both branches, got {TestReducer.ReceivedInputs.Count}");
    }

    /// <summary>
    /// Reducer return value should be passed as input to the downstream node.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_WithReducer_ReducerOutputPassedDownstream()
    {
        TestReducer.CallCount = 0;
        TestReducer.ReceivedInputs = null;
        var invokedAgents = new List<string>();
        var events = new List<IProgressEvent>();
        var reporter = new TestProgressReporter(events);
        var runner = BuildReducerFactory(invokedAgents);

        var result = await runner.RunGraphAsync(
            "reducer-graph",
            "test input",
            progress: reporter,
            cancellationToken: _ct);

        Assert.True(result.Succeeded, $"Graph should succeed. Error: {result.ErrorMessage}");
        Assert.Equal(1, TestReducer.CallCount);

        var reducerEvents = events.OfType<ReducerNodeInvokedEvent>().ToList();
        Assert.Single(reducerEvents);
        Assert.True(reducerEvents[0].InputBranchCount >= 2,
            $"Reducer should process at least 2 branches, got {reducerEvents[0].InputBranchCount}");
    }

    /// <summary>
    /// Without a reducer, outputs are concatenated (existing behavior).
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_WithoutReducer_OutputsConcatenated()
    {
        var runner = BuildCondRoutingFactory();

        var result = await runner.RunGraphAsync(
            "cond-routing-graph",
            "GATE_OPEN",
            cancellationToken: _ct);

        Assert.True(result.Succeeded, $"Graph should succeed. Error: {result.ErrorMessage}");
    }

    // -----------------------------------------------------------------------
    // 4. WaitAny actually uses Task.WhenAny
    // -----------------------------------------------------------------------

    /// <summary>
    /// Two upstream nodes: one fast, one slow. WaitAny fan-in should start
    /// after the fast one, not wait for both.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_WaitAny_ProceedsOnFirstComplete()
    {
        var events = new List<IProgressEvent>();
        var reporter = new TestProgressReporter(events);
        var runner = BuildWaitAnyTimingFactory(new Dictionary<string, DateTimeOffset>());

        var result = await runner.RunGraphAsync(
            "waitany-timing-graph",
            "test input",
            progress: reporter,
            cancellationToken: _ct);

        Assert.True(result.Succeeded, $"Graph should succeed. Error: {result.ErrorMessage}");

        // Verify the sink was invoked
        var invokedNodes = events.OfType<AgentInvokedEvent>().Select(e => e.NodeId).ToList();
        Assert.Contains(invokedNodes, n => n is not null && n.EndsWith(nameof(WaitAnySinkTimingAgent)));

        // Verify WaitAny semantics: the total graph duration should be significantly
        // less than the slow node's 2s delay + entry + sink time (if it waited for all,
        // it would be >2s). With WaitAny, the sink starts as soon as the fast worker completes.
        Assert.True(
            result.TotalDuration.TotalSeconds < 1.5,
            $"WaitAny graph should complete quickly (got {result.TotalDuration.TotalSeconds:F2}s). " +
            $"If >2s, WaitAny may be waiting for the slow node.");
    }

    /// <summary>
    /// After WaitAny resolves with the fast branch, the remaining slow branch's
    /// cancellation token should be signalled so it stops executing.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_WaitAny_CancelsRemainingBranches()
    {
        var slowBranchCancelled = new TaskCompletionSource<bool>();
        var slowMockEntered = new TaskCompletionSource<bool>();
        var runner = BuildWaitAnyCancellationFactory(slowBranchCancelled, slowMockEntered);

        var result = await runner.RunGraphAsync(
            "waitany-cancel-graph",
            "test input",
            cancellationToken: _ct);

        Assert.True(result.Succeeded, $"Graph should succeed. Error: {result.ErrorMessage}");

        // Verify the slow mock was actually entered (rules out routing issues).
        Assert.True(slowMockEntered.Task.IsCompletedSuccessfully,
            "Slow worker mock should have been entered. " +
            $"Completed nodes: {string.Join(", ", result.NodeResults.Keys)}");

        var wasCancelled = await slowBranchCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), _ct);
        Assert.True(wasCancelled, "Slow branch should have been cancelled after WaitAny resolved");
    }

    // -----------------------------------------------------------------------
    // 5. Progress events for WaitAny path
    // -----------------------------------------------------------------------

    /// <summary>
    /// WaitAny execution should emit AgentInvokedEvent for each node.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_WaitAny_EmitsAgentInvokedEvents()
    {
        var events = new List<IProgressEvent>();
        var reporter = new TestProgressReporter(events);
        var invokedAgents = new List<string>();
        var runner = BuildWaitAnyFactory(invokedAgents);

        await runner.RunGraphAsync(
            "waitany-events-graph",
            "test input",
            progress: reporter,
            cancellationToken: _ct);

        var invokedEvents = events.OfType<AgentInvokedEvent>().ToList();
        Assert.True(
            invokedEvents.Count >= 3,
            $"Expected at least 3 AgentInvokedEvents (entry + worker + sink), got {invokedEvents.Count}");
    }

    /// <summary>
    /// WaitAny execution should emit WorkflowStartedEvent and WorkflowCompletedEvent.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_WaitAny_EmitsWorkflowLifecycleEvents()
    {
        var events = new List<IProgressEvent>();
        var reporter = new TestProgressReporter(events);
        var invokedAgents = new List<string>();
        var runner = BuildWaitAnyFactory(invokedAgents);

        await runner.RunGraphAsync(
            "waitany-events-graph",
            "test input",
            progress: reporter,
            cancellationToken: _ct);

        Assert.Single(events.OfType<WorkflowStartedEvent>());
        Assert.Single(events.OfType<WorkflowCompletedEvent>());
    }

    // -----------------------------------------------------------------------
    // 6. RoutingMode enforcement
    // -----------------------------------------------------------------------

    /// <summary>
    /// FirstMatching: only the first matching edge fires even if multiple would match.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_FirstMatching_OnlyFirstEdgeFires()
    {
        var invokedAgents = new List<string>();
        var events = new List<IProgressEvent>();
        var reporter = new TestProgressReporter(events);
        var runner = BuildFirstMatchingFactory(invokedAgents);

        var result = await runner.RunGraphAsync(
            "first-matching-graph",
            "MATCH_ALL",
            progress: reporter,
            cancellationToken: _ct);

        Assert.True(result.Succeeded, $"Graph should succeed. Error: {result.ErrorMessage}");

        var invokedNodes = events.OfType<AgentInvokedEvent>().Select(e => e.NodeId).ToList();
        Assert.Contains(invokedNodes, n => n is not null && n.EndsWith(nameof(FirstMatchWorkerAAgent)));
        Assert.DoesNotContain(invokedNodes, n => n is not null && n.EndsWith(nameof(FirstMatchWorkerBAgent)));
    }

    /// <summary>
    /// ExclusiveChoice: exactly one must fire. If zero match, graph fails.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_ExclusiveChoice_ZeroMatches_GraphFails()
    {
        var invokedAgents = new List<string>();
        var runner = BuildExclusiveChoiceFactory(invokedAgents);

        var result = await runner.RunGraphAsync(
            "exclusive-choice-graph",
            "NO_MATCH_ANYWHERE",
            cancellationToken: _ct);

        Assert.False(result.Succeeded, "Graph should fail when ExclusiveChoice has zero matches");
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("ExclusiveChoice", result.ErrorMessage);
    }

    /// <summary>
    /// ExclusiveChoice with multiple matches causes graph failure.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_ExclusiveChoice_MultipleMatches_GraphFails()
    {
        var invokedAgents = new List<string>();
        var runner = BuildExclusiveChoiceFactory(invokedAgents);

        var result = await runner.RunGraphAsync(
            "exclusive-choice-graph",
            "MATCH_BOTH",
            cancellationToken: _ct);

        Assert.False(result.Succeeded, "Graph should fail when ExclusiveChoice has multiple matches");
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("ExclusiveChoice", result.ErrorMessage);
    }

    /// <summary>
    /// LlmChoice: the LLM picks which edge to follow by returning the condition text.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_LlmChoice_LlmPicksRoute()
    {
        var invokedAgents = new List<string>();
        var runner = BuildLlmChoiceFactory(invokedAgents);

        var result = await runner.RunGraphAsync(
            "llm-choice-graph",
            "test input about searching the web",
            cancellationToken: _ct);

        Assert.True(result.Succeeded, $"LlmChoice graph should succeed. Error: {result.ErrorMessage}");

        var completedNodes = result.NodeResults.Keys.ToList();
        Assert.True(
            completedNodes.Any(n => n.Contains("LlmChoiceWorkerA", StringComparison.OrdinalIgnoreCase)),
            $"LlmChoice should route to WorkerA (web search). Completed: {string.Join(", ", completedNodes)}");
    }

    /// <summary>
    /// LlmChoice routing skips the unchosen branch — only the LLM-selected
    /// target should execute.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_LlmChoice_UnchosenBranchSkipped()
    {
        var events = new List<IProgressEvent>();
        var reporter = new TestProgressReporter(events);
        var invokedAgents = new List<string>();
        var runner = BuildLlmChoiceFactory(invokedAgents);

        var result = await runner.RunGraphAsync(
            "llm-choice-graph",
            "test input about searching the web",
            progress: reporter,
            cancellationToken: _ct);

        Assert.True(result.Succeeded, $"LlmChoice graph should succeed. Error: {result.ErrorMessage}");

        var invokedNodes = events.OfType<AgentInvokedEvent>().Select(e => e.NodeId).ToList();
        Assert.DoesNotContain(invokedNodes, n => n is not null && n.EndsWith(nameof(LlmChoiceWorkerBAgent)));
    }

    /// <summary>
    /// LlmChoice fallback: when the LLM response does not match any condition,
    /// the first conditional edge is selected as a fallback.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_LlmChoice_NoMatchFallsBackToFirst()
    {
        var runner = BuildLlmChoiceNoMatchFactory();

        var result = await runner.RunGraphAsync(
            "llm-choice-graph",
            "test input with no matching route",
            cancellationToken: _ct);

        Assert.True(result.Succeeded, $"LlmChoice fallback should succeed. Error: {result.ErrorMessage}");

        var completedNodes = result.NodeResults.Keys.ToList();
        Assert.True(
            completedNodes.Any(n => n.Contains("LlmChoiceWorkerA", StringComparison.OrdinalIgnoreCase)),
            $"LlmChoice should fall back to first edge (WorkerA). Completed: {string.Join(", ", completedNodes)}");
    }

    /// <summary>
    /// LlmChoice number-based routing: the LLM returns "2" and the router
    /// selects the second conditional edge.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_LlmChoice_NumberBasedRouting_PicksCorrectEdge()
    {
        var invokedAgents = new List<string>();
        var runner = BuildLlmChoiceNumberRouteFactory(invokedAgents, llmResponse: "2");

        var result = await runner.RunGraphAsync(
            "llm-choice-number-graph",
            "decide which route",
            cancellationToken: _ct);

        Assert.True(result.Succeeded, $"LlmChoice number routing should succeed. Error: {result.ErrorMessage}");

        var completedNodes = result.NodeResults.Keys.ToList();
        Assert.True(
            completedNodes.Any(n => n.Contains("LlmChoiceNumberWorkerB", StringComparison.OrdinalIgnoreCase)),
            $"LlmChoice should route to WorkerB (second edge). Completed: {string.Join(", ", completedNodes)}");
        Assert.False(
            completedNodes.Any(n => n.Contains("LlmChoiceNumberWorkerA", StringComparison.OrdinalIgnoreCase)),
            $"LlmChoice should NOT route to WorkerA. Completed: {string.Join(", ", completedNodes)}");
    }

    /// <summary>
    /// LlmChoice exact-text fallback: the LLM returns "web-analysis" which must
    /// match only the "web-analysis" condition and NOT the "web-research" condition
    /// (which would happen with Contains-based matching on "web").
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_LlmChoice_ExactTextMatch_PicksCorrectEdge()
    {
        var invokedAgents = new List<string>();
        var runner = BuildLlmChoiceNumberRouteFactory(invokedAgents, llmResponse: "web-analysis");

        var result = await runner.RunGraphAsync(
            "llm-choice-number-graph",
            "decide which route",
            cancellationToken: _ct);

        Assert.True(result.Succeeded, $"LlmChoice exact match should succeed. Error: {result.ErrorMessage}");

        var completedNodes = result.NodeResults.Keys.ToList();
        Assert.True(
            completedNodes.Any(n => n.Contains("LlmChoiceNumberWorkerB", StringComparison.OrdinalIgnoreCase)),
            $"LlmChoice should route to WorkerB (web-analysis). Completed: {string.Join(", ", completedNodes)}");
        Assert.False(
            completedNodes.Any(n => n.Contains("LlmChoiceNumberWorkerA", StringComparison.OrdinalIgnoreCase)),
            $"LlmChoice should NOT route to WorkerA (web-research). Completed: {string.Join(", ", completedNodes)}");
    }

    // -----------------------------------------------------------------------
    // 7. NodeRoutingMode per-node override
    // -----------------------------------------------------------------------

    /// <summary>
    /// Graph-wide Deterministic but one node overridden to FirstMatching.
    /// The override node should follow FirstMatching semantics.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_NodeRoutingModeOverride_FirstMatching()
    {
        var invokedAgents = new List<string>();
        var events = new List<IProgressEvent>();
        var reporter = new TestProgressReporter(events);
        var runner = BuildNodeOverrideFactory(invokedAgents);

        var result = await runner.RunGraphAsync(
            "node-override-graph",
            "MATCH_ALL",
            progress: reporter,
            cancellationToken: _ct);

        Assert.True(result.Succeeded, $"Graph should succeed. Error: {result.ErrorMessage}");

        var invokedNodes = events.OfType<AgentInvokedEvent>().Select(e => e.NodeId).ToList();
        Assert.Contains(invokedNodes, n => n is not null && n.EndsWith(nameof(NodeOverrideWorkerAAgent)));
        Assert.DoesNotContain(invokedNodes, n => n is not null && n.EndsWith(nameof(NodeOverrideWorkerBAgent)));
    }

    // -----------------------------------------------------------------------
    // 8. Concurrent fan-in reducer thread-safety
    // -----------------------------------------------------------------------

    /// <summary>
    /// Two branches complete near-simultaneously into a reducer. The reducer
    /// must receive exactly the expected inputs without cross-contamination
    /// from shared mutable state in the closure.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_ConcurrentFanIn_ReducerReceivesExactInputs()
    {
        ConcurrentReducerValidator.Reset();
        var runner = BuildConcurrentReducerFactory();

        var result = await runner.RunGraphAsync(
            "concurrent-reducer-graph",
            "trigger concurrent branches",
            cancellationToken: _ct);

        Assert.True(result.Succeeded, $"Graph should succeed. Error: {result.ErrorMessage}");
        Assert.True(
            ConcurrentReducerValidator.CallCount >= 1,
            $"Reducer should be called at least once, got {ConcurrentReducerValidator.CallCount}");

        // The last invocation should have exactly 2 inputs (one per branch).
        Assert.NotNull(ConcurrentReducerValidator.LastReceivedInputs);
        Assert.Equal(2, ConcurrentReducerValidator.LastReceivedInputs!.Count);
        Assert.Contains("branch-a-output", ConcurrentReducerValidator.LastReceivedInputs);
        Assert.Contains("branch-b-output", ConcurrentReducerValidator.LastReceivedInputs);
    }

    // -----------------------------------------------------------------------
    // 9. Runtime error path coverage
    // -----------------------------------------------------------------------

    /// <summary>
    /// A condition method that throws should cause the graph to fail
    /// with a meaningful error, not silently swallow the exception.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_ConditionMethodThrows_GraphFailsWithError()
    {
        var runner = BuildThrowingConditionFactory();

        var result = await runner.RunGraphAsync(
            "throwing-condition-graph",
            "test input",
            cancellationToken: _ct);

        Assert.False(result.Succeeded, "Graph should fail when condition method throws");
        Assert.NotNull(result.ErrorMessage);
    }

    /// <summary>
    /// A reducer method that throws should cause the graph to fail with
    /// the reducer exception propagated.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_ReducerMethodThrows_GraphFailsWithReducerError()
    {
        ThrowingReducer.Reset();
        var runner = BuildThrowingReducerFactory();

        var result = await runner.RunGraphAsync(
            "throwing-reducer-graph",
            "test input",
            cancellationToken: _ct);

        Assert.False(result.Succeeded, "Graph should fail when reducer method throws");
        Assert.NotNull(result.ErrorMessage);
    }

    /// <summary>
    /// AllMatching routing with 3 conditional edges where 2 conditions pass:
    /// exactly 2 branches should execute.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_AllMatching_TwoOfThreeConditionsTrue_ExactlyTwoBranchesExecute()
    {
        var events = new List<IProgressEvent>();
        var reporter = new TestProgressReporter(events);
        var runner = BuildAllMatchingFactory();

        var result = await runner.RunGraphAsync(
            "all-matching-graph",
            "MATCH_A_AND_B",
            progress: reporter,
            cancellationToken: _ct);

        Assert.True(result.Succeeded, $"Graph should succeed. Error: {result.ErrorMessage}");

        var invokedNodes = events.OfType<AgentInvokedEvent>().Select(e => e.NodeId).ToList();
        Assert.Contains(invokedNodes, n => n is not null && n.EndsWith(nameof(AllMatchWorkerAAgent)));
        Assert.Contains(invokedNodes, n => n is not null && n.EndsWith(nameof(AllMatchWorkerBAgent)));
        Assert.DoesNotContain(invokedNodes, n => n is not null && n.EndsWith(nameof(AllMatchWorkerCAgent)));
    }

    /// <summary>
    /// Deterministic routing with all conditions false and no unconditional edges:
    /// the source node becomes terminal and the graph completes successfully
    /// (no downstream nodes execute).
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_DeterministicNoMatchingEdge_NodeBecomesTerminal()
    {
        var events = new List<IProgressEvent>();
        var reporter = new TestProgressReporter(events);
        var runner = BuildNoMatchingEdgeFactory();

        var result = await runner.RunGraphAsync(
            "no-matching-edge-graph",
            "NO_MATCH_AT_ALL",
            progress: reporter,
            cancellationToken: _ct);

        Assert.True(result.Succeeded, $"Graph should succeed when node is terminal. Error: {result.ErrorMessage}");

        var invokedNodes = events.OfType<AgentInvokedEvent>().Select(e => e.NodeId).ToList();
        Assert.Contains(invokedNodes, n => n is not null && n.EndsWith(nameof(NoMatchEntryAgent)));
        Assert.DoesNotContain(invokedNodes, n => n is not null && n.EndsWith(nameof(NoMatchWorkerAgent)));
    }

    /// <summary>
    /// An agent that throws during execution with IsRequired=true should fail the graph.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_AgentThrows_RequiredTrue_GraphFails()
    {
        var runner = BuildAgentThrowsFactory(isRequired: true);

        var result = await runner.RunGraphAsync(
            "agent-throws-req-graph",
            "test input",
            cancellationToken: _ct);

        Assert.False(result.Succeeded, "Graph should fail when a required agent throws");
        Assert.NotNull(result.ErrorMessage);
    }

    /// <summary>
    /// An agent that throws during execution with IsRequired=false should not
    /// fail the graph; other branches continue.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_AgentThrows_RequiredFalse_GraphContinues()
    {
        var events = new List<IProgressEvent>();
        var reporter = new TestProgressReporter(events);
        var runner = BuildAgentThrowsFactory(isRequired: false);

        var result = await runner.RunGraphAsync(
            "agent-throws-opt-graph",
            "test input",
            progress: reporter,
            cancellationToken: _ct);

        Assert.True(result.Succeeded, $"Graph should succeed when optional agent throws. Error: {result.ErrorMessage}");
    }

    /// <summary>
    /// A pre-cancelled cancellation token should cause the run to exit promptly.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_PreCancelledToken_ExitsPromptly()
    {
        var runner = BuildCondRoutingFactory();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // RunGraphAsync may throw OperationCanceledException or return
        // a failed result — either is acceptable for cancellation.
        try
        {
            var result = await runner.RunGraphAsync(
                "cond-routing-graph",
                "test input",
                cancellationToken: cts.Token);

            // If it returns instead of throwing, verify it either failed or
            // completed extremely quickly.
            Assert.True(
                !result.Succeeded || sw.Elapsed.TotalSeconds < 2,
                $"Pre-cancelled token should fail or complete instantly, took {sw.Elapsed.TotalSeconds:F2}s");
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        sw.Stop();
        Assert.True(sw.Elapsed.TotalSeconds < 5,
            $"Pre-cancelled token should exit promptly, took {sw.Elapsed.TotalSeconds:F2}s");
    }

    // -----------------------------------------------------------------------
    // 11. Agent identity — NodeId uses FullName
    // -----------------------------------------------------------------------

    /// <summary>
    /// <c>NodeResults</c> keys should be namespace-qualified (FullName), not simple class names.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_NodeResultKeys_UseFullName()
    {
        var runner = BuildCondRoutingFactory();

        var result = await runner.RunGraphAsync(
            "cond-routing-graph",
            "input that does NOT match",
            cancellationToken: _ct);

        Assert.True(result.Succeeded, $"Graph should succeed. Error: {result.ErrorMessage}");

        // All keys should contain a dot (namespace separator), proving they
        // are FullName-qualified rather than simple Name.
        foreach (var key in result.NodeResults.Keys)
        {
            Assert.Contains(".", key,
                StringComparison.Ordinal);
        }
    }

    // -----------------------------------------------------------------------
    // 12. Fan-out branches — BranchResults grouping by Type-based edges
    // -----------------------------------------------------------------------

    /// <summary>
    /// Fan-out from a single entry to two workers should produce a branch group
    /// when both workers share the same inbound edge (the entry node).
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_FanOut_BranchResultsGroupBySharedInboundEdges()
    {
        var invokedAgents = new List<string>();
        var runner = BuildReducerFactory(invokedAgents);

        var result = await runner.RunGraphAsync(
            "reducer-graph",
            "test input",
            cancellationToken: _ct);

        Assert.True(result.Succeeded, $"Graph should succeed. Error: {result.ErrorMessage}");

        // The reducer-graph has Entry → WorkerA + WorkerB → Sink.
        // WorkerA and WorkerB share the same inbound edge (Entry),
        // so they should be grouped into a branch.
        Assert.True(result.BranchResults.Count >= 1,
            $"Expected at least one branch group for fan-out workers, got {result.BranchResults.Count}. " +
            $"NodeResults keys: [{string.Join(", ", result.NodeResults.Keys)}]");

        var firstBranch = result.BranchResults.Values.First();
        Assert.True(firstBranch.Count >= 2,
            $"Branch should contain at least 2 agents (the fan-out workers), got {firstBranch.Count}");
    }

    // -----------------------------------------------------------------------
    // Factory builders
    // -----------------------------------------------------------------------

    private static IGraphWorkflowRunner BuildCondRoutingFactory()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        // Echo the user input back as the response so condition predicates
        // can evaluate against the original user intent.
        mockChatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((msgs, _, _) =>
            {
                var userMsg = msgs.LastOrDefault(m => m.Role == ChatRole.User);
                var text = userMsg?.Text ?? "response";
                return Task.FromResult(new ChatResponse(
                    new ChatMessage(ChatRole.Assistant, text)));
            });

        return new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgent<CondEntryAgent>()
                .AddAgent<CondGatedWorkerAgent>()
                .AddAgent<CondAlwaysWorkerAgent>()
                .AddAgent<CondSinkAgent>())
            .UsingGraphWorkflows()
            .BuildServiceProvider(config)
            .GetRequiredService<IGraphWorkflowRunner>();
    }

    private static IGraphWorkflowRunner BuildFailingFactory(
        string failAgentName,
        bool failingIsRequired)
    {
        var config = new ConfigurationBuilder().Build();
        var callCount = 0;
        var mockChatClient = new Mock<IChatClient>();
        // First call succeeds (entry agent), subsequent calls fail
        mockChatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((_, _, _) =>
            {
                var count = Interlocked.Increment(ref callCount);
                if (count > 1)
                    throw new InvalidOperationException("Simulated worker failure");

                return Task.FromResult(new ChatResponse(
                    new ChatMessage(ChatRole.Assistant, "entry-ok")));
            });

        var builder = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgent<ReqFailEntryAgent>()
                .AddAgent<ReqFailWorkerAgent>()
                .AddAgent<ReqOkWorkerAgent>()
                .AddAgent<ReqFailSinkAgent>()
                .AddAgent<MixReqFailEntryAgent>()
                .AddAgent<MixReqFailWorkerAgent>()
                .AddAgent<MixOptOkWorkerAgent>()
                .AddAgent<MixReqFailSinkAgent>());

        return builder
            .UsingGraphWorkflows()
            .BuildServiceProvider(config)
            .GetRequiredService<IGraphWorkflowRunner>();
    }

    private static IGraphWorkflowRunner BuildOptionalFailFactory(List<string> invokedAgents)
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((messages, _, _) =>
            {
                // Fail when the system prompt contains the optional worker's instructions.
                var systemText = messages.FirstOrDefault(m => m.Role == ChatRole.System)?.Text ?? "";
                if (systemText.Contains("optional-fail-worker", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Simulated optional worker failure");

                return Task.FromResult(new ChatResponse(
                    new ChatMessage(ChatRole.Assistant, "ok")));
            });

        var builder = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgent<OptFailEntryAgent>()
                .AddAgent<OptFailWorkerAgent>()
                .AddAgent<OptOkWorkerAgent>()
                .AddAgent<OptFailSinkAgent>());

        return builder
            .UsingGraphWorkflows()
            .BuildServiceProvider(config)
            .GetRequiredService<IGraphWorkflowRunner>();
    }

    private static IGraphWorkflowRunner BuildReducerFactory(
        List<string> invokedAgents,
        Dictionary<string, string>? captureInputs = null)
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "branch-output")));

        var builder = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgent<ReducerEntryAgent>()
                .AddAgent<ReducerWorkerAAgent>()
                .AddAgent<ReducerWorkerBAgent>()
                .AddAgent<ReducerSinkAgent>()
                .AddAgent<ReducerTerminalAgent>());

        return builder
            .UsingGraphWorkflows()
            .BuildServiceProvider(config)
            .GetRequiredService<IGraphWorkflowRunner>();
    }

    private static IGraphWorkflowRunner BuildWaitAnyTimingFactory(
        Dictionary<string, DateTimeOffset> nodeStartTimes)
    {
        var config = new ConfigurationBuilder().Build();
        var callCount = 0;
        var normalMock = new Mock<IChatClient>();
        normalMock.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>(async (_, _, ct) =>
            {
                var count = Interlocked.Increment(ref callCount);
                // Third call is the slow worker (entry=1, fast=2, slow=3)
                // But concurrent ordering is non-deterministic.
                // Instead, just return fast for all — the WaitAny semantics are
                // tested by checking that the graph completes quickly.
                return new ChatResponse(new ChatMessage(ChatRole.Assistant, "output"));
            });

        var builder = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => normalMock.Object)
                .AddAgent<WaitAnyTimingEntryAgent>()
                .AddAgent<FastTimingWorkerAgent>()
                .AddAgent<SlowTimingWorkerAgent>()
                .AddAgent<WaitAnySinkTimingAgent>());

        return builder
            .UsingGraphWorkflows()
            .BuildServiceProvider(config)
            .GetRequiredService<IGraphWorkflowRunner>();
    }

    private static IGraphWorkflowRunner BuildWaitAnyFactory(List<string> invokedAgents)
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "response")));

        var builder = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgent<WaitAnyEventsEntryAgent>()
                .AddAgent<WaitAnyEventsWorkerAgent>()
                .AddAgent<WaitAnyEventsSinkAgent>());

        return builder
            .UsingGraphWorkflows()
            .BuildServiceProvider(config)
            .GetRequiredService<IGraphWorkflowRunner>();
    }

    private static IGraphWorkflowRunner BuildFirstMatchingFactory(List<string> invokedAgents)
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "MATCH_ALL")));

        var builder = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgent<FirstMatchEntryAgent>()
                .AddAgent<FirstMatchWorkerAAgent>()
                .AddAgent<FirstMatchWorkerBAgent>()
                .AddAgent<FirstMatchSinkAgent>());

        return builder
            .UsingGraphWorkflows()
            .BuildServiceProvider(config)
            .GetRequiredService<IGraphWorkflowRunner>();
    }

    private static IGraphWorkflowRunner BuildExclusiveChoiceFactory(List<string> invokedAgents)
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "response")));

        var builder = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgent<ExclusiveEntryAgent>()
                .AddAgent<ExclusiveWorkerAAgent>()
                .AddAgent<ExclusiveWorkerBAgent>()
                .AddAgent<ExclusiveSinkAgent>());

        return builder
            .UsingGraphWorkflows()
            .BuildServiceProvider(config)
            .GetRequiredService<IGraphWorkflowRunner>();
    }

    private static IGraphWorkflowRunner BuildLlmChoiceFactory(List<string> invokedAgents)
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((messages, _, _) =>
            {
                var userText = messages.FirstOrDefault(m => m.Role == ChatRole.User)?.Text ?? "";
                if (userText.Contains("Available routes:", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(new ChatResponse(
                        new ChatMessage(ChatRole.Assistant, "Do web search")));
                }

                return Task.FromResult(new ChatResponse(
                    new ChatMessage(ChatRole.Assistant, "response")));
            });

        var builder = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgent<LlmChoiceEntryAgent>()
                .AddAgent<LlmChoiceWorkerAAgent>()
                .AddAgent<LlmChoiceWorkerBAgent>());

        return builder
            .UsingGraphWorkflows()
            .BuildServiceProvider(config)
            .GetRequiredService<IGraphWorkflowRunner>();
    }

    private static IGraphWorkflowRunner BuildLlmChoiceNoMatchFactory()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((messages, _, _) =>
            {
                var userText = messages.FirstOrDefault(m => m.Role == ChatRole.User)?.Text ?? "";
                if (userText.Contains("Available routes:", StringComparison.OrdinalIgnoreCase))
                {
                    // Return a response that doesn't match any condition string
                    return Task.FromResult(new ChatResponse(
                        new ChatMessage(ChatRole.Assistant, "I'm not sure what to do")));
                }

                return Task.FromResult(new ChatResponse(
                    new ChatMessage(ChatRole.Assistant, "response")));
            });

        var builder = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgent<LlmChoiceEntryAgent>()
                .AddAgent<LlmChoiceWorkerAAgent>()
                .AddAgent<LlmChoiceWorkerBAgent>());

        return builder
            .UsingGraphWorkflows()
            .BuildServiceProvider(config)
            .GetRequiredService<IGraphWorkflowRunner>();
    }

    private static IGraphWorkflowRunner BuildLlmChoiceNumberRouteFactory(
        List<string> invokedAgents,
        string llmResponse)
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((messages, _, _) =>
            {
                var userText = messages.FirstOrDefault(m => m.Role == ChatRole.User)?.Text ?? "";
                if (userText.Contains("Available routes:", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(new ChatResponse(
                        new ChatMessage(ChatRole.Assistant, llmResponse)));
                }

                return Task.FromResult(new ChatResponse(
                    new ChatMessage(ChatRole.Assistant, "response")));
            });

        var builder = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgent<LlmChoiceNumberEntryAgent>()
                .AddAgent<LlmChoiceNumberWorkerAAgent>()
                .AddAgent<LlmChoiceNumberWorkerBAgent>());

        return builder
            .UsingGraphWorkflows()
            .BuildServiceProvider(config)
            .GetRequiredService<IGraphWorkflowRunner>();
    }

    private static IGraphWorkflowRunner BuildNodeOverrideFactory(List<string> invokedAgents)
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "MATCH_ALL")));

        var builder = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgent<NodeOverrideEntryAgent>()
                .AddAgent<NodeOverrideWorkerAAgent>()
                .AddAgent<NodeOverrideWorkerBAgent>()
                .AddAgent<NodeOverrideSinkAgent>());

        return builder
            .UsingGraphWorkflows()
            .BuildServiceProvider(config)
            .GetRequiredService<IGraphWorkflowRunner>();
    }

    private static IGraphWorkflowRunner BuildWaitAnyCancellationFactory(
        TaskCompletionSource<bool> slowBranchCancelled,
        TaskCompletionSource<bool> slowMockEntered)
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>(async (messages, opts, ct) =>
            {
                // Check both messages and ChatOptions.Instructions
                // (MAF may put agent instructions in ChatOptions instead of a system message).
                var systemText = messages.FirstOrDefault(m => m.Role == ChatRole.System)?.Text ?? "";
                var optionsInstructions = opts?.Instructions ?? "";
                var allInstructions = systemText + " " + optionsInstructions;

                if (allInstructions.Contains("slow-cancel-worker", StringComparison.OrdinalIgnoreCase))
                {
                    slowMockEntered.TrySetResult(true);
                    ct.Register(() => slowBranchCancelled.TrySetResult(true));
                    await Task.Delay(TimeSpan.FromSeconds(30), ct);
                }
                else if (allInstructions.Contains("fast-cancel-worker", StringComparison.OrdinalIgnoreCase))
                {
                    // Small delay so the slow worker has time to enter its mock.
                    await Task.Delay(200);
                }

                return new ChatResponse(new ChatMessage(ChatRole.Assistant, "output"));
            });

        var builder = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgent<WaitAnyCancelEntryAgent>()
                .AddAgent<FastCancelWorkerAgent>()
                .AddAgent<SlowCancelWorkerAgent>()
                .AddAgent<WaitAnyCancelSinkAgent>());

        return builder
            .UsingGraphWorkflows()
            .BuildServiceProvider(config)
            .GetRequiredService<IGraphWorkflowRunner>();
    }

    private static IGraphWorkflowRunner BuildConcurrentReducerFactory()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((messages, opts, _) =>
            {
                var systemText = messages.FirstOrDefault(m => m.Role == ChatRole.System)?.Text ?? "";
                var optionsInstructions = opts?.Instructions ?? "";
                var allInstructions = systemText + " " + optionsInstructions;

                if (allInstructions.Contains("worker-a", StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "branch-a-output")));
                if (allInstructions.Contains("worker-b", StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "branch-b-output")));
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "response")));
            });

        return new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgent<ConcReducerEntryAgent>()
                .AddAgent<ConcReducerWorkerAAgent>()
                .AddAgent<ConcReducerWorkerBAgent>()
                .AddAgent<ConcReducerSinkAgent>())
            .UsingGraphWorkflows()
            .BuildServiceProvider(config)
            .GetRequiredService<IGraphWorkflowRunner>();
    }

    private static IGraphWorkflowRunner BuildThrowingConditionFactory()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "response")));

        return new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgent<ThrowCondEntryAgent>()
                .AddAgent<ThrowCondWorkerAgent>())
            .UsingGraphWorkflows()
            .BuildServiceProvider(config)
            .GetRequiredService<IGraphWorkflowRunner>();
    }

    private static IGraphWorkflowRunner BuildThrowingReducerFactory()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "branch-output")));

        return new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgent<ThrowReducerEntryAgent>()
                .AddAgent<ThrowReducerWorkerAAgent>()
                .AddAgent<ThrowReducerWorkerBAgent>()
                .AddAgent<ThrowReducerSinkAgent>())
            .UsingGraphWorkflows()
            .BuildServiceProvider(config)
            .GetRequiredService<IGraphWorkflowRunner>();
    }

    private static IGraphWorkflowRunner BuildAllMatchingFactory()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "MATCH_A_AND_B")));

        return new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgent<AllMatchEntryAgent>()
                .AddAgent<AllMatchWorkerAAgent>()
                .AddAgent<AllMatchWorkerBAgent>()
                .AddAgent<AllMatchWorkerCAgent>()
                .AddAgent<AllMatchSinkAgent>())
            .UsingGraphWorkflows()
            .BuildServiceProvider(config)
            .GetRequiredService<IGraphWorkflowRunner>();
    }

    private static IGraphWorkflowRunner BuildNoMatchingEdgeFactory()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "NO_MATCH_AT_ALL")));

        return new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgent<NoMatchEntryAgent>()
                .AddAgent<NoMatchWorkerAgent>())
            .UsingGraphWorkflows()
            .BuildServiceProvider(config)
            .GetRequiredService<IGraphWorkflowRunner>();
    }

    private static IGraphWorkflowRunner BuildAgentThrowsFactory(bool isRequired)
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((messages, opts, _) =>
            {
                var systemText = messages.FirstOrDefault(m => m.Role == ChatRole.System)?.Text ?? "";
                var optionsInstructions = opts?.Instructions ?? "";
                var allInstructions = systemText + " " + optionsInstructions;
                if (allInstructions.Contains("throws-worker", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Simulated agent explosion");
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
            });

        var syringe = new Syringe().UsingReflection();

        if (isRequired)
        {
            return syringe
                .UsingAgentFramework(af => af
                    .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                    .AddAgent<AgentThrowsReqEntryAgent>()
                    .AddAgent<AgentThrowsReqWorkerAgent>()
                    .AddAgent<AgentThrowsReqOkAgent>())
                .UsingGraphWorkflows()
                .BuildServiceProvider(config)
                .GetRequiredService<IGraphWorkflowRunner>();
        }

        return syringe
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgent<AgentThrowsOptEntryAgent>()
                .AddAgent<AgentThrowsOptWorkerAgent>()
                .AddAgent<AgentThrowsOptOkAgent>()
                .AddAgent<AgentThrowsOptSinkAgent>())
            .UsingGraphWorkflows()
            .BuildServiceProvider(config)
            .GetRequiredService<IGraphWorkflowRunner>();
    }

}

/// <summary>
/// A chat client that always throws an exception, used to simulate agent failures.
/// </summary>
internal sealed class FailingChatClient : IChatClient
{
    private readonly string _agentName;

    public FailingChatClient(string agentName) => _agentName = agentName;

    public ChatClientMetadata Metadata => new();

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"Simulated failure in {_agentName}");

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"Simulated failure in {_agentName}");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}

// ---------------------------------------------------------------------------
// Test progress reporter
// ---------------------------------------------------------------------------

internal sealed class TestProgressReporter : IProgressReporter
{
    private readonly List<IProgressEvent> _events;
    private long _sequence;

    public TestProgressReporter(List<IProgressEvent> events)
    {
        _events = events;
    }

    public string WorkflowId => "test-workflow";
    public string? AgentId => null;
    public int Depth => 0;

    public void Report(IProgressEvent progressEvent)
    {
        lock (_events)
        {
            _events.Add(progressEvent);
        }
    }

    public IProgressReporter CreateChild(string agentId) => this;

    public long NextSequence() => Interlocked.Increment(ref _sequence);
}

// ---------------------------------------------------------------------------
// 1. Condition routing test agents
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Entry for condition routing graph.")]
[AgentGraphEntry("cond-routing-graph", RoutingMode = GraphRoutingMode.Deterministic)]
[AgentGraphEdge("cond-routing-graph", typeof(CondGatedWorkerAgent), Condition = nameof(ShouldGate))]
[AgentGraphEdge("cond-routing-graph", typeof(CondAlwaysWorkerAgent))]
internal sealed class CondEntryAgent
{
    /// <summary>
    /// Condition predicate: returns true when the output contains "GATE_OPEN".
    /// </summary>
    public static bool ShouldGate(object? upstreamOutput)
    {
        if (upstreamOutput is string s)
        {
            return s.Contains("GATE_OPEN", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}

// Temporary debug: add a separate test to see what agents run
// TODO: remove after debugging

[NeedlrAiAgent(Instructions = "Gated worker, only runs when condition passes.")]
[AgentGraphEdge("cond-routing-graph", typeof(CondSinkAgent))]
internal sealed class CondGatedWorkerAgent { }

[NeedlrAiAgent(Instructions = "Always-runs worker (unconditional edge).")]
[AgentGraphEdge("cond-routing-graph", typeof(CondSinkAgent))]
internal sealed class CondAlwaysWorkerAgent { }

[NeedlrAiAgent(Instructions = "Sink for condition routing graph.")]
[AgentGraphNode("cond-routing-graph", JoinMode = GraphJoinMode.WaitAny)]
internal sealed class CondSinkAgent { }

// ---------------------------------------------------------------------------
// 2. IsRequired failure propagation test agents
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Entry for required-fail graph.")]
[AgentGraphEntry("req-fail-graph")]
[AgentGraphEdge("req-fail-graph", typeof(ReqFailWorkerAgent), IsRequired = true)]
[AgentGraphEdge("req-fail-graph", typeof(ReqOkWorkerAgent), IsRequired = true)]
internal sealed class ReqFailEntryAgent { }

[NeedlrAiAgent(Instructions = "This worker will fail.")]
[AgentGraphEdge("req-fail-graph", typeof(ReqFailSinkAgent))]
internal sealed class ReqFailWorkerAgent { }

[NeedlrAiAgent(Instructions = "This required-ok worker will succeed.")]
[AgentGraphEdge("req-fail-graph", typeof(ReqFailSinkAgent))]
internal sealed class ReqOkWorkerAgent { }

[NeedlrAiAgent(Instructions = "Sink for required-fail graph.")]
[AgentGraphNode("req-fail-graph", JoinMode = GraphJoinMode.WaitAny)]
internal sealed class ReqFailSinkAgent { }

// Optional fail agents
[NeedlrAiAgent(Instructions = "Entry for optional-fail graph.")]
[AgentGraphEntry("opt-fail-graph")]
[AgentGraphEdge("opt-fail-graph", typeof(OptFailWorkerAgent), IsRequired = false)]
[AgentGraphEdge("opt-fail-graph", typeof(OptOkWorkerAgent), IsRequired = true)]
internal sealed class OptFailEntryAgent { }

[NeedlrAiAgent(Instructions = "This is the optional-fail-worker that will fail.")]
[AgentGraphEdge("opt-fail-graph", typeof(OptFailSinkAgent))]
internal sealed class OptFailWorkerAgent { }

[NeedlrAiAgent(Instructions = "This opt-ok worker will succeed.")]
[AgentGraphEdge("opt-fail-graph", typeof(OptFailSinkAgent))]
internal sealed class OptOkWorkerAgent { }

[NeedlrAiAgent(Instructions = "Sink for optional-fail graph.")]
[AgentGraphNode("opt-fail-graph", JoinMode = GraphJoinMode.WaitAny)]
internal sealed class OptFailSinkAgent { }

// Mixed fail agents
[NeedlrAiAgent(Instructions = "Entry for mixed-fail graph.")]
[AgentGraphEntry("mix-fail-graph")]
[AgentGraphEdge("mix-fail-graph", typeof(MixReqFailWorkerAgent), IsRequired = true)]
[AgentGraphEdge("mix-fail-graph", typeof(MixOptOkWorkerAgent), IsRequired = false)]
internal sealed class MixReqFailEntryAgent { }

[NeedlrAiAgent(Instructions = "Required worker that will fail.")]
[AgentGraphEdge("mix-fail-graph", typeof(MixReqFailSinkAgent))]
internal sealed class MixReqFailWorkerAgent { }

[NeedlrAiAgent(Instructions = "Optional worker that succeeds.")]
[AgentGraphEdge("mix-fail-graph", typeof(MixReqFailSinkAgent))]
internal sealed class MixOptOkWorkerAgent { }

[NeedlrAiAgent(Instructions = "Sink for mixed-fail graph.")]
[AgentGraphNode("mix-fail-graph", JoinMode = GraphJoinMode.WaitAny)]
internal sealed class MixReqFailSinkAgent { }

// ---------------------------------------------------------------------------
// 3. Reducer test agents
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Entry for reducer graph.")]
[AgentGraphEntry("reducer-graph")]
[AgentGraphEdge("reducer-graph", typeof(ReducerWorkerAAgent))]
[AgentGraphEdge("reducer-graph", typeof(ReducerWorkerBAgent))]
internal sealed class ReducerEntryAgent { }

[NeedlrAiAgent(Instructions = "Worker A for reducer graph.")]
[AgentGraphEdge("reducer-graph", typeof(ReducerSinkAgent))]
internal sealed class ReducerWorkerAAgent { }

[NeedlrAiAgent(Instructions = "Worker B for reducer graph.")]
[AgentGraphEdge("reducer-graph", typeof(ReducerSinkAgent))]
internal sealed class ReducerWorkerBAgent { }

[NeedlrAiAgent(Instructions = "Sink for reducer graph.")]
[AgentGraphNode("reducer-graph", JoinMode = GraphJoinMode.WaitAll)]
[AgentGraphEdge("reducer-graph", typeof(ReducerTerminalAgent))]
internal sealed class ReducerSinkAgent { }

[NeedlrAiAgent(Instructions = "Terminal for reducer graph.")]
[AgentGraphNode("reducer-graph", JoinMode = GraphJoinMode.WaitAny)]
internal sealed class ReducerTerminalAgent { }

[AgentGraphReducer("reducer-graph", ReducerMethod = nameof(Merge))]
internal static class TestReducer
{
    public static int CallCount;
    public static IReadOnlyList<string>? ReceivedInputs;

    public static string Merge(IReadOnlyList<string> branchOutputs)
    {
        Interlocked.Increment(ref CallCount);
        ReceivedInputs = branchOutputs;
        return "REDUCED:" + string.Join("|", branchOutputs);
    }
}

// ---------------------------------------------------------------------------
// 4. WaitAny timing test agents
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Entry for WaitAny timing graph.")]
[AgentGraphEntry("waitany-timing-graph")]
[AgentGraphEdge("waitany-timing-graph", typeof(FastTimingWorkerAgent))]
[AgentGraphEdge("waitany-timing-graph", typeof(SlowTimingWorkerAgent))]
internal sealed class WaitAnyTimingEntryAgent { }

[NeedlrAiAgent(Instructions = "Fast worker (returns immediately).")]
[AgentGraphEdge("waitany-timing-graph", typeof(WaitAnySinkTimingAgent))]
internal sealed class FastTimingWorkerAgent { }

[NeedlrAiAgent(Instructions = "Slow worker (delays 2 seconds).")]
[AgentGraphEdge("waitany-timing-graph", typeof(WaitAnySinkTimingAgent))]
internal sealed class SlowTimingWorkerAgent { }

[NeedlrAiAgent(Instructions = "Sink with WaitAny for timing test.")]
[AgentGraphNode("waitany-timing-graph", JoinMode = GraphJoinMode.WaitAny)]
internal sealed class WaitAnySinkTimingAgent { }

// ---------------------------------------------------------------------------
// 5. WaitAny events test agents
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Entry for WaitAny events graph.")]
[AgentGraphEntry("waitany-events-graph")]
[AgentGraphEdge("waitany-events-graph", typeof(WaitAnyEventsWorkerAgent))]
internal sealed class WaitAnyEventsEntryAgent { }

[NeedlrAiAgent(Instructions = "Worker for WaitAny events graph.")]
[AgentGraphEdge("waitany-events-graph", typeof(WaitAnyEventsSinkAgent))]
internal sealed class WaitAnyEventsWorkerAgent { }

[NeedlrAiAgent(Instructions = "Sink for WaitAny events graph.")]
[AgentGraphNode("waitany-events-graph", JoinMode = GraphJoinMode.WaitAny)]
internal sealed class WaitAnyEventsSinkAgent { }

// ---------------------------------------------------------------------------
// 6. FirstMatching routing mode test agents
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Entry for first-matching graph.")]
[AgentGraphEntry("first-matching-graph", RoutingMode = GraphRoutingMode.FirstMatching)]
[AgentGraphEdge("first-matching-graph", typeof(FirstMatchWorkerAAgent), Condition = nameof(MatchAll))]
[AgentGraphEdge("first-matching-graph", typeof(FirstMatchWorkerBAgent), Condition = nameof(MatchAll))]
internal sealed class FirstMatchEntryAgent
{
    public static bool MatchAll(object? _) => true;
}

[NeedlrAiAgent(Instructions = "Worker A for first-matching graph.")]
[AgentGraphEdge("first-matching-graph", typeof(FirstMatchSinkAgent))]
internal sealed class FirstMatchWorkerAAgent { }

[NeedlrAiAgent(Instructions = "Worker B for first-matching graph.")]
[AgentGraphEdge("first-matching-graph", typeof(FirstMatchSinkAgent))]
internal sealed class FirstMatchWorkerBAgent { }

[NeedlrAiAgent(Instructions = "Sink for first-matching graph.")]
[AgentGraphNode("first-matching-graph", JoinMode = GraphJoinMode.WaitAny)]
internal sealed class FirstMatchSinkAgent { }

// ---------------------------------------------------------------------------
// ExclusiveChoice routing mode test agents
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Entry for exclusive-choice graph.")]
[AgentGraphEntry("exclusive-choice-graph", RoutingMode = GraphRoutingMode.ExclusiveChoice)]
[AgentGraphEdge("exclusive-choice-graph", typeof(ExclusiveWorkerAAgent), Condition = nameof(MatchA))]
[AgentGraphEdge("exclusive-choice-graph", typeof(ExclusiveWorkerBAgent), Condition = nameof(MatchB))]
internal sealed class ExclusiveEntryAgent
{
    public static bool MatchA(object? input) =>
        input is string s && (s.Contains("MATCH_A", StringComparison.OrdinalIgnoreCase) ||
                              s.Contains("MATCH_BOTH", StringComparison.OrdinalIgnoreCase));

    public static bool MatchB(object? input) =>
        input is string s && (s.Contains("MATCH_B", StringComparison.OrdinalIgnoreCase) ||
                              s.Contains("MATCH_BOTH", StringComparison.OrdinalIgnoreCase));
}

[NeedlrAiAgent(Instructions = "Worker A for exclusive-choice graph.")]
internal sealed class ExclusiveWorkerAAgent { }

[NeedlrAiAgent(Instructions = "Worker B for exclusive-choice graph.")]
internal sealed class ExclusiveWorkerBAgent { }

[NeedlrAiAgent(Instructions = "Sink for exclusive-choice graph.")]
[AgentGraphNode("exclusive-choice-graph", JoinMode = GraphJoinMode.WaitAny)]
internal sealed class ExclusiveSinkAgent { }

// ---------------------------------------------------------------------------
// LlmChoice routing mode test agents
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Entry for LLM choice graph.")]
[AgentGraphEntry("llm-choice-graph", RoutingMode = GraphRoutingMode.LlmChoice)]
[AgentGraphEdge("llm-choice-graph", typeof(LlmChoiceWorkerAAgent), Condition = "Do web search")]
[AgentGraphEdge("llm-choice-graph", typeof(LlmChoiceWorkerBAgent), Condition = "Summarize")]
internal sealed class LlmChoiceEntryAgent { }

[NeedlrAiAgent(Instructions = "Worker A for LLM choice graph.")]
internal sealed class LlmChoiceWorkerAAgent { }

[NeedlrAiAgent(Instructions = "Worker B for LLM choice graph.")]
internal sealed class LlmChoiceWorkerBAgent { }

// ---------------------------------------------------------------------------
// LlmChoice number-based routing test agents
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Entry for LLM choice number routing graph.")]
[AgentGraphEntry("llm-choice-number-graph", RoutingMode = GraphRoutingMode.LlmChoice)]
[AgentGraphEdge("llm-choice-number-graph", typeof(LlmChoiceNumberWorkerAAgent), Condition = "web-research")]
[AgentGraphEdge("llm-choice-number-graph", typeof(LlmChoiceNumberWorkerBAgent), Condition = "web-analysis")]
internal sealed class LlmChoiceNumberEntryAgent { }

[NeedlrAiAgent(Instructions = "Worker A for LLM choice number routing graph.")]
internal sealed class LlmChoiceNumberWorkerAAgent { }

[NeedlrAiAgent(Instructions = "Worker B for LLM choice number routing graph.")]
internal sealed class LlmChoiceNumberWorkerBAgent { }

// ---------------------------------------------------------------------------
// 7. NodeRoutingMode override test agents
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Entry for node override graph.")]
[AgentGraphEntry("node-override-graph", RoutingMode = GraphRoutingMode.Deterministic)]
[AgentGraphEdge("node-override-graph", typeof(NodeOverrideWorkerAAgent),
    Condition = nameof(AlwaysTrue), NodeRoutingMode = GraphRoutingMode.FirstMatching)]
[AgentGraphEdge("node-override-graph", typeof(NodeOverrideWorkerBAgent),
    Condition = nameof(AlwaysTrue))]
internal sealed class NodeOverrideEntryAgent
{
    public static bool AlwaysTrue(object? _) => true;
}

[NeedlrAiAgent(Instructions = "Worker A for node override graph.")]
[AgentGraphEdge("node-override-graph", typeof(NodeOverrideSinkAgent))]
internal sealed class NodeOverrideWorkerAAgent { }

[NeedlrAiAgent(Instructions = "Worker B for node override graph.")]
[AgentGraphEdge("node-override-graph", typeof(NodeOverrideSinkAgent))]
internal sealed class NodeOverrideWorkerBAgent { }

[NeedlrAiAgent(Instructions = "Sink for node override graph.")]
[AgentGraphNode("node-override-graph", JoinMode = GraphJoinMode.WaitAny)]
internal sealed class NodeOverrideSinkAgent { }

// ---------------------------------------------------------------------------
// Fan-in without reducer test agents (existing behavior verification)
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Entry for fan-in without reducer.")]
[AgentGraphEntry("wf-waitany-fanin")]
[AgentGraphEdge("wf-waitany-fanin", typeof(FanInWorkerAAgent))]
[AgentGraphEdge("wf-waitany-fanin", typeof(FanInWorkerBAgent))]
internal sealed class FanInEntryAgent { }

[NeedlrAiAgent(Instructions = "Worker A for fan-in.")]
[AgentGraphEdge("wf-waitany-fanin", typeof(FanInSinkAgent))]
internal sealed class FanInWorkerAAgent { }

[NeedlrAiAgent(Instructions = "Worker B for fan-in.")]
[AgentGraphEdge("wf-waitany-fanin", typeof(FanInSinkAgent))]
internal sealed class FanInWorkerBAgent { }

[NeedlrAiAgent(Instructions = "Sink for fan-in.")]
[AgentGraphNode("wf-waitany-fanin", JoinMode = GraphJoinMode.WaitAny)]
internal sealed class FanInSinkAgent { }

// ---------------------------------------------------------------------------
// WaitAny cancellation test agents
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Entry for WaitAny cancellation graph.")]
[AgentGraphEntry("waitany-cancel-graph")]
[AgentGraphEdge("waitany-cancel-graph", typeof(FastCancelWorkerAgent))]
[AgentGraphEdge("waitany-cancel-graph", typeof(SlowCancelWorkerAgent))]
internal sealed class WaitAnyCancelEntryAgent { }

[NeedlrAiAgent(Instructions = "This is the fast-cancel-worker that returns quickly.")]
[AgentGraphEdge("waitany-cancel-graph", typeof(WaitAnyCancelSinkAgent))]
internal sealed class FastCancelWorkerAgent { }

[NeedlrAiAgent(Instructions = "This is the slow-cancel-worker that delays.")]
[AgentGraphEdge("waitany-cancel-graph", typeof(WaitAnyCancelSinkAgent))]
internal sealed class SlowCancelWorkerAgent { }

[NeedlrAiAgent(Instructions = "Sink with WaitAny for cancellation test.")]
[AgentGraphNode("waitany-cancel-graph", JoinMode = GraphJoinMode.WaitAny)]
internal sealed class WaitAnyCancelSinkAgent { }

// ---------------------------------------------------------------------------
// 8. Concurrent fan-in reducer test agents
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Entry for concurrent reducer graph.")]
[AgentGraphEntry("concurrent-reducer-graph")]
[AgentGraphEdge("concurrent-reducer-graph", typeof(ConcReducerWorkerAAgent))]
[AgentGraphEdge("concurrent-reducer-graph", typeof(ConcReducerWorkerBAgent))]
internal sealed class ConcReducerEntryAgent { }

[NeedlrAiAgent(Instructions = "This is the worker-a for concurrent reducer.")]
[AgentGraphEdge("concurrent-reducer-graph", typeof(ConcReducerSinkAgent))]
internal sealed class ConcReducerWorkerAAgent { }

[NeedlrAiAgent(Instructions = "This is the worker-b for concurrent reducer.")]
[AgentGraphEdge("concurrent-reducer-graph", typeof(ConcReducerSinkAgent))]
internal sealed class ConcReducerWorkerBAgent { }

[NeedlrAiAgent(Instructions = "Sink for concurrent reducer graph.")]
[AgentGraphNode("concurrent-reducer-graph", JoinMode = GraphJoinMode.WaitAll)]
internal sealed class ConcReducerSinkAgent { }

[AgentGraphReducer("concurrent-reducer-graph", ReducerMethod = nameof(Merge))]
internal static class ConcurrentReducerValidator
{
    private static int _callCount;
    private static IReadOnlyList<string>? _lastReceivedInputs;
    private static readonly object _lock = new();

    public static int CallCount => _callCount;
    public static IReadOnlyList<string>? LastReceivedInputs
    {
        get { lock (_lock) return _lastReceivedInputs; }
    }

    public static void Reset()
    {
        Interlocked.Exchange(ref _callCount, 0);
        lock (_lock) _lastReceivedInputs = null;
    }

    public static string Merge(IReadOnlyList<string> branchOutputs)
    {
        Interlocked.Increment(ref _callCount);
        lock (_lock) _lastReceivedInputs = branchOutputs.ToList().AsReadOnly();
        return "REDUCED:" + string.Join("|", branchOutputs);
    }
}

// ---------------------------------------------------------------------------
// 9a. Throwing condition test agents
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Entry for throwing condition graph.")]
[AgentGraphEntry("throwing-condition-graph", RoutingMode = GraphRoutingMode.Deterministic)]
[AgentGraphEdge("throwing-condition-graph", typeof(ThrowCondWorkerAgent), Condition = nameof(ThrowingCondition))]
internal sealed class ThrowCondEntryAgent
{
    public static bool ThrowingCondition(object? _) =>
        throw new InvalidOperationException("Condition method explosion");
}

[NeedlrAiAgent(Instructions = "Worker for throwing condition graph.")]
internal sealed class ThrowCondWorkerAgent { }

// ---------------------------------------------------------------------------
// 9b. Throwing reducer test agents
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Entry for throwing reducer graph.")]
[AgentGraphEntry("throwing-reducer-graph")]
[AgentGraphEdge("throwing-reducer-graph", typeof(ThrowReducerWorkerAAgent))]
[AgentGraphEdge("throwing-reducer-graph", typeof(ThrowReducerWorkerBAgent))]
internal sealed class ThrowReducerEntryAgent { }

[NeedlrAiAgent(Instructions = "Worker A for throwing reducer graph.")]
[AgentGraphEdge("throwing-reducer-graph", typeof(ThrowReducerSinkAgent))]
internal sealed class ThrowReducerWorkerAAgent { }

[NeedlrAiAgent(Instructions = "Worker B for throwing reducer graph.")]
[AgentGraphEdge("throwing-reducer-graph", typeof(ThrowReducerSinkAgent))]
internal sealed class ThrowReducerWorkerBAgent { }

[NeedlrAiAgent(Instructions = "Sink for throwing reducer graph.")]
[AgentGraphNode("throwing-reducer-graph", JoinMode = GraphJoinMode.WaitAll)]
internal sealed class ThrowReducerSinkAgent { }

[AgentGraphReducer("throwing-reducer-graph", ReducerMethod = nameof(Explode))]
internal static class ThrowingReducer
{
    private static int _callCount;
    public static int CallCount => _callCount;

    public static void Reset() => Interlocked.Exchange(ref _callCount, 0);

    public static string Explode(IReadOnlyList<string> branchOutputs)
    {
        Interlocked.Increment(ref _callCount);
        throw new InvalidOperationException("Reducer explosion");
    }
}

// ---------------------------------------------------------------------------
// 9c. AllMatching routing mode test agents
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Entry for all-matching graph.")]
[AgentGraphEntry("all-matching-graph", RoutingMode = GraphRoutingMode.AllMatching)]
[AgentGraphEdge("all-matching-graph", typeof(AllMatchWorkerAAgent), Condition = nameof(MatchA))]
[AgentGraphEdge("all-matching-graph", typeof(AllMatchWorkerBAgent), Condition = nameof(MatchB))]
[AgentGraphEdge("all-matching-graph", typeof(AllMatchWorkerCAgent), Condition = nameof(MatchC))]
internal sealed class AllMatchEntryAgent
{
    public static bool MatchA(object? input) =>
        input is string s && s.Contains("MATCH_A", StringComparison.OrdinalIgnoreCase);

    public static bool MatchB(object? input) =>
        input is string s &&
        (s.Contains("MATCH_B", StringComparison.OrdinalIgnoreCase) ||
         s.Contains("MATCH_A_AND_B", StringComparison.OrdinalIgnoreCase));

    public static bool MatchC(object? input) =>
        input is string s && s.Contains("MATCH_C", StringComparison.OrdinalIgnoreCase);
}

[NeedlrAiAgent(Instructions = "Worker A for all-matching graph.")]
[AgentGraphEdge("all-matching-graph", typeof(AllMatchSinkAgent))]
internal sealed class AllMatchWorkerAAgent { }

[NeedlrAiAgent(Instructions = "Worker B for all-matching graph.")]
[AgentGraphEdge("all-matching-graph", typeof(AllMatchSinkAgent))]
internal sealed class AllMatchWorkerBAgent { }

[NeedlrAiAgent(Instructions = "Worker C for all-matching graph.")]
[AgentGraphEdge("all-matching-graph", typeof(AllMatchSinkAgent))]
internal sealed class AllMatchWorkerCAgent { }

[NeedlrAiAgent(Instructions = "Sink for all-matching graph.")]
[AgentGraphNode("all-matching-graph", JoinMode = GraphJoinMode.WaitAny)]
internal sealed class AllMatchSinkAgent { }

// ---------------------------------------------------------------------------
// 9d. No matching edge in Deterministic mode test agents
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Entry for no-matching-edge graph.")]
[AgentGraphEntry("no-matching-edge-graph", RoutingMode = GraphRoutingMode.Deterministic)]
[AgentGraphEdge("no-matching-edge-graph", typeof(NoMatchWorkerAgent), Condition = nameof(NeverMatch))]
internal sealed class NoMatchEntryAgent
{
    public static bool NeverMatch(object? _) => false;
}

[NeedlrAiAgent(Instructions = "Worker for no-matching-edge graph.")]
internal sealed class NoMatchWorkerAgent { }

// ---------------------------------------------------------------------------
// 9e. Agent throws test agents (required vs optional)
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Entry for agent-throws required graph.")]
[AgentGraphEntry("agent-throws-req-graph")]
[AgentGraphEdge("agent-throws-req-graph", typeof(AgentThrowsReqWorkerAgent), IsRequired = true)]
[AgentGraphEdge("agent-throws-req-graph", typeof(AgentThrowsReqOkAgent), IsRequired = true)]
internal sealed class AgentThrowsReqEntryAgent { }

[NeedlrAiAgent(Instructions = "This is the throws-worker that will explode.")]
internal sealed class AgentThrowsReqWorkerAgent { }

[NeedlrAiAgent(Instructions = "Ok worker for agent-throws required graph.")]
internal sealed class AgentThrowsReqOkAgent { }

[NeedlrAiAgent(Instructions = "Entry for agent-throws optional graph.")]
[AgentGraphEntry("agent-throws-opt-graph")]
[AgentGraphEdge("agent-throws-opt-graph", typeof(AgentThrowsOptWorkerAgent), IsRequired = false)]
[AgentGraphEdge("agent-throws-opt-graph", typeof(AgentThrowsOptOkAgent), IsRequired = true)]
internal sealed class AgentThrowsOptEntryAgent { }

[NeedlrAiAgent(Instructions = "This is the throws-worker that will explode (optional).")]
[AgentGraphEdge("agent-throws-opt-graph", typeof(AgentThrowsOptSinkAgent))]
internal sealed class AgentThrowsOptWorkerAgent { }

[NeedlrAiAgent(Instructions = "Ok worker for agent-throws optional graph.")]
[AgentGraphEdge("agent-throws-opt-graph", typeof(AgentThrowsOptSinkAgent))]
internal sealed class AgentThrowsOptOkAgent { }

[NeedlrAiAgent(Instructions = "Sink for agent-throws optional graph.")]
[AgentGraphNode("agent-throws-opt-graph", JoinMode = GraphJoinMode.WaitAny)]
internal sealed class AgentThrowsOptSinkAgent { }
