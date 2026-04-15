using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Iterative;

/// <summary>
/// Mutable state passed to the prompt factory and <see cref="IterativeLoopOptions.IsComplete"/>
/// predicate on each iteration of an <see cref="IIterativeAgentLoop"/>.
/// </summary>
/// <remarks>
/// <para>
/// The prompt factory uses this context to build a state-aware prompt for each iteration.
/// The workspace contains all persistent state (files written by tools); the prompt factory
/// reads workspace files to understand what has been accomplished so far.
/// </para>
/// <para>
/// <see cref="LastToolResults"/> contains tool results from the <em>previous</em> iteration
/// only. They are ephemeral — cleared before each new iteration runs. The prompt factory
/// can selectively embed them (e.g., search results) into the next prompt, or ignore them
/// if the tool wrote its output to the workspace.
/// </para>
/// </remarks>
public sealed class IterativeContext
{
    /// <summary>
    /// Gets the zero-based iteration number. Incremented before each iteration runs.
    /// The prompt factory sees 0 on the first iteration, 1 on the second, etc.
    /// </summary>
    public int Iteration { get; internal set; }

    /// <summary>
    /// Gets the workspace shared across all iterations. Tools read and write files here;
    /// the prompt factory reads files to build state-aware prompts.
    /// </summary>
    /// <remarks>
    /// The workspace is the primary memory mechanism for the iterative loop — it replaces
    /// conversation history accumulation. The loop does NOT manage workspace lifecycle;
    /// callers create and own the workspace instance.
    /// </remarks>
    public required IWorkspace Workspace { get; init; }

    /// <summary>
    /// Gets the tool call results from the previous iteration, or an empty list on the
    /// first iteration. Results are ephemeral — they are replaced after each iteration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In <see cref="ToolResultMode.SingleCall"/> mode, these are the results of the tool
    /// calls the model requested (which were NOT sent back to the model). The prompt factory
    /// is responsible for embedding any relevant results into the next prompt.
    /// </para>
    /// <para>
    /// In <see cref="ToolResultMode.OneRoundTrip"/> mode, these are the results of any
    /// <em>second-round</em> tool calls (the model saw the first-round results and requested
    /// more tools, which were executed but not sent back). First-round results were already
    /// consumed by the model.
    /// </para>
    /// <para>
    /// In <see cref="ToolResultMode.MultiRound"/> mode, these are the results of tool calls
    /// from the final round that triggered the round limit. If the model produced a text
    /// response, this list is empty (no pending results).
    /// </para>
    /// </remarks>
    public IReadOnlyList<ToolCallResult> LastToolResults { get; internal set; } = [];

    /// <summary>
    /// Gets a general-purpose state dictionary for passing arbitrary data between
    /// iterations. Callers can store anything here — counters, flags, intermediate
    /// computations — that doesn't belong in the workspace.
    /// </summary>
    public IDictionary<string, object> State { get; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets the cancellation token for the current loop run. Tools and prompt factories
    /// should check this for cooperative cancellation.
    /// </summary>
    public CancellationToken CancellationToken { get; internal set; }
}
