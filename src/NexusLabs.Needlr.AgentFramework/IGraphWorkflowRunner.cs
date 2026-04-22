using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Progress;

namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Executes DAG/graph workflows declared via <see cref="AgentGraphEntryAttribute"/>,
/// <see cref="AgentGraphEdgeAttribute"/>, and <see cref="AgentGraphNodeAttribute"/>.
/// </summary>
/// <remarks>
/// <para>
/// Registered as a singleton by <c>UsingAgentFramework()</c>. Inject via
/// constructor or resolve from <c>IServiceProvider</c>.
/// </para>
/// <para>
/// The runner automatically selects the execution strategy based on declared
/// topology. Graphs using only <see cref="GraphJoinMode.WaitAll"/> nodes
/// execute via MAF's native BSP engine. Graphs containing any
/// <see cref="GraphJoinMode.WaitAny"/> node or <see cref="GraphRoutingMode.LlmChoice"/>
/// routing use Needlr's own executor.
/// </para>
/// </remarks>
public interface IGraphWorkflowRunner
{
    /// <summary>
    /// Executes a graph/DAG workflow by name.
    /// </summary>
    /// <param name="graphName">
    /// The graph name (case-sensitive). Must match the
    /// <see cref="AgentGraphEntryAttribute.GraphName"/> and
    /// <see cref="AgentGraphEdgeAttribute.GraphName"/> values on at least one
    /// agent type.
    /// </param>
    /// <param name="input">The input message to send to the entry node.</param>
    /// <param name="progress">
    /// Optional progress reporter for real-time execution events.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// An <see cref="IDagRunResult"/> containing per-node diagnostics, edge
    /// metadata, timing offsets, and aggregate token usage.
    /// </returns>
    Task<IDagRunResult> RunGraphAsync(
        string graphName,
        string input,
        IProgressReporter? progress = null,
        CancellationToken cancellationToken = default);
}
