namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Controls how a source node selects which outgoing edges to follow in a
/// graph workflow.
/// </summary>
public enum GraphRoutingMode
{
    /// <summary>
    /// Condition strings reference predicate methods on the agent class.
    /// Each edge condition is evaluated as a <c>Func&lt;object?, bool&gt;</c>.
    /// Edges without conditions are unconditional (always followed).
    /// </summary>
    Deterministic = 0,

    /// <summary>
    /// All edges whose condition passes are followed in parallel (fan-out).
    /// When multiple conditions are true, all matching targets execute concurrently.
    /// </summary>
    AllMatching = 1,

    /// <summary>
    /// Edges are evaluated in declaration order; only the first matching edge
    /// is followed. This is a Needlr abstraction — MAF has no ordered-priority
    /// routing primitive.
    /// </summary>
    FirstMatching = 2,

    /// <summary>
    /// Exactly one edge must match. Maps to MAF's <c>AddSwitch</c> with
    /// <c>AddCase</c> per edge. An analyzer error is emitted if conditions
    /// are ambiguous at compile time.
    /// </summary>
    ExclusiveChoice = 3,

    /// <summary>
    /// Condition strings become handoff-style tool descriptions. The agent's
    /// LLM selects which edge to follow. The routing decision is recorded in
    /// diagnostics for auditability.
    /// </summary>
    LlmChoice = 4,
}
