using System.Collections.Concurrent;
using System.Reflection;

using NexusLabs.Needlr.AgentFramework;

namespace NexusLabs.Needlr.AgentFramework.Workflows;

/// <summary>
/// Discovers and caches <see cref="GraphTopology"/> from attributes declared
/// on agent types. Cached per graph name for the lifetime of the provider.
/// </summary>
internal sealed class GraphTopologyProvider
{
    private readonly ConcurrentDictionary<string, GraphTopology> _cache = new();

    /// <summary>
    /// Gets the topology for the named graph, scanning assemblies on first access
    /// and caching the result for subsequent calls.
    /// </summary>
    public GraphTopology GetTopology(string graphName) =>
        _cache.GetOrAdd(graphName, static name => DiscoverTopology(name));

    private static GraphTopology DiscoverTopology(string graphName)
    {
        Type? entryType = null;
        GraphRoutingMode graphRoutingMode = GraphRoutingMode.Deterministic;
        var edgeDetails = new List<GraphEdgeDetail>();
        var joinModes = new Dictionary<Type, GraphJoinMode>();
        var allTypes = new HashSet<Type>();
        Func<IReadOnlyList<string>, string>? reducerFunc = null;
        Type? reducerType = null;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException) { continue; }
            catch (FileNotFoundException) { continue; }

            foreach (var type in types)
            {
                foreach (var attr in type.GetCustomAttributes<AgentGraphEntryAttribute>())
                {
                    if (string.Equals(attr.GraphName, graphName, StringComparison.Ordinal))
                    {
                        entryType = type;
                        graphRoutingMode = attr.RoutingMode;
                        allTypes.Add(type);
                    }
                }

                foreach (var attr in type.GetCustomAttributes<AgentGraphEdgeAttribute>())
                {
                    if (string.Equals(attr.GraphName, graphName, StringComparison.Ordinal))
                    {
                        edgeDetails.Add(new GraphEdgeDetail(
                            type,
                            attr.TargetAgentType,
                            attr.Condition,
                            attr.IsRequired,
                            attr.HasNodeRoutingMode ? attr.NodeRoutingMode : null));
                        allTypes.Add(type);
                        allTypes.Add(attr.TargetAgentType);
                    }
                }

                foreach (var attr in type.GetCustomAttributes<AgentGraphNodeAttribute>())
                {
                    if (string.Equals(attr.GraphName, graphName, StringComparison.Ordinal))
                    {
                        joinModes[type] = attr.JoinMode;
                    }
                }

                foreach (var attr in type.GetCustomAttributes<AgentGraphReducerAttribute>())
                {
                    if (!string.Equals(attr.GraphName, graphName, StringComparison.Ordinal))
                        continue;
                    if (string.IsNullOrWhiteSpace(attr.ReducerMethod))
                        continue;

                    var method = type.GetMethod(
                        attr.ReducerMethod,
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        [typeof(IReadOnlyList<string>)],
                        null);

                    if (method is not null && method.ReturnType == typeof(string))
                    {
                        reducerType = type;
                        var captured = method;
                        reducerFunc = inputs => (string)captured.Invoke(null, [inputs])!;
                    }
                }
            }
        }

        var incomingTypes = new Dictionary<Type, List<Type>>();
        var inboundEdges = new Dictionary<Type, List<Type>>();
        var outboundEdges = new Dictionary<Type, List<Type>>();

        foreach (var type in allTypes)
        {
            incomingTypes[type] = [];
            inboundEdges[type] = [];
            outboundEdges[type] = [];
        }

        foreach (var edge in edgeDetails)
        {
            incomingTypes[edge.Target].Add(edge.Source);
            inboundEdges[edge.Target].Add(edge.Source);
            outboundEdges[edge.Source].Add(edge.Target);
        }

        var outgoingEdgesBySource = new Dictionary<Type, List<GraphEdgeDetail>>();
        foreach (var edge in edgeDetails)
        {
            if (!outgoingEdgesBySource.TryGetValue(edge.Source, out var list))
            {
                list = [];
                outgoingEdgesBySource[edge.Source] = list;
            }

            list.Add(edge);
        }

        var effectiveRoutingModes = new Dictionary<Type, GraphRoutingMode>();
        foreach (var (sourceType, sourceEdges) in outgoingEdgesBySource)
        {
            var nodeOverride = sourceEdges
                .Select(e => e.NodeRoutingModeOverride)
                .FirstOrDefault(m => m is not null);
            effectiveRoutingModes[sourceType] = nodeOverride ?? graphRoutingMode;
        }

        var edgeIsRequired = new Dictionary<(Type Source, Type Target), bool>();
        foreach (var edge in edgeDetails)
        {
            edgeIsRequired[(edge.Source, edge.Target)] = edge.IsRequired;
        }

        return new GraphTopology(
            entryType,
            allTypes,
            joinModes,
            incomingTypes,
            inboundEdges.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<Type>)kv.Value),
            outboundEdges.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<Type>)kv.Value),
            graphRoutingMode,
            outgoingEdgesBySource,
            effectiveRoutingModes,
            edgeIsRequired,
            reducerFunc,
            reducerType);
    }
}
