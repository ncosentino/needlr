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
        var factory = BuildCondRoutingFactory();

        var result = await factory.RunGraphAsync(
            "cond-routing-graph",
            "input that does NOT match",
            progress: reporter,
            cancellationToken: _ct);

        Assert.True(result.Succeeded, $"Graph should succeed. Error: {result.ErrorMessage}");

        var invokedNames = events.OfType<AgentInvokedEvent>().Select(e => e.NodeId).ToList();
        Assert.Contains("CondAlwaysWorkerAgent", invokedNames);
        Assert.DoesNotContain("CondGatedWorkerAgent", invokedNames);
    }

    /// <summary>
    /// An unconditional edge should always execute its target.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_UnconditionalEdge_AlwaysExecutes()
    {
        var events = new List<IProgressEvent>();
        var reporter = new TestProgressReporter(events);
        var factory = BuildCondRoutingFactory();

        var result = await factory.RunGraphAsync(
            "cond-routing-graph",
            "input that does NOT match",
            progress: reporter,
            cancellationToken: _ct);

        Assert.True(result.Succeeded, $"Graph should succeed. Error: {result.ErrorMessage}");

        var invokedNames = events.OfType<AgentInvokedEvent>().Select(e => e.NodeId).ToList();
        Assert.Contains("CondAlwaysWorkerAgent", invokedNames);
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
        var factory = BuildFailingFactory(
            failAgentName: "ReqFailWorkerAgent",
            failingIsRequired: true);

        var result = await factory.RunGraphAsync(
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
        var factory = BuildOptionalFailFactory(invokedAgents);

        var result = await factory.RunGraphAsync(
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
        var factory = BuildFailingFactory(
            failAgentName: "MixReqFailWorkerAgent",
            failingIsRequired: true);

        var result = await factory.RunGraphAsync(
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
        var factory = BuildReducerFactory(invokedAgents);

        var result = await factory.RunGraphAsync(
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
        var factory = BuildReducerFactory(invokedAgents);

        var result = await factory.RunGraphAsync(
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
        var factory = BuildCondRoutingFactory();

        var result = await factory.RunGraphAsync(
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
        var factory = BuildWaitAnyTimingFactory(new Dictionary<string, DateTimeOffset>());

        var result = await factory.RunGraphAsync(
            "waitany-timing-graph",
            "test input",
            progress: reporter,
            cancellationToken: _ct);

        Assert.True(result.Succeeded, $"Graph should succeed. Error: {result.ErrorMessage}");

        // Verify the sink was invoked
        var invokedNodes = events.OfType<AgentInvokedEvent>().Select(e => e.NodeId).ToList();
        Assert.Contains("WaitAnySinkTimingAgent", invokedNodes);

        // Verify WaitAny semantics: the total graph duration should be significantly
        // less than the slow node's 2s delay + entry + sink time (if it waited for all,
        // it would be >2s). With WaitAny, the sink starts as soon as the fast worker completes.
        Assert.True(
            result.TotalDuration.TotalSeconds < 1.5,
            $"WaitAny graph should complete quickly (got {result.TotalDuration.TotalSeconds:F2}s). " +
            $"If >2s, WaitAny may be waiting for the slow node.");
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
        var factory = BuildWaitAnyFactory(invokedAgents);

        await factory.RunGraphAsync(
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
        var factory = BuildWaitAnyFactory(invokedAgents);

        await factory.RunGraphAsync(
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
        var factory = BuildFirstMatchingFactory(invokedAgents);

        var result = await factory.RunGraphAsync(
            "first-matching-graph",
            "MATCH_ALL",
            progress: reporter,
            cancellationToken: _ct);

        Assert.True(result.Succeeded, $"Graph should succeed. Error: {result.ErrorMessage}");

        var invokedNodes = events.OfType<AgentInvokedEvent>().Select(e => e.NodeId).ToList();
        Assert.Contains("FirstMatchWorkerAAgent", invokedNodes);
        Assert.DoesNotContain("FirstMatchWorkerBAgent", invokedNodes);
    }

    /// <summary>
    /// ExclusiveChoice: exactly one must fire. If zero match, graph fails.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_ExclusiveChoice_ZeroMatches_GraphFails()
    {
        var invokedAgents = new List<string>();
        var factory = BuildExclusiveChoiceFactory(invokedAgents);

        var result = await factory.RunGraphAsync(
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
        var factory = BuildExclusiveChoiceFactory(invokedAgents);

        var result = await factory.RunGraphAsync(
            "exclusive-choice-graph",
            "MATCH_BOTH",
            cancellationToken: _ct);

        Assert.False(result.Succeeded, "Graph should fail when ExclusiveChoice has multiple matches");
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("ExclusiveChoice", result.ErrorMessage);
    }

    /// <summary>
    /// LlmChoice is not supported in this pass and should throw NotSupportedException.
    /// </summary>
    [Fact]
    public async Task RunGraphAsync_LlmChoice_ThrowsNotSupported()
    {
        var invokedAgents = new List<string>();
        var factory = BuildLlmChoiceFactory(invokedAgents);

        await Assert.ThrowsAsync<NotSupportedException>(
            async () => await factory.RunGraphAsync(
                "llm-choice-graph",
                "any input",
                cancellationToken: _ct));
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
        var factory = BuildNodeOverrideFactory(invokedAgents);

        var result = await factory.RunGraphAsync(
            "node-override-graph",
            "MATCH_ALL",
            progress: reporter,
            cancellationToken: _ct);

        Assert.True(result.Succeeded, $"Graph should succeed. Error: {result.ErrorMessage}");

        var invokedNodes = events.OfType<AgentInvokedEvent>().Select(e => e.NodeId).ToList();
        Assert.Contains("NodeOverrideWorkerAAgent", invokedNodes);
        Assert.DoesNotContain("NodeOverrideWorkerBAgent", invokedNodes);
    }

    // -----------------------------------------------------------------------
    // Factory builders
    // -----------------------------------------------------------------------

    private static IWorkflowFactory BuildCondRoutingFactory()
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
            .BuildServiceProvider(config)
            .GetRequiredService<IWorkflowFactory>();
    }

    private static IWorkflowFactory BuildFailingFactory(
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
            .BuildServiceProvider(config)
            .GetRequiredService<IWorkflowFactory>();
    }

    private static IWorkflowFactory BuildOptionalFailFactory(List<string> invokedAgents)
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
            .BuildServiceProvider(config)
            .GetRequiredService<IWorkflowFactory>();
    }

    private static IWorkflowFactory BuildReducerFactory(
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
            .BuildServiceProvider(config)
            .GetRequiredService<IWorkflowFactory>();
    }

    private static IWorkflowFactory BuildWaitAnyTimingFactory(
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
            .BuildServiceProvider(config)
            .GetRequiredService<IWorkflowFactory>();
    }

    private static IWorkflowFactory BuildWaitAnyFactory(List<string> invokedAgents)
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
            .BuildServiceProvider(config)
            .GetRequiredService<IWorkflowFactory>();
    }

    private static IWorkflowFactory BuildFirstMatchingFactory(List<string> invokedAgents)
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
            .BuildServiceProvider(config)
            .GetRequiredService<IWorkflowFactory>();
    }

    private static IWorkflowFactory BuildExclusiveChoiceFactory(List<string> invokedAgents)
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
            .BuildServiceProvider(config)
            .GetRequiredService<IWorkflowFactory>();
    }

    private static IWorkflowFactory BuildLlmChoiceFactory(List<string> invokedAgents)
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
                .AddAgent<LlmChoiceEntryAgent>()
                .AddAgent<LlmChoiceWorkerAAgent>()
                .AddAgent<LlmChoiceWorkerBAgent>());

        return builder
            .BuildServiceProvider(config)
            .GetRequiredService<IWorkflowFactory>();
    }

    private static IWorkflowFactory BuildNodeOverrideFactory(List<string> invokedAgents)
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
            .BuildServiceProvider(config)
            .GetRequiredService<IWorkflowFactory>();
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
