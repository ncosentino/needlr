namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Marks the decorated agent as the entry point for a named graph workflow.
/// Exactly one agent per graph must carry this attribute.
/// </summary>
/// <remarks>
/// <para>
/// Graph-wide configuration (routing mode, superstep limits) is declared here.
/// Per-node overrides for routing mode can be specified on the first
/// <see cref="AgentGraphEdgeAttribute"/> from a given source node.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [NeedlrAiAgent(Instructions = "Analyze the request.")]
/// [AgentGraphEntry("Research", MaxSupersteps = 15, RoutingMode = GraphRoutingMode.AllMatching)]
/// [AgentGraphEdge("Research", typeof(WebAgent), Condition = "NeedsWebData")]
/// [AgentGraphEdge("Research", typeof(SummaryAgent))]
/// public class AnalyzerAgent { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class AgentGraphEntryAttribute : Attribute
{
    /// <summary>
    /// Initializes a new <see cref="AgentGraphEntryAttribute"/>.
    /// </summary>
    /// <param name="graphName">The name of the graph this agent is the entry point for.</param>
    public AgentGraphEntryAttribute(string graphName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(graphName);
        GraphName = graphName;
    }

    /// <summary>Gets the graph name this agent is the entry point for.</summary>
    public string GraphName { get; }

    /// <summary>
    /// Gets or sets the maximum number of supersteps before the graph terminates.
    /// A superstep is one round of executing all currently-active nodes.
    /// Defaults to <c>20</c>.
    /// </summary>
    public int MaxSupersteps { get; set; } = 20;

    /// <summary>
    /// Gets or sets the graph-wide default routing mode. Individual nodes can
    /// override this via a <c>RoutingMode</c> property on their first
    /// <see cref="AgentGraphEdgeAttribute"/>.
    /// </summary>
    public GraphRoutingMode RoutingMode { get; set; } = GraphRoutingMode.Deterministic;
}
