namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Provides access to the diagnostics captured during the most recent agent run in the current
/// async flow. Uses the mutable-holder <see cref="AsyncLocal{T}"/> pattern so that writes from
/// child async flows (inside middleware) are visible to the parent scope.
/// </summary>
/// <remarks>
/// <para>
/// Callers wrap agent execution in <see cref="BeginCapture"/>:
/// </para>
/// <code>
/// using (diagnosticsAccessor.BeginCapture())
/// {
///     await agent.RunAsync(prompt);
///     var diagnostics = diagnosticsAccessor.LastRunDiagnostics;
///     // diagnostics contains token usage, tool calls, timing, etc.
/// }
/// </code>
/// </remarks>
public interface IAgentDiagnosticsAccessor
{
    /// <summary>
    /// Gets the diagnostics from the last completed agent run in the current scope,
    /// or <see langword="null"/> if no run has completed yet.
    /// </summary>
    IAgentRunDiagnostics? LastRunDiagnostics { get; }

    /// <summary>
    /// Opens a capture scope. Diagnostics written by middleware during agent execution
    /// are visible via <see cref="LastRunDiagnostics"/> after the run completes.
    /// Disposing the returned handle restores the previous scope.
    /// </summary>
    IDisposable BeginCapture();

    /// <summary>
    /// Gets the <see cref="IChatCompletionCollector"/> wired by the diagnostics system,
    /// or <see langword="null"/> if diagnostics were not configured. Used as a fallback
    /// when <see cref="LastRunDiagnostics"/> is unavailable (e.g., when AsyncLocal state
    /// does not propagate across workflow execution boundaries).
    /// </summary>
    IChatCompletionCollector? CompletionCollector => null;
}
