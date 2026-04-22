namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Declares a directed edge from the decorated agent to the specified target
/// agent type within a named graph workflow. Apply multiple times for fan-out
/// (multiple outgoing edges from one agent).
/// </summary>
/// <remarks>
/// <para>
/// Edge placement follows the same convention as <see cref="AgentHandoffsToAttribute"/>:
/// the attribute is placed on the <em>source</em> agent and references the target.
/// </para>
/// <para>
/// When <see cref="Condition"/> is set, the edge is conditional. In
/// <see cref="GraphRoutingMode.Deterministic"/> mode, the condition string
/// names a predicate method on the agent class. In
/// <see cref="GraphRoutingMode.LlmChoice"/> mode, the condition string
/// becomes a tool description for LLM-based routing.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [NeedlrAiAgent(Instructions = "Analyze the request.")]
/// [AgentGraphEntry("Research", MaxSupersteps = 15)]
/// [AgentGraphEdge("Research", typeof(WebAgent), Condition = "NeedsWebData")]
/// [AgentGraphEdge("Research", typeof(SummaryAgent))]
/// public class AnalyzerAgent { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class AgentGraphEdgeAttribute : Attribute
{
    /// <summary>
    /// Initializes a new <see cref="AgentGraphEdgeAttribute"/>.
    /// </summary>
    /// <param name="graphName">The name of the graph this edge belongs to.</param>
    /// <param name="targetAgentType">The downstream agent type.</param>
    public AgentGraphEdgeAttribute(string graphName, Type targetAgentType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(graphName);
        ArgumentNullException.ThrowIfNull(targetAgentType);
        GraphName = graphName;
        TargetAgentType = targetAgentType;
    }

    /// <summary>Gets the graph name this edge belongs to.</summary>
    public string GraphName { get; }

    /// <summary>Gets the downstream agent type.</summary>
    public Type TargetAgentType { get; }

    /// <summary>
    /// Gets or sets the optional routing condition. Interpretation depends on
    /// the graph's <see cref="GraphRoutingMode"/>. When <see langword="null"/>,
    /// the edge is unconditional.
    /// </summary>
    public string? Condition { get; set; }

    /// <summary>
    /// Gets or sets whether this edge's target is required for graph success.
    /// When <see langword="true"/> (default), a failure in the target node
    /// fails the entire graph. When <see langword="false"/>, the branch is
    /// marked degraded but parallel branches continue.
    /// </summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>
    /// Gets or sets a per-node routing mode override for the source node of this
    /// edge. When set on any edge from a given source node, this overrides the
    /// graph-wide <see cref="AgentGraphEntryAttribute.RoutingMode"/> for that
    /// node's outgoing edges. Only needs to be set on one edge per source node
    /// (the generator reads the first non-null value).
    /// Use <see cref="HasNodeRoutingMode"/> to check if a value was explicitly set.
    /// </summary>
    public GraphRoutingMode NodeRoutingMode
    {
        get => _nodeRoutingMode;
        set
        {
            _nodeRoutingMode = value;
            HasNodeRoutingMode = true;
        }
    }

    /// <summary>
    /// Gets whether <see cref="NodeRoutingMode"/> was explicitly set on this edge.
    /// When <see langword="false"/>, the graph-wide routing mode is used.
    /// </summary>
    public bool HasNodeRoutingMode { get; private set; }

    private GraphRoutingMode _nodeRoutingMode;
}
