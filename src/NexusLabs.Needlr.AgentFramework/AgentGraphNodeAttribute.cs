namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Declares per-node configuration for a graph workflow participant. Apply to
/// agents that receive multiple incoming edges to control join semantics.
/// </summary>
/// <remarks>
/// <para>
/// This attribute is optional. Agents that do not carry it use the default
/// <see cref="GraphJoinMode.WaitAll"/> join mode.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [NeedlrAiAgent(Instructions = "Synthesize all research findings.")]
/// [AgentGraphNode("Research", JoinMode = GraphJoinMode.WaitAll)]
/// public class SummarizerAgent { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class AgentGraphNodeAttribute : Attribute
{
    /// <summary>
    /// Initializes a new <see cref="AgentGraphNodeAttribute"/>.
    /// </summary>
    /// <param name="graphName">The graph this node configuration applies to.</param>
    public AgentGraphNodeAttribute(string graphName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(graphName);
        GraphName = graphName;
    }

    /// <summary>Gets the graph name this node belongs to.</summary>
    public string GraphName { get; }

    /// <summary>
    /// Gets or sets how this node handles multiple incoming edges.
    /// Defaults to <see cref="GraphJoinMode.WaitAll"/> (barrier).
    /// </summary>
    public GraphJoinMode JoinMode { get; set; } = GraphJoinMode.WaitAll;
}
