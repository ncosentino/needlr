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
    /// The target node proceeds when ANY incoming edge completes. Other incoming
    /// branches continue in the background; their results are available if they
    /// finish before the graph terminates.
    /// </summary>
    /// <remarks>
    /// MAF's <c>FanInEdgeData</c> implements barrier-only (WaitAll) semantics natively.
    /// WaitAny is implemented at the Needlr orchestration layer using
    /// <see cref="System.Threading.Tasks.Task.WhenAny(System.Threading.Tasks.Task[])"/>.
    /// </remarks>
    WaitAny = 1,
}
