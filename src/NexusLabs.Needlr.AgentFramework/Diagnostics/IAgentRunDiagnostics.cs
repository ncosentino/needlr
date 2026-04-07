namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Immutable view of diagnostics captured during a single agent run, including token usage,
/// per-call timing, tool call details, and success/failure state.
/// </summary>
public interface IAgentRunDiagnostics
{
    /// <summary>Gets the name of the agent that ran.</summary>
    string AgentName { get; }

    /// <summary>Gets the total wall-clock duration of the agent run.</summary>
    TimeSpan TotalDuration { get; }

    /// <summary>Gets the aggregate token usage across all LLM calls in this run.</summary>
    TokenUsage AggregateTokenUsage { get; }

    /// <summary>Gets per-call diagnostics for each LLM chat completion, ordered by invocation time.</summary>
    IReadOnlyList<ChatCompletionDiagnostics> ChatCompletions { get; }

    /// <summary>Gets per-call diagnostics for each tool invocation, ordered by invocation time.</summary>
    IReadOnlyList<ToolCallDiagnostics> ToolCalls { get; }

    /// <summary>Gets the number of input messages provided to the agent.</summary>
    int TotalInputMessages { get; }

    /// <summary>Gets the number of output messages produced by the agent.</summary>
    int TotalOutputMessages { get; }

    /// <summary>Gets whether the agent run completed successfully.</summary>
    bool Succeeded { get; }

    /// <summary>Gets the error message if the run failed; <see langword="null"/> on success.</summary>
    string? ErrorMessage { get; }

    /// <summary>Gets when the agent run started.</summary>
    DateTimeOffset StartedAt { get; }

    /// <summary>Gets when the agent run completed.</summary>
    DateTimeOffset CompletedAt { get; }
}
