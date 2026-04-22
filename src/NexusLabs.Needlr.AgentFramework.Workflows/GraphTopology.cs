namespace NexusLabs.Needlr.AgentFramework.Workflows;

/// <summary>
/// The complete topology of a graph workflow, discovered from attributes at
/// runtime. Immutable once built.
/// </summary>
internal sealed record GraphTopology(
    Type? EntryType,
    HashSet<Type> AllTypes,
    Dictionary<Type, GraphJoinMode> JoinModes,
    Dictionary<Type, List<Type>> IncomingTypes,
    Dictionary<string, IReadOnlyList<string>> InboundEdges,
    Dictionary<string, IReadOnlyList<string>> OutboundEdges,
    GraphRoutingMode GraphRoutingMode,
    Dictionary<Type, List<GraphEdgeDetail>> OutgoingEdgesBySource,
    Dictionary<Type, GraphRoutingMode> EffectiveRoutingModes,
    Dictionary<(Type Source, Type Target), bool> EdgeIsRequired,
    Func<IReadOnlyList<string>, string>? ReducerFunc,
    Type? ReducerType)
{
    /// <summary>
    /// Whether any node in this graph uses <see cref="GraphJoinMode.WaitAny"/>.
    /// </summary>
    public bool HasWaitAnyNodes =>
        JoinModes.Values.Any(m => m == GraphJoinMode.WaitAny);

    /// <summary>
    /// Whether this graph requires the Needlr-native executor instead of the
    /// MAF BSP engine. The MAF BSP path only supports unconditional WaitAll
    /// graphs with Deterministic/AllMatching routing and no reducers. Any
    /// advanced feature forces the Needlr-native executor.
    /// </summary>
    public bool RequiresNeedlrExecutor =>
        HasWaitAnyNodes ||
        HasConditions ||
        HasOptionalEdges ||
        HasReducer ||
        HasNonTrivialRouting;

    private bool HasConditions =>
        OutgoingEdgesBySource.Values
            .SelectMany(edges => edges)
            .Any(e => e.Condition is not null);

    private bool HasOptionalEdges =>
        EdgeIsRequired.Values.Any(isReq => !isReq);

    private bool HasReducer =>
        ReducerFunc is not null;

    private bool HasNonTrivialRouting =>
        GraphRoutingMode != GraphRoutingMode.Deterministic &&
        GraphRoutingMode != GraphRoutingMode.AllMatching ||
        EffectiveRoutingModes.Values.Any(m =>
            m != GraphRoutingMode.Deterministic &&
            m != GraphRoutingMode.AllMatching);
}
