using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Immutable view of diagnostics captured during a single agent run, including token usage,
/// per-call timing, tool call details, and success/failure state.
/// </summary>
/// <remarks>
/// <para>
/// Captured automatically when <c>UsingDiagnostics()</c> is called on the
/// <see cref="AgentFrameworkSyringe"/>. Access via
/// <see cref="IAgentDiagnosticsAccessor.LastRunDiagnostics"/> after an agent run,
/// or via <see cref="IAgentStageResult.Diagnostics"/> for pipeline/group-chat stages.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using (diagnosticsAccessor.BeginCapture())
/// {
///     await agent.RunAsync("Summarize this document.", cancellationToken: ct);
///     var diag = diagnosticsAccessor.LastRunDiagnostics!;
///
///     Console.WriteLine($"Agent: {diag.AgentName}");
///     Console.WriteLine($"Duration: {diag.TotalDuration.TotalMilliseconds}ms");
///     Console.WriteLine($"Tokens: {diag.AggregateTokenUsage.TotalTokens}");
///     Console.WriteLine($"LLM calls: {diag.ChatCompletions.Count}");
///     Console.WriteLine($"Tool calls: {diag.ToolCalls.Count}");
/// }
/// </code>
/// </example>
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

    /// <summary>
    /// Gets the full input messages provided to the agent for this run. Captured once
    /// at the agent-run boundary so evaluators can replay the exact prompt without
    /// re-reading per-call chat completion snapshots. Empty when no messages were
    /// captured (e.g., the run failed before the middleware observed the input).
    /// </summary>
    IReadOnlyList<ChatMessage> InputMessages { get; }

    /// <summary>
    /// Gets the aggregated output response produced by the agent for this run, or
    /// <see langword="null"/> when no output was produced (e.g., the run failed
    /// before any updates were emitted). For streaming runs this is synthesized
    /// from accumulated <see cref="AgentResponseUpdate"/> items grouped by message.
    /// </summary>
    AgentResponse? OutputResponse { get; }

    /// <summary>Gets whether the agent run completed successfully.</summary>
    bool Succeeded { get; }

    /// <summary>Gets the error message if the run failed; <see langword="null"/> on success.</summary>
    string? ErrorMessage { get; }

    /// <summary>Gets when the agent run started.</summary>
    DateTimeOffset StartedAt { get; }

    /// <summary>Gets when the agent run completed.</summary>
    DateTimeOffset CompletedAt { get; }

    /// <summary>
    /// Gets the execution mode that produced these diagnostics, or
    /// <see langword="null"/> if unknown. Known values:
    /// <c>"FunctionInvokingChatClient"</c>, <c>"IterativeLoop"</c>.
    /// </summary>
    string? ExecutionMode { get; }
}
