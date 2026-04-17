namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Provides access to per-tool-call diagnostics captured by the function-calling
/// middleware. Used by pipeline run extensions to prevent silent tool call data loss
/// when AsyncLocal diagnostics builders don't propagate across workflow infrastructure.
/// </summary>
public interface IToolCallCollector
{
    /// <summary>
    /// Drains all captured tool calls since the last drain. Thread-safe.
    /// Returns an empty list if no tool calls have been recorded.
    /// </summary>
    IReadOnlyList<ToolCallDiagnostics> DrainToolCalls();
}
