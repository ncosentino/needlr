// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Compile-time graph topology data registered by the source generator.
/// Each instance captures the entry point, edges, nodes, and optional reducer
/// for one named graph.
/// </summary>
public sealed class GraphTopologyRegistration
{
    /// <summary>
    /// Initializes a new instance of <see cref="GraphTopologyRegistration"/>.
    /// </summary>
    /// <param name="entryType">The entry-point agent type for this graph, or <c>null</c> if none was declared.</param>
    /// <param name="routingMode">The graph-level routing mode (cast from <c>GraphRoutingMode</c>).</param>
    /// <param name="edges">All edges in this graph with source, target, optional condition, required flag, and optional per-node routing mode.</param>
    /// <param name="nodes">All explicitly declared nodes with their join mode.</param>
    /// <param name="reducer">The reducer type and static method name, or <c>null</c> if no reducer was declared.</param>
    public GraphTopologyRegistration(
        Type? entryType,
        int routingMode,
        IReadOnlyList<(Type Source, Type Target, string? Condition, bool IsRequired, int? NodeRoutingMode)> edges,
        IReadOnlyList<(Type NodeType, int JoinMode)> nodes,
        (Type ReducerType, string ReducerMethod)? reducer)
    {
        EntryType = entryType;
        RoutingMode = routingMode;
        Edges = edges;
        Nodes = nodes;
        Reducer = reducer;
    }

    /// <summary>The entry-point agent type for this graph.</summary>
    public Type? EntryType { get; }

    /// <summary>The graph-level routing mode as an integer (maps to <c>GraphRoutingMode</c>).</summary>
    public int RoutingMode { get; }

    /// <summary>All declared edges: source agent type, target agent type, optional condition string, required flag, and optional per-node routing mode override.</summary>
    public IReadOnlyList<(Type Source, Type Target, string? Condition, bool IsRequired, int? NodeRoutingMode)> Edges { get; }

    /// <summary>All explicitly declared node join modes.</summary>
    public IReadOnlyList<(Type NodeType, int JoinMode)> Nodes { get; }

    /// <summary>The reducer type and static method name, if declared.</summary>
    public (Type ReducerType, string ReducerMethod)? Reducer { get; }
}
