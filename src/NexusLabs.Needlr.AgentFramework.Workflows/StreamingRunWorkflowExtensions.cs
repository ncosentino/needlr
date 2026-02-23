using Microsoft.Agents.AI.Workflows;

namespace NexusLabs.Needlr.AgentFramework.Workflows;

/// <summary>
/// Extension methods on <see cref="StreamingRun"/> for collecting agent responses.
/// </summary>
public static class StreamingRunWorkflowExtensions
{
    /// <summary>
    /// Collects all agent response text from a streaming run, grouped by executor ID.
    /// </summary>
    /// <returns>
    /// A dictionary mapping each agent's executor ID to its complete response text.
    /// Agents that emitted no text produce no entry.
    /// </returns>
    public static Task<IReadOnlyDictionary<string, string>> CollectAgentResponsesAsync(
        this StreamingRun run,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        return CollectFromEventsAsync(run.WatchStreamAsync(cancellationToken));
    }

    internal static async Task<IReadOnlyDictionary<string, string>> CollectFromEventsAsync(
        IAsyncEnumerable<WorkflowEvent> events)
    {
        var responses = new Dictionary<string, System.Text.StringBuilder>();

        await foreach (var evt in events)
        {
            if (evt is AgentResponseUpdateEvent update
                && update.ExecutorId is not null
                && update.Data is not null)
            {
                var text = update.Data.ToString();
                if (string.IsNullOrEmpty(text))
                    continue;

                if (!responses.TryGetValue(update.ExecutorId, out var sb))
                    responses[update.ExecutorId] = sb = new System.Text.StringBuilder();

                sb.Append(text);
            }
        }

        return responses.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.ToString());
    }
}
