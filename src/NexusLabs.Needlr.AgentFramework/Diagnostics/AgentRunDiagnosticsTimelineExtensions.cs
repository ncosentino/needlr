namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Extensions for producing ordered timeline views of an <see cref="IAgentRunDiagnostics"/>.
/// </summary>
public static class AgentRunDiagnosticsTimelineExtensions
{
    /// <summary>
    /// Merges the chat completions and tool calls into a single list ordered by
    /// <see cref="DiagnosticsTimelineEntry.StartedAt"/> (ascending), with
    /// <see cref="DiagnosticsTimelineEntry.Sequence"/> as a tiebreaker within the same
    /// kind.
    /// </summary>
    /// <param name="diagnostics">The agent run diagnostics to project.</param>
    /// <returns>A snapshot list of timeline entries. Never <see langword="null"/>; may be empty.</returns>
    public static IReadOnlyList<DiagnosticsTimelineEntry> GetOrderedTimeline(
        this IAgentRunDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        var entries = new List<DiagnosticsTimelineEntry>(
            diagnostics.ChatCompletions.Count + diagnostics.ToolCalls.Count);

        foreach (var chat in diagnostics.ChatCompletions)
        {
            entries.Add(new DiagnosticsTimelineEntry(
                Kind: DiagnosticsTimelineEntryKind.ChatCompletion,
                Sequence: chat.Sequence,
                StartedAt: chat.StartedAt,
                CompletedAt: chat.CompletedAt,
                ChatCompletion: chat,
                ToolCall: null));
        }

        foreach (var tool in diagnostics.ToolCalls)
        {
            entries.Add(new DiagnosticsTimelineEntry(
                Kind: DiagnosticsTimelineEntryKind.ToolCall,
                Sequence: tool.Sequence,
                StartedAt: tool.StartedAt,
                CompletedAt: tool.CompletedAt,
                ChatCompletion: null,
                ToolCall: tool));
        }

        entries.Sort(static (a, b) =>
        {
            var startedComparison = a.StartedAt.CompareTo(b.StartedAt);
            if (startedComparison != 0)
            {
                return startedComparison;
            }

            if (a.Kind == b.Kind)
            {
                return a.Sequence.CompareTo(b.Sequence);
            }

            return a.Kind == DiagnosticsTimelineEntryKind.ChatCompletion ? -1 : 1;
        });

        return entries;
    }
}
