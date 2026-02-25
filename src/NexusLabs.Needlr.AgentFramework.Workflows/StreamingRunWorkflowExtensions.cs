using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Workflows;

/// <summary>
/// Extension methods on <see cref="StreamingRun"/> and <see cref="Workflow"/> for collecting agent responses.
/// </summary>
public static class StreamingRunWorkflowExtensions
{
    /// <summary>
    /// Creates a streaming execution of the workflow, sends the message, and collects all agent responses.
    /// </summary>
    /// <param name="workflow">The workflow to execute.</param>
    /// <param name="message">The user message to send to the workflow.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    /// A dictionary mapping each agent's executor ID to its complete response text.
    /// Agents that emitted no text produce no entry.
    /// </returns>
    public static async Task<IReadOnlyDictionary<string, string>> RunAsync(
        this Workflow workflow,
        string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentException.ThrowIfNullOrEmpty(message);
        return await workflow.RunAsync(new ChatMessage(ChatRole.User, message), cancellationToken);
    }

    /// <summary>
    /// Creates a streaming execution of the workflow, sends the message, and collects all agent responses.
    /// </summary>
    /// <param name="workflow">The workflow to execute.</param>
    /// <param name="message">The chat message to send to the workflow.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    /// A dictionary mapping each agent's executor ID to its complete response text.
    /// Agents that emitted no text produce no entry.
    /// </returns>
    public static async Task<IReadOnlyDictionary<string, string>> RunAsync(
        this Workflow workflow,
        ChatMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(message);
        await using var run = await InProcessExecution.RunStreamingAsync(workflow, message, cancellationToken: cancellationToken);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
        return await run.CollectAgentResponsesAsync(cancellationToken);
    }

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
