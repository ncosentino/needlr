using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using NexusLabs.Needlr.AgentFramework;

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
    /// Creates a streaming execution of the workflow, sends the message, collects all agent
    /// responses, and stops early when any termination condition is met.
    /// </summary>
    /// <param name="workflow">The workflow to execute.</param>
    /// <param name="message">The user message to send to the workflow.</param>
    /// <param name="terminationConditions">
    /// Conditions evaluated after each completed agent turn. The first condition that returns
    /// <see langword="true"/> causes the loop to stop and remaining responses to be discarded.
    /// Pass an empty collection (or <see langword="null"/>) to disable Layer 2 termination.
    /// </param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    /// A dictionary mapping each agent's executor ID to its complete response text up to the
    /// point of termination. Agents that emitted no text produce no entry.
    /// </returns>
    public static async Task<IReadOnlyDictionary<string, string>> RunAsync(
        this Workflow workflow,
        string message,
        IReadOnlyList<IWorkflowTerminationCondition>? terminationConditions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentException.ThrowIfNullOrEmpty(message);
        return await workflow.RunAsync(
            new ChatMessage(ChatRole.User, message),
            terminationConditions,
            cancellationToken);
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
    /// Creates a streaming execution of the workflow, sends the message, collects all agent
    /// responses, and stops early when any termination condition is met.
    /// </summary>
    /// <param name="workflow">The workflow to execute.</param>
    /// <param name="message">The chat message to send to the workflow.</param>
    /// <param name="terminationConditions">
    /// Conditions evaluated after each completed agent turn. The first condition that returns
    /// <see langword="true"/> causes the loop to stop and remaining responses to be discarded.
    /// Pass an empty collection (or <see langword="null"/>) to disable Layer 2 termination.
    /// </param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    /// A dictionary mapping each agent's executor ID to its complete response text up to the
    /// point of termination. Agents that emitted no text produce no entry.
    /// </returns>
    public static async Task<IReadOnlyDictionary<string, string>> RunAsync(
        this Workflow workflow,
        ChatMessage message,
        IReadOnlyList<IWorkflowTerminationCondition>? terminationConditions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(message);
        await using var run = await InProcessExecution.RunStreamingAsync(workflow, message, cancellationToken: cancellationToken);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        if (terminationConditions is null || terminationConditions.Count == 0)
            return await run.CollectAgentResponsesAsync(cancellationToken);

        return await CollectWithTerminationAsync(
            run.WatchStreamAsync(cancellationToken),
            terminationConditions,
            cancellationToken);
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

    internal static async Task<IReadOnlyDictionary<string, string>> CollectWithTerminationAsync(
        IAsyncEnumerable<WorkflowEvent> events,
        IReadOnlyList<IWorkflowTerminationCondition> conditions,
        CancellationToken cancellationToken)
    {
        var responses = new Dictionary<string, System.Text.StringBuilder>();
        var history = new List<ChatMessage>();
        var turnCount = 0;

        string? currentExecutorId = null;

        await foreach (var evt in events.WithCancellation(cancellationToken))
        {
            if (evt is not AgentResponseUpdateEvent update
                || update.ExecutorId is null
                || update.Data is null)
            {
                continue;
            }

            var text = update.Data.ToString();
            if (string.IsNullOrEmpty(text))
                continue;

            // Detect executor change — previous agent's turn is complete
            if (currentExecutorId is not null
                && currentExecutorId != update.ExecutorId
                && responses.TryGetValue(currentExecutorId, out var completedSb))
            {
                var responseText = completedSb.ToString();
                turnCount++;
                history.Add(new ChatMessage(ChatRole.Assistant, responseText));

                var ctx = new TerminationContext
                {
                    AgentId = currentExecutorId,
                    ResponseText = responseText,
                    TurnCount = turnCount,
                    ConversationHistory = history,
                };

                if (ShouldTerminate(ctx, conditions))
                    return FinalizeResponses(responses);
            }

            currentExecutorId = update.ExecutorId;

            if (!responses.TryGetValue(update.ExecutorId, out var sb))
                responses[update.ExecutorId] = sb = new System.Text.StringBuilder();

            sb.Append(text);
        }

        // Check the last agent's turn
        if (currentExecutorId is not null
            && responses.TryGetValue(currentExecutorId, out var lastSb))
        {
            var responseText = lastSb.ToString();
            turnCount++;
            history.Add(new ChatMessage(ChatRole.Assistant, responseText));

            var ctx = new TerminationContext
            {
                AgentId = currentExecutorId,
                ResponseText = responseText,
                TurnCount = turnCount,
                ConversationHistory = history,
            };

            ShouldTerminate(ctx, conditions); // evaluate but don't stop — stream already ended
        }

        return FinalizeResponses(responses);
    }

    private static bool ShouldTerminate(
        TerminationContext ctx,
        IReadOnlyList<IWorkflowTerminationCondition> conditions)
    {
        foreach (var condition in conditions)
        {
            if (condition.ShouldTerminate(ctx))
                return true;
        }
        return false;
    }

    private static IReadOnlyDictionary<string, string> FinalizeResponses(
        Dictionary<string, System.Text.StringBuilder> responses)
        => responses.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
}

