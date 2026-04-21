using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Diagnostics for a single node within a DAG workflow execution, including
/// edge connectivity, timing offsets relative to the workflow start, and the
/// <see cref="NodeKind"/> discriminator that distinguishes agent nodes from
/// reducer nodes.
/// </summary>
/// <remarks>
/// <para>
/// Access node results via <see cref="IDagRunResult.NodeResults"/> after a
/// DAG workflow run completes. Reducer nodes (<see cref="NodeKind.Reducer"/>)
/// will have <see langword="null"/> diagnostics because they do not execute
/// LLM calls.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var dagResult = (IDagRunResult)pipelineResult;
/// foreach (var (nodeId, node) in dagResult.NodeResults)
/// {
///     Console.WriteLine($"  {nodeId}: {node.Kind}, " +
///         $"offset={node.StartOffset.TotalMilliseconds}ms, " +
///         $"duration={node.Duration.TotalMilliseconds}ms");
/// }
/// </code>
/// </example>
public interface IDagNodeResult
{
    /// <summary>Gets the unique identifier of the node within the graph.</summary>
    string NodeId { get; }

    /// <summary>Gets the agent or reducer name for the node.</summary>
    string AgentName { get; }

    /// <summary>Gets whether this node is an LLM-backed agent or a deterministic reducer.</summary>
    NodeKind Kind { get; }

    /// <summary>
    /// Gets the captured diagnostics for the node's execution, or
    /// <see langword="null"/> for reducer nodes or when diagnostics were not enabled.
    /// </summary>
    IAgentRunDiagnostics? Diagnostics { get; }

    /// <summary>
    /// Gets the final <see cref="ChatResponse"/> produced by this node, or
    /// <see langword="null"/> if the node is a reducer or produced no text response.
    /// </summary>
    ChatResponse? FinalResponse { get; }

    /// <summary>Gets the node IDs that feed into this node.</summary>
    IReadOnlyList<string> InboundEdges { get; }

    /// <summary>Gets the node IDs that this node feeds into.</summary>
    IReadOnlyList<string> OutboundEdges { get; }

    /// <summary>Gets the wall-clock offset from the workflow start to the node's start.</summary>
    TimeSpan StartOffset { get; }

    /// <summary>Gets the wall-clock duration of the node's execution.</summary>
    TimeSpan Duration { get; }
}
