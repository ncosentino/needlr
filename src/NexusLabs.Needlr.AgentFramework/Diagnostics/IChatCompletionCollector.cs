namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Provides access to per-LLM-call completion diagnostics captured by the chat client
/// middleware. Used by pipeline run extensions to correlate LLM call timing with
/// agent turns.
/// </summary>
public interface IChatCompletionCollector
{
    /// <summary>
    /// Drains all captured completions since the last drain. Thread-safe.
    /// Returns an empty list if no completions have been recorded.
    /// </summary>
    IReadOnlyList<ChatCompletionDiagnostics> DrainCompletions();
}
