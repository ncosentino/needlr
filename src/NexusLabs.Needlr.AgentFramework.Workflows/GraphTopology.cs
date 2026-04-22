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
    /// Whether this graph requires the Needlr-native executor (WaitAny or
    /// LlmChoice routing) instead of the MAF BSP engine.
    /// </summary>
    public bool RequiresNeedlrExecutor =>
        HasWaitAnyNodes ||
        GraphRoutingMode == GraphRoutingMode.LlmChoice ||
        EffectiveRoutingModes.Values.Any(m => m == GraphRoutingMode.LlmChoice);
}
