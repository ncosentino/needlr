namespace NexusLabs.Needlr.AgentFramework.Progress;

/// <summary>
/// A reducer node in a DAG workflow was invoked. Reducer nodes are deterministic
/// functions (no LLM calls) that aggregate branch outputs during fan-in convergence.
/// </summary>
/// <remarks>
/// <para>
/// This event is distinct from <see cref="AgentInvokedEvent"/> because reducer nodes
/// do not carry LLM-specific metadata (token usage, model, etc.). Consumers that
/// only care about agent turns can safely ignore this event type.
/// </para>
/// </remarks>
public sealed record ReducerNodeInvokedEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    string NodeId,
    string? GraphName,
    string? BranchId,
    int InputBranchCount,
    TimeSpan Duration) : IProgressEvent;
