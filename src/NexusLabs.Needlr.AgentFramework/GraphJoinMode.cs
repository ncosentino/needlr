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
    /// later-completing branches are available in the result dictionary if they
    /// finish before the graph terminates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MAF's BSP execution model uses mandatory synchronization barriers,
    /// so WaitAny is <b>not compatible</b> with
    /// <see cref="IWorkflowFactory.CreateGraphWorkflow(string)"/> (which returns
    /// a MAF <c>Workflow</c>). Use the <c>RunGraphAsync</c> extension method
    /// from <c>NexusLabs.Needlr.AgentFramework.Workflows</c> instead — it detects
    /// WaitAny nodes and uses Needlr's own graph executor with
    /// <see cref="System.Threading.Tasks.Task.WhenAny(System.Threading.Tasks.Task[])"/>.
    /// </para>
    /// </remarks>
    WaitAny = 1,
}
