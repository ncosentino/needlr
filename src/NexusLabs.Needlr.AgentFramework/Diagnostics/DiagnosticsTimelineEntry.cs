namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// A single entry in an agent run's ordered timeline of chat completions and tool calls.
/// </summary>
/// <remarks>
/// <para>
/// Timeline entries are constructed by
/// <see cref="AgentRunDiagnosticsTimelineExtensions.GetOrderedTimeline(IAgentRunDiagnostics)"/>,
/// which merges and orders the <see cref="IAgentRunDiagnostics.ChatCompletions"/> and
/// <see cref="IAgentRunDiagnostics.ToolCalls"/> collections by wall-clock time. Use the
/// <see cref="ChatCompletion"/> or <see cref="ToolCall"/> property corresponding to
/// <see cref="Kind"/> to access the original diagnostics record.
/// </para>
/// </remarks>
/// <param name="Kind">Whether this entry represents a chat completion or a tool call.</param>
/// <param name="Sequence">The invocation sequence within the source collection.</param>
/// <param name="StartedAt">UTC timestamp when the operation began.</param>
/// <param name="CompletedAt">UTC timestamp when the operation finished.</param>
/// <param name="ChatCompletion">The source chat-completion record when <see cref="Kind"/> is <see cref="DiagnosticsTimelineEntryKind.ChatCompletion"/>; otherwise <see langword="null"/>.</param>
/// <param name="ToolCall">The source tool-call record when <see cref="Kind"/> is <see cref="DiagnosticsTimelineEntryKind.ToolCall"/>; otherwise <see langword="null"/>.</param>
public sealed record DiagnosticsTimelineEntry(
    DiagnosticsTimelineEntryKind Kind,
    int Sequence,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    ChatCompletionDiagnostics? ChatCompletion,
    ToolCallDiagnostics? ToolCall);
