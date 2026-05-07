namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Read-only accessor that exposes a snapshot of the diagnostics being accumulated
/// for the currently in-flight agent run on the calling async flow. The post-run
/// sibling is <see cref="IAgentDiagnosticsAccessor.LastRunDiagnostics"/>.
/// </summary>
/// <remarks>
/// <para>
/// Backed by the same <see cref="AgentRunDiagnosticsBuilder"/> that produces the
/// post-run <see cref="IAgentRunDiagnostics"/>. Each call to <see cref="Current"/>
/// returns a freshly materialized snapshot — the chat completion and tool call
/// collections are copied into ordered arrays at snapshot time so the caller can
/// iterate freely while the underlying run continues to accumulate records.
/// </para>
/// <para>
/// The primary use case is intra-tool verification: a tool can inspect prior
/// completed tool calls (and their structured arguments / return values, which are
/// captured losslessly by <see cref="ToolCallDiagnostics.Arguments"/> and
/// <see cref="ToolCallDiagnostics.Result"/>) to cross-check what an LLM is now
/// claiming against what actually happened earlier in the same run.
/// </para>
/// <example>
/// <code>
/// public sealed class RecordFixDecisionTool(IInFlightAgentDiagnosticsAccessor diagnostics)
/// {
///     [AgentFunction]
///     public Result Record(int issueId, string action, string reason)
///     {
///         if (action == "Applied" &amp;&amp; diagnostics.Current is { } snap)
///         {
///             var lastWrite = snap.ToolCalls
///                 .Where(t => t.ToolName == "FindReplaceWithCount")
///                 .LastOrDefault();
///             if (lastWrite?.Result is FindReplaceResult { Success: false } r)
///             {
///                 // Override: agent said Applied but the prior write failed.
///                 action = "Rejected";
///                 reason = $"[auto-corrected: prior write failed: {r.Error}] {reason}";
///             }
///         }
///         // ...
///     }
/// }
/// </code>
/// </example>
/// <para>
/// <b>Mid-flight semantics on the returned snapshot.</b> The shape is
/// <see cref="IAgentRunDiagnostics"/>, which is also used post-run, but a few
/// fields have provisional meaning while the run is still executing:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <see cref="IAgentRunDiagnostics.ToolCalls"/> contains every tool call
///     that has <i>completed</i> on this async flow so far, in chronological
///     order by reserved sequence number. The currently executing tool call
///     (the one whose body called <see cref="Current"/>) is <i>not</i> present —
///     diagnostics are appended only after the tool returns.
///   </description></item>
///   <item><description>
///     <see cref="IAgentRunDiagnostics.ChatCompletions"/> contains every chat
///     completion that has finished so far. Like tool calls, the in-progress
///     completion that triggered the current tool call is not yet present.
///   </description></item>
///   <item><description>
///     <see cref="IAgentRunDiagnostics.AggregateTokenUsage"/> reflects only
///     the completed completions; it grows as the run continues.
///   </description></item>
///   <item><description>
///     <see cref="IAgentRunDiagnostics.TotalDuration"/> is elapsed-so-far,
///     measured from <see cref="IAgentRunDiagnostics.StartedAt"/> to the
///     instant the snapshot was taken.
///   </description></item>
///   <item><description>
///     <see cref="IAgentRunDiagnostics.CompletedAt"/> is the snapshot time,
///     not a true run-end time.
///   </description></item>
///   <item><description>
///     <see cref="IAgentRunDiagnostics.Succeeded"/> defaults to <see langword="true"/>
///     unless the run has already explicitly recorded a failure.
///     <see cref="IAgentRunDiagnostics.ErrorMessage"/> follows the same rule.
///   </description></item>
///   <item><description>
///     <see cref="IAgentRunDiagnostics.OutputResponse"/> is typically
///     <see langword="null"/> mid-run because the response is recorded at run end.
///   </description></item>
/// </list>
/// <para>
/// <b>Sub-agent semantics.</b> If a sub-agent is currently executing (i.e. its
/// own <see cref="AgentRunDiagnosticsBuilder"/> was pushed onto the AsyncLocal
/// stack), <see cref="Current"/> reflects the innermost (sub-agent's) run.
/// When the sub-agent completes and its builder is disposed, the parent run's
/// builder becomes current again.
/// </para>
/// <para>
/// <b>Concurrency.</b> Each agent run on a parallel async flow has its own
/// AsyncLocal-scoped builder, so <see cref="Current"/> on flow A never returns
/// diagnostics from flow B. The snapshot itself is immutable: subsequent
/// additions to the underlying builder do not mutate previously-returned
/// snapshots.
/// </para>
/// <para>
/// <b>Returns <see langword="null"/></b> when called outside an active agent
/// run (e.g. from production code that resolves the accessor directly without
/// going through the diagnostics middleware, or from tests that did not start
/// a builder). Callers must handle <see langword="null"/> gracefully.
/// </para>
/// </remarks>
public interface IInFlightAgentDiagnosticsAccessor
{
    /// <summary>
    /// Gets a snapshot of the diagnostics accumulated so far for the agent run
    /// currently executing on this async flow, or <see langword="null"/> if no
    /// run is active. See the type-level remarks for mid-flight field semantics
    /// and tool-call timing guarantees.
    /// </summary>
    IAgentRunDiagnostics? Current { get; }
}
