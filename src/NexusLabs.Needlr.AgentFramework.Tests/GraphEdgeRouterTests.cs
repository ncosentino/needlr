using NexusLabs.Needlr.AgentFramework.Workflows;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Unit tests for <see cref="GraphEdgeRouter"/> covering condition evaluation
/// and routing mode enforcement.
/// </summary>
public sealed class GraphEdgeRouterTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    // -----------------------------------------------------------------------
    // Test fixture types for EvaluateCondition
    // -----------------------------------------------------------------------

    private sealed class AgentWithValidConditions
    {
        public static bool IsPositive(object? upstream) =>
            upstream is int value && value > 0;

        public static bool IsNegative(object? upstream) =>
            upstream is int value && value < 0;

        public static bool AlwaysTrue(object? upstream) => true;

        public static bool AlwaysFalse(object? upstream) => false;
    }

    private sealed class AgentWithWrongReturnType
    {
        public static string NotABool(object? upstream) => "oops";
    }

    private sealed class AgentWithNoConditionMethods;

    private sealed class AgentWithPrivateCondition
    {
        private static bool SecretCondition(object? upstream) => true;
    }

    // -----------------------------------------------------------------------
    // EvaluateCondition — valid conditions
    // -----------------------------------------------------------------------

    [Fact]
    public void EvaluateCondition_ValidMethod_ReturnsTrue()
    {
        var result = GraphEdgeRouter.EvaluateCondition(
            typeof(AgentWithValidConditions),
            nameof(AgentWithValidConditions.IsPositive),
            42);

        Assert.True(result);
    }

    [Fact]
    public void EvaluateCondition_ValidMethod_ReturnsFalse()
    {
        var result = GraphEdgeRouter.EvaluateCondition(
            typeof(AgentWithValidConditions),
            nameof(AgentWithValidConditions.IsPositive),
            -5);

        Assert.False(result);
    }

    [Fact]
    public void EvaluateCondition_AlwaysTrue_ReturnsTrue()
    {
        var result = GraphEdgeRouter.EvaluateCondition(
            typeof(AgentWithValidConditions),
            nameof(AgentWithValidConditions.AlwaysTrue),
            null);

        Assert.True(result);
    }

    [Fact]
    public void EvaluateCondition_ReceivesUpstreamOutput()
    {
        var result = GraphEdgeRouter.EvaluateCondition(
            typeof(AgentWithValidConditions),
            nameof(AgentWithValidConditions.IsNegative),
            -10);

        Assert.True(result);
    }

    // -----------------------------------------------------------------------
    // EvaluateCondition — missing method
    // -----------------------------------------------------------------------

    [Fact]
    public void EvaluateCondition_MissingMethod_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            GraphEdgeRouter.EvaluateCondition(
                typeof(AgentWithNoConditionMethods),
                "NonExistent",
                null));

        Assert.Contains("NonExistent", ex.Message);
        Assert.Contains("AgentWithNoConditionMethods", ex.Message);
    }

    // -----------------------------------------------------------------------
    // EvaluateCondition — wrong return type
    // -----------------------------------------------------------------------

    [Fact]
    public void EvaluateCondition_WrongReturnType_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            GraphEdgeRouter.EvaluateCondition(
                typeof(AgentWithWrongReturnType),
                nameof(AgentWithWrongReturnType.NotABool),
                null));

        Assert.Contains("NotABool", ex.Message);
    }

    // -----------------------------------------------------------------------
    // EvaluateCondition — private method still accessible
    // -----------------------------------------------------------------------

    [Fact]
    public void EvaluateCondition_PrivateMethod_Succeeds()
    {
        var result = GraphEdgeRouter.EvaluateCondition(
            typeof(AgentWithPrivateCondition),
            "SecretCondition",
            null);

        Assert.True(result);
    }

    // -----------------------------------------------------------------------
    // Shared topology/router builder helpers
    // -----------------------------------------------------------------------

    private static GraphTopology CreateTopology(
        Type sourceType,
        List<GraphEdgeDetail> edges,
        GraphRoutingMode graphRoutingMode = GraphRoutingMode.Deterministic,
        GraphRoutingMode? sourceEffectiveMode = null)
    {
        var effectiveModes = new Dictionary<Type, GraphRoutingMode>();
        if (sourceEffectiveMode.HasValue)
        {
            effectiveModes[sourceType] = sourceEffectiveMode.Value;
        }

        return new GraphTopology(
            sourceType,
            new HashSet<Type>(edges.Select(e => e.Target).Append(sourceType)),
            [],
            [],
            new Dictionary<Type, IReadOnlyList<Type>>(),
            new Dictionary<Type, IReadOnlyList<Type>>(),
            graphRoutingMode,
            new Dictionary<Type, List<GraphEdgeDetail>>
            {
                [sourceType] = edges,
            },
            effectiveModes,
            [],
            null,
            null);
    }

    private static GraphEdgeDetail Edge(
        Type source,
        Type target,
        string? condition = null,
        bool isRequired = true,
        GraphRoutingMode? routingOverride = null) =>
        new(source, target, condition, isRequired, routingOverride);

    // Dummy target types
    private sealed class TargetA;
    private sealed class TargetB;
    private sealed class TargetC;

    // -----------------------------------------------------------------------
    // ResolveOutgoingEdgesAsync — no outgoing edges
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResolveOutgoingEdgesAsync_NoEdges_ReturnsEmpty()
    {
        var router = new GraphEdgeRouter();
        var topology = new GraphTopology(
            typeof(AgentWithValidConditions),
            [typeof(AgentWithValidConditions)],
            [],
            [],
            new Dictionary<Type, IReadOnlyList<Type>>(),
            new Dictionary<Type, IReadOnlyList<Type>>(),
            GraphRoutingMode.Deterministic,
            [],
            [],
            [],
            null,
            null);

        var result = await router.ResolveOutgoingEdgesAsync(
            typeof(AgentWithValidConditions),
            null,
            topology,
            null,
            _ct);

        Assert.Empty(result);
    }

    // -----------------------------------------------------------------------
    // ResolveOutgoingEdgesAsync — Deterministic/AllMatching returns all matching
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResolveOutgoingEdgesAsync_Deterministic_ReturnsAllMatchingEdges()
    {
        var edges = new List<GraphEdgeDetail>
        {
            Edge(typeof(AgentWithValidConditions), typeof(TargetA), nameof(AgentWithValidConditions.AlwaysTrue)),
            Edge(typeof(AgentWithValidConditions), typeof(TargetB), nameof(AgentWithValidConditions.AlwaysFalse)),
            Edge(typeof(AgentWithValidConditions), typeof(TargetC)),
        };
        var topology = CreateTopology(typeof(AgentWithValidConditions), edges, GraphRoutingMode.Deterministic);
        var router = new GraphEdgeRouter();

        var result = await router.ResolveOutgoingEdgesAsync(
            typeof(AgentWithValidConditions),
            null,
            topology,
            null,
            _ct);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, e => e.Target == typeof(TargetA));
        Assert.Contains(result, e => e.Target == typeof(TargetC));
    }

    [Fact]
    public async Task ResolveOutgoingEdgesAsync_AllMatching_ReturnsAllMatchingEdges()
    {
        var edges = new List<GraphEdgeDetail>
        {
            Edge(typeof(AgentWithValidConditions), typeof(TargetA), nameof(AgentWithValidConditions.AlwaysTrue)),
            Edge(typeof(AgentWithValidConditions), typeof(TargetB), nameof(AgentWithValidConditions.AlwaysTrue)),
        };
        var topology = CreateTopology(typeof(AgentWithValidConditions), edges, GraphRoutingMode.AllMatching);
        var router = new GraphEdgeRouter();

        var result = await router.ResolveOutgoingEdgesAsync(
            typeof(AgentWithValidConditions),
            null,
            topology,
            null,
            _ct);

        Assert.Equal(2, result.Count);
    }

    // -----------------------------------------------------------------------
    // ResolveOutgoingEdgesAsync — FirstMatching returns only first
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResolveOutgoingEdgesAsync_FirstMatching_ReturnsOnlyFirst()
    {
        var edges = new List<GraphEdgeDetail>
        {
            Edge(typeof(AgentWithValidConditions), typeof(TargetA), nameof(AgentWithValidConditions.AlwaysTrue)),
            Edge(typeof(AgentWithValidConditions), typeof(TargetB), nameof(AgentWithValidConditions.AlwaysTrue)),
        };
        var topology = CreateTopology(typeof(AgentWithValidConditions), edges, GraphRoutingMode.FirstMatching);
        var router = new GraphEdgeRouter();

        var result = await router.ResolveOutgoingEdgesAsync(
            typeof(AgentWithValidConditions),
            null,
            topology,
            null,
            _ct);

        Assert.Single(result);
        Assert.Equal(typeof(TargetA), result[0].Target);
    }

    [Fact]
    public async Task ResolveOutgoingEdgesAsync_FirstMatching_NoMatch_ReturnsEmpty()
    {
        var edges = new List<GraphEdgeDetail>
        {
            Edge(typeof(AgentWithValidConditions), typeof(TargetA), nameof(AgentWithValidConditions.AlwaysFalse)),
        };
        var topology = CreateTopology(typeof(AgentWithValidConditions), edges, GraphRoutingMode.FirstMatching);
        var router = new GraphEdgeRouter();

        var result = await router.ResolveOutgoingEdgesAsync(
            typeof(AgentWithValidConditions),
            null,
            topology,
            null,
            _ct);

        Assert.Empty(result);
    }

    // -----------------------------------------------------------------------
    // ResolveOutgoingEdgesAsync — ExclusiveChoice
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResolveOutgoingEdgesAsync_ExclusiveChoice_ExactlyOneMatch_ReturnsIt()
    {
        var edges = new List<GraphEdgeDetail>
        {
            Edge(typeof(AgentWithValidConditions), typeof(TargetA), nameof(AgentWithValidConditions.AlwaysTrue)),
            Edge(typeof(AgentWithValidConditions), typeof(TargetB), nameof(AgentWithValidConditions.AlwaysFalse)),
        };
        var topology = CreateTopology(typeof(AgentWithValidConditions), edges, GraphRoutingMode.ExclusiveChoice);
        var router = new GraphEdgeRouter();

        var result = await router.ResolveOutgoingEdgesAsync(
            typeof(AgentWithValidConditions),
            null,
            topology,
            null,
            _ct);

        Assert.Single(result);
        Assert.Equal(typeof(TargetA), result[0].Target);
    }

    [Fact]
    public async Task ResolveOutgoingEdgesAsync_ExclusiveChoice_ZeroMatches_Throws()
    {
        var edges = new List<GraphEdgeDetail>
        {
            Edge(typeof(AgentWithValidConditions), typeof(TargetA), nameof(AgentWithValidConditions.AlwaysFalse)),
            Edge(typeof(AgentWithValidConditions), typeof(TargetB), nameof(AgentWithValidConditions.AlwaysFalse)),
        };
        var topology = CreateTopology(typeof(AgentWithValidConditions), edges, GraphRoutingMode.ExclusiveChoice);
        var router = new GraphEdgeRouter();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            router.ResolveOutgoingEdgesAsync(
                typeof(AgentWithValidConditions),
                null,
                topology,
                null,
                _ct));

        Assert.Contains("ExclusiveChoice", ex.Message);
        Assert.Contains("no edge condition matched", ex.Message);
    }

    [Fact]
    public async Task ResolveOutgoingEdgesAsync_ExclusiveChoice_MultipleMatches_Throws()
    {
        var edges = new List<GraphEdgeDetail>
        {
            Edge(typeof(AgentWithValidConditions), typeof(TargetA), nameof(AgentWithValidConditions.AlwaysTrue)),
            Edge(typeof(AgentWithValidConditions), typeof(TargetB), nameof(AgentWithValidConditions.AlwaysTrue)),
        };
        var topology = CreateTopology(typeof(AgentWithValidConditions), edges, GraphRoutingMode.ExclusiveChoice);
        var router = new GraphEdgeRouter();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            router.ResolveOutgoingEdgesAsync(
                typeof(AgentWithValidConditions),
                null,
                topology,
                null,
                _ct));

        Assert.Contains("ExclusiveChoice", ex.Message);
        Assert.Contains("2 edges matched", ex.Message);
    }

    // -----------------------------------------------------------------------
    // ResolveOutgoingEdgesAsync — unconditional edges always included
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResolveOutgoingEdgesAsync_UnconditionalEdges_AlwaysIncluded()
    {
        var edges = new List<GraphEdgeDetail>
        {
            Edge(typeof(AgentWithValidConditions), typeof(TargetA)),
            Edge(typeof(AgentWithValidConditions), typeof(TargetB)),
        };
        var topology = CreateTopology(typeof(AgentWithValidConditions), edges, GraphRoutingMode.Deterministic);
        var router = new GraphEdgeRouter();

        var result = await router.ResolveOutgoingEdgesAsync(
            typeof(AgentWithValidConditions),
            null,
            topology,
            null,
            _ct);

        Assert.Equal(2, result.Count);
    }

    // -----------------------------------------------------------------------
    // ResolveOutgoingEdgesAsync — mixed conditional + unconditional
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResolveOutgoingEdgesAsync_MixedConditionalAndUnconditional()
    {
        var edges = new List<GraphEdgeDetail>
        {
            Edge(typeof(AgentWithValidConditions), typeof(TargetA), nameof(AgentWithValidConditions.AlwaysFalse)),
            Edge(typeof(AgentWithValidConditions), typeof(TargetB)),
            Edge(typeof(AgentWithValidConditions), typeof(TargetC), nameof(AgentWithValidConditions.AlwaysTrue)),
        };
        var topology = CreateTopology(typeof(AgentWithValidConditions), edges, GraphRoutingMode.Deterministic);
        var router = new GraphEdgeRouter();

        var result = await router.ResolveOutgoingEdgesAsync(
            typeof(AgentWithValidConditions),
            null,
            topology,
            null,
            _ct);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, e => e.Target == typeof(TargetB));
        Assert.Contains(result, e => e.Target == typeof(TargetC));
        Assert.DoesNotContain(result, e => e.Target == typeof(TargetA));
    }

    // -----------------------------------------------------------------------
    // ResolveOutgoingEdgesAsync — uses effective routing mode per node
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResolveOutgoingEdgesAsync_UsesPerNodeEffectiveRoutingMode()
    {
        var edges = new List<GraphEdgeDetail>
        {
            Edge(typeof(AgentWithValidConditions), typeof(TargetA), nameof(AgentWithValidConditions.AlwaysTrue)),
            Edge(typeof(AgentWithValidConditions), typeof(TargetB), nameof(AgentWithValidConditions.AlwaysTrue)),
        };
        // Graph-level is AllMatching, but the node override is FirstMatching
        var topology = CreateTopology(
            typeof(AgentWithValidConditions),
            edges,
            GraphRoutingMode.AllMatching,
            sourceEffectiveMode: GraphRoutingMode.FirstMatching);
        var router = new GraphEdgeRouter();

        var result = await router.ResolveOutgoingEdgesAsync(
            typeof(AgentWithValidConditions),
            null,
            topology,
            null,
            _ct);

        Assert.Single(result);
        Assert.Equal(typeof(TargetA), result[0].Target);
    }

    // -----------------------------------------------------------------------
    // ResolveOutgoingEdgesAsync — upstream output passed to conditions
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResolveOutgoingEdgesAsync_PassesUpstreamOutputToConditions()
    {
        var edges = new List<GraphEdgeDetail>
        {
            Edge(typeof(AgentWithValidConditions), typeof(TargetA), nameof(AgentWithValidConditions.IsPositive)),
            Edge(typeof(AgentWithValidConditions), typeof(TargetB), nameof(AgentWithValidConditions.IsNegative)),
        };
        var topology = CreateTopology(typeof(AgentWithValidConditions), edges, GraphRoutingMode.Deterministic);
        var router = new GraphEdgeRouter();

        var positiveResult = await router.ResolveOutgoingEdgesAsync(
            typeof(AgentWithValidConditions),
            42,
            topology,
            null,
            _ct);

        Assert.Single(positiveResult);
        Assert.Equal(typeof(TargetA), positiveResult[0].Target);

        var negativeResult = await router.ResolveOutgoingEdgesAsync(
            typeof(AgentWithValidConditions),
            -7,
            topology,
            null,
            _ct);

        Assert.Single(negativeResult);
        Assert.Equal(typeof(TargetB), negativeResult[0].Target);
    }

    // -----------------------------------------------------------------------
    // ResolveOutgoingEdgesAsync — LlmChoice without chat client throws
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResolveOutgoingEdgesAsync_LlmChoice_NoChatClient_Throws()
    {
        var edges = new List<GraphEdgeDetail>
        {
            Edge(typeof(AgentWithValidConditions), typeof(TargetA), "route A"),
        };
        var topology = CreateTopology(typeof(AgentWithValidConditions), edges, GraphRoutingMode.LlmChoice);
        var router = new GraphEdgeRouter();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            router.ResolveOutgoingEdgesAsync(
                typeof(AgentWithValidConditions),
                null,
                topology,
                routingChatClient: null,
                _ct));

        Assert.Contains("LlmChoice", ex.Message);
        Assert.Contains("IChatClient", ex.Message);
    }
}
