namespace NexusLabs.Needlr.AgentFramework.Progress;

/// <summary>
/// Emits progress events to registered sinks. Carries hierarchical context
/// (workflow ID, agent ID, depth) so events are automatically correlated.
/// </summary>
/// <remarks>
/// <para>
/// Use <see cref="CreateChild"/> to create a scoped reporter for sub-agent runs.
/// The child reporter inherits the parent's sinks and auto-sets <c>ParentAgentId</c>
/// and increments <c>Depth</c> on all emitted events.
/// </para>
/// </remarks>
public interface IProgressReporter
{
    /// <summary>Emits a progress event to all registered sinks.</summary>
    void Report(IProgressEvent progressEvent);

    /// <summary>
    /// Creates a child reporter scoped to a specific agent. Events emitted by the child
    /// carry the parent's agent ID as <c>ParentAgentId</c> and an incremented <c>Depth</c>.
    /// </summary>
    /// <param name="agentId">The agent ID for the child scope.</param>
    IProgressReporter CreateChild(string agentId);

    /// <summary>Gets the workflow ID for this reporter's scope.</summary>
    string WorkflowId { get; }

    /// <summary>Gets the current agent ID, or <see langword="null"/> for workflow-level scope.</summary>
    string? AgentId { get; }

    /// <summary>Gets the nesting depth.</summary>
    int Depth { get; }
}
