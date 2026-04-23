using NexusLabs.Needlr.AgentFramework.Workflows;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Unit tests for <see cref="GraphTopology"/> covering property computation
/// and edge-case handling.
/// </summary>
public sealed class GraphTopologyTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static GraphTopology CreateTopology(
        Type? entryType = null,
        HashSet<Type>? allTypes = null,
        Dictionary<Type, GraphJoinMode>? joinModes = null,
        Dictionary<Type, List<Type>>? incomingTypes = null,
        Dictionary<Type, IReadOnlyList<Type>>? inboundEdges = null,
        Dictionary<Type, IReadOnlyList<Type>>? outboundEdges = null,
        GraphRoutingMode graphRoutingMode = GraphRoutingMode.Deterministic,
        Dictionary<Type, List<GraphEdgeDetail>>? outgoingEdgesBySource = null,
        Dictionary<Type, GraphRoutingMode>? effectiveRoutingModes = null,
        Dictionary<(Type, Type), bool>? edgeIsRequired = null,
        Func<IReadOnlyList<string>, string>? reducerFunc = null,
        Type? reducerType = null) =>
        new(
            entryType,
            allTypes ?? [],
            joinModes ?? [],
            incomingTypes ?? [],
            inboundEdges ?? new Dictionary<Type, IReadOnlyList<Type>>(),
            outboundEdges ?? new Dictionary<Type, IReadOnlyList<Type>>(),
            graphRoutingMode,
            outgoingEdgesBySource ?? [],
            effectiveRoutingModes ?? [],
            edgeIsRequired ?? [],
            reducerFunc,
            reducerType);

    // Dummy types used for edge construction
    private sealed class NodeA;
    private sealed class NodeB;
    private sealed class NodeC;

    // -----------------------------------------------------------------------
    // 1. Construction — verify all properties round-trip
    // -----------------------------------------------------------------------

    [Fact]
    public void Constructor_PreservesAllProperties()
    {
        var allTypes = new HashSet<Type> { typeof(NodeA), typeof(NodeB) };
        var joinModes = new Dictionary<Type, GraphJoinMode>
        {
            [typeof(NodeB)] = GraphJoinMode.WaitAll,
        };
        var incomingTypes = new Dictionary<Type, List<Type>>
        {
            [typeof(NodeB)] = [typeof(NodeA)],
        };
        var inboundEdges = new Dictionary<Type, IReadOnlyList<Type>>
        {
            [typeof(NodeB)] = new List<Type> { typeof(NodeA) },
        };
        var outboundEdges = new Dictionary<Type, IReadOnlyList<Type>>
        {
            [typeof(NodeA)] = new List<Type> { typeof(NodeB) },
        };
        var edge = new GraphEdgeDetail(typeof(NodeA), typeof(NodeB), null, true, null);
        var outgoingEdges = new Dictionary<Type, List<GraphEdgeDetail>>
        {
            [typeof(NodeA)] = [edge],
        };
        var effectiveModes = new Dictionary<Type, GraphRoutingMode>
        {
            [typeof(NodeA)] = GraphRoutingMode.AllMatching,
        };
        var edgeRequired = new Dictionary<(Type, Type), bool>
        {
            [(typeof(NodeA), typeof(NodeB))] = true,
        };

        var topology = new GraphTopology(
            typeof(NodeA),
            allTypes,
            joinModes,
            incomingTypes,
            inboundEdges,
            outboundEdges,
            GraphRoutingMode.AllMatching,
            outgoingEdges,
            effectiveModes,
            edgeRequired,
            null,
            null);

        Assert.Equal(typeof(NodeA), topology.EntryType);
        Assert.Same(allTypes, topology.AllTypes);
        Assert.Same(joinModes, topology.JoinModes);
        Assert.Same(incomingTypes, topology.IncomingTypes);
        Assert.Same(inboundEdges, topology.InboundEdges);
        Assert.Same(outboundEdges, topology.OutboundEdges);
        Assert.Equal(GraphRoutingMode.AllMatching, topology.GraphRoutingMode);
        Assert.Same(outgoingEdges, topology.OutgoingEdgesBySource);
        Assert.Same(effectiveModes, topology.EffectiveRoutingModes);
        Assert.Same(edgeRequired, topology.EdgeIsRequired);
        Assert.Null(topology.ReducerFunc);
        Assert.Null(topology.ReducerType);
    }

    // -----------------------------------------------------------------------
    // 2. HasWaitAnyNodes
    // -----------------------------------------------------------------------

    [Fact]
    public void HasWaitAnyNodes_True_WhenWaitAnyPresent()
    {
        var topology = CreateTopology(
            joinModes: new Dictionary<Type, GraphJoinMode>
            {
                [typeof(NodeA)] = GraphJoinMode.WaitAll,
                [typeof(NodeB)] = GraphJoinMode.WaitAny,
            });

        Assert.True(topology.HasWaitAnyNodes);
    }

    [Fact]
    public void HasWaitAnyNodes_False_WhenOnlyWaitAll()
    {
        var topology = CreateTopology(
            joinModes: new Dictionary<Type, GraphJoinMode>
            {
                [typeof(NodeA)] = GraphJoinMode.WaitAll,
            });

        Assert.False(topology.HasWaitAnyNodes);
    }

    [Fact]
    public void HasWaitAnyNodes_False_WhenEmpty()
    {
        var topology = CreateTopology();

        Assert.False(topology.HasWaitAnyNodes);
    }

    // -----------------------------------------------------------------------
    // 3. RequiresNeedlrExecutor — WaitAny
    // -----------------------------------------------------------------------

    [Fact]
    public void RequiresNeedlrExecutor_True_WhenWaitAnyNodes()
    {
        var topology = CreateTopology(
            joinModes: new Dictionary<Type, GraphJoinMode>
            {
                [typeof(NodeA)] = GraphJoinMode.WaitAny,
            });

        Assert.True(topology.RequiresNeedlrExecutor);
    }

    // -----------------------------------------------------------------------
    // 4. RequiresNeedlrExecutor — LlmChoice routing
    // -----------------------------------------------------------------------

    [Fact]
    public void RequiresNeedlrExecutor_True_WhenLlmChoiceGraphRouting()
    {
        var topology = CreateTopology(
            graphRoutingMode: GraphRoutingMode.LlmChoice);

        Assert.True(topology.RequiresNeedlrExecutor);
    }

    [Fact]
    public void RequiresNeedlrExecutor_True_WhenEffectiveRoutingIsLlmChoice()
    {
        var topology = CreateTopology(
            effectiveRoutingModes: new Dictionary<Type, GraphRoutingMode>
            {
                [typeof(NodeA)] = GraphRoutingMode.LlmChoice,
            });

        Assert.True(topology.RequiresNeedlrExecutor);
    }

    // -----------------------------------------------------------------------
    // 5. RequiresNeedlrExecutor — conditions
    // -----------------------------------------------------------------------

    [Fact]
    public void RequiresNeedlrExecutor_True_WhenEdgeHasCondition()
    {
        var edge = new GraphEdgeDetail(typeof(NodeA), typeof(NodeB), "SomeCondition", true, null);
        var topology = CreateTopology(
            outgoingEdgesBySource: new Dictionary<Type, List<GraphEdgeDetail>>
            {
                [typeof(NodeA)] = [edge],
            });

        Assert.True(topology.RequiresNeedlrExecutor);
    }

    // -----------------------------------------------------------------------
    // 6. RequiresNeedlrExecutor — optional edges
    // -----------------------------------------------------------------------

    [Fact]
    public void RequiresNeedlrExecutor_True_WhenOptionalEdgeExists()
    {
        var topology = CreateTopology(
            edgeIsRequired: new Dictionary<(Type, Type), bool>
            {
                [(typeof(NodeA), typeof(NodeB))] = false,
            });

        Assert.True(topology.RequiresNeedlrExecutor);
    }

    // -----------------------------------------------------------------------
    // 7. RequiresNeedlrExecutor — reducer
    // -----------------------------------------------------------------------

    [Fact]
    public void RequiresNeedlrExecutor_True_WhenReducerPresent()
    {
        var topology = CreateTopology(
            reducerFunc: inputs => string.Join(",", inputs));

        Assert.True(topology.RequiresNeedlrExecutor);
    }

    // -----------------------------------------------------------------------
    // 8. RequiresNeedlrExecutor — FirstMatching routing
    // -----------------------------------------------------------------------

    [Fact]
    public void RequiresNeedlrExecutor_True_WhenFirstMatchingRouting()
    {
        var topology = CreateTopology(
            graphRoutingMode: GraphRoutingMode.FirstMatching);

        Assert.True(topology.RequiresNeedlrExecutor);
    }

    [Fact]
    public void RequiresNeedlrExecutor_True_WhenExclusiveChoiceRouting()
    {
        var topology = CreateTopology(
            graphRoutingMode: GraphRoutingMode.ExclusiveChoice);

        Assert.True(topology.RequiresNeedlrExecutor);
    }

    // -----------------------------------------------------------------------
    // 9. RequiresNeedlrExecutor — false for trivial graph
    // -----------------------------------------------------------------------

    [Fact]
    public void RequiresNeedlrExecutor_False_ForTrivialGraph()
    {
        var edge = new GraphEdgeDetail(typeof(NodeA), typeof(NodeB), null, true, null);
        var topology = CreateTopology(
            graphRoutingMode: GraphRoutingMode.Deterministic,
            outgoingEdgesBySource: new Dictionary<Type, List<GraphEdgeDetail>>
            {
                [typeof(NodeA)] = [edge],
            },
            edgeIsRequired: new Dictionary<(Type, Type), bool>
            {
                [(typeof(NodeA), typeof(NodeB))] = true,
            });

        Assert.False(topology.RequiresNeedlrExecutor);
    }

    // -----------------------------------------------------------------------
    // 10. Empty edges — no crash
    // -----------------------------------------------------------------------

    [Fact]
    public void EmptyTopology_NoExceptions()
    {
        var topology = CreateTopology();

        Assert.Null(topology.EntryType);
        Assert.Empty(topology.AllTypes);
        Assert.Empty(topology.JoinModes);
        Assert.Empty(topology.OutgoingEdgesBySource);
        Assert.False(topology.HasWaitAnyNodes);
        Assert.False(topology.RequiresNeedlrExecutor);
    }

    // -----------------------------------------------------------------------
    // 11. AllMatching graph routing — no Needlr executor required
    // -----------------------------------------------------------------------

    [Fact]
    public void RequiresNeedlrExecutor_False_WhenAllMatchingNoAdvancedFeatures()
    {
        var edge = new GraphEdgeDetail(typeof(NodeA), typeof(NodeB), null, true, null);
        var topology = CreateTopology(
            graphRoutingMode: GraphRoutingMode.AllMatching,
            outgoingEdgesBySource: new Dictionary<Type, List<GraphEdgeDetail>>
            {
                [typeof(NodeA)] = [edge],
            },
            edgeIsRequired: new Dictionary<(Type, Type), bool>
            {
                [(typeof(NodeA), typeof(NodeB))] = true,
            });

        Assert.False(topology.RequiresNeedlrExecutor);
    }

    // -----------------------------------------------------------------------
    // 12. Per-node effective routing override triggers Needlr executor
    // -----------------------------------------------------------------------

    [Fact]
    public void RequiresNeedlrExecutor_True_WhenPerNodeEffectiveRoutingIsNonTrivial()
    {
        var topology = CreateTopology(
            graphRoutingMode: GraphRoutingMode.Deterministic,
            effectiveRoutingModes: new Dictionary<Type, GraphRoutingMode>
            {
                [typeof(NodeA)] = GraphRoutingMode.ExclusiveChoice,
            });

        Assert.True(topology.RequiresNeedlrExecutor);
    }

    // -----------------------------------------------------------------------
    // 13. Type-based InboundEdges/OutboundEdges — same-Name disambiguation
    // -----------------------------------------------------------------------

    [Fact]
    public void InboundEdges_DistinguishesSameNameDifferentNamespace_ByTypeKey()
    {
        // These two types share the simple Name "SharedNameAgent" but
        // live in different namespaces (different declaring types here).
        var typeA = typeof(NamespaceA.SharedNameAgent);
        var typeB = typeof(NamespaceB.SharedNameAgent);
        var entry = typeof(NodeA);

        Assert.Equal(typeA.Name, typeB.Name); // Precondition: names collide

        var inbound = new Dictionary<Type, IReadOnlyList<Type>>
        {
            [typeA] = new List<Type> { entry },
            [typeB] = new List<Type> { entry },
            [entry] = Array.Empty<Type>(),
        };
        var outbound = new Dictionary<Type, IReadOnlyList<Type>>
        {
            [entry] = new List<Type> { typeA, typeB },
            [typeA] = Array.Empty<Type>(),
            [typeB] = Array.Empty<Type>(),
        };

        var topology = CreateTopology(
            entryType: entry,
            allTypes: [entry, typeA, typeB],
            inboundEdges: inbound,
            outboundEdges: outbound);

        // Both types are present as distinct keys
        Assert.True(topology.InboundEdges.ContainsKey(typeA));
        Assert.True(topology.InboundEdges.ContainsKey(typeB));
        Assert.Equal(2, topology.OutboundEdges[entry].Count);
    }
}

// Types that share Name but live in different nested "namespaces" for collision tests.
// Can't use file-scoped and block namespaces in the same file, so we use
// static holder classes to simulate different namespaces with the same inner type name.
internal static class NamespaceA { internal sealed class SharedNameAgent; }
internal static class NamespaceB { internal sealed class SharedNameAgent; }
