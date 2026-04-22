namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Controls how a target node handles multiple incoming edges in a graph workflow.
/// </summary>
public enum GraphJoinMode
{
    /// <summary>
    /// Barrier join: the target node waits for ALL incoming edges to complete
    /// before executing. Maps to MAF's <c>FanInEdgeData</c>.
    /// </summary>
    WaitAll = 0,

    /// <summary>
    /// The target node proceeds when ANY incoming edge completes. The first
    /// branch to finish triggers execution of the target; results from
    /// later-completing branches are available in <c>IDagRunResult</c> if they
    /// finish before the graph terminates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Not yet implemented.</b> MAF's graph execution engine uses Bulk
    /// Synchronous Parallel (BSP) with mandatory synchronization barriers.
    /// Every superstep waits for ALL active nodes to complete — there is no
    /// primitive for "proceed when any source emits." Implementing WaitAny
    /// requires a custom execution layer outside MAF's <c>InProcessExecution</c>,
    /// which is planned for a future release.
    /// </para>
    /// <para>
    /// Declaring <c>WaitAny</c> today is accepted by the compiler and
    /// analyzers, but <see cref="NexusLabs.Needlr.AgentFramework.IWorkflowFactory.CreateGraphWorkflow(string)"/>
    /// will throw <see cref="System.NotSupportedException"/> at runtime.
    /// </para>
    /// </remarks>
    WaitAny = 1,
}
