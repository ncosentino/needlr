using System.Diagnostics;
using System.Text;

using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;

/// <summary>
/// Extension methods for running workflows with per-stage diagnostics aggregation.
/// </summary>
public static class PipelineRunExtensions
{
    /// <summary>
    /// Executes the workflow and returns an <see cref="IPipelineRunResult"/> with per-stage
    /// diagnostics including per-LLM-call timing from the chat client middleware.
    /// </summary>
    public static async Task<IPipelineRunResult> RunWithDiagnosticsAsync(
        this Workflow workflow,
        string message,
        IAgentDiagnosticsAccessor diagnosticsAccessor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentException.ThrowIfNullOrEmpty(message);
        ArgumentNullException.ThrowIfNull(diagnosticsAccessor);

        var pipelineStart = Stopwatch.StartNew();
        var stages = new List<IAgentStageResult>();
        var responses = new Dictionary<string, StringBuilder>();
        string? currentExecutorId = null;
        var turnStopwatch = new Stopwatch();
        var turnStartedAt = DateTimeOffset.UtcNow;
        bool succeeded = true;
        string? errorMessage = null;

        // Drain any stale completions from previous runs
        ChatMiddlewareHolder.Instance?.DrainCompletions();

        try
        {
            using (diagnosticsAccessor.BeginCapture())
            {
                await using var run = await InProcessExecution.RunStreamingAsync(
                    workflow,
                    new ChatMessage(ChatRole.User, message),
                    cancellationToken: cancellationToken);

                await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

                // If a budget cancellation token is active, register a callback
                // that calls CancelRunAsync when the budget is exceeded.
                CancellationTokenRegistration? budgetRegistration = null;
                if (cancellationToken.CanBeCanceled)
                {
                    budgetRegistration = cancellationToken.Register(() =>
                    {
                        _ = run.CancelRunAsync();
                    });
                }

                try
                {
                await foreach (var evt in run.WatchStreamAsync(cancellationToken))
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

                    // Turn boundary: capture stage for the completing agent
                    if (currentExecutorId is not null
                        && currentExecutorId != update.ExecutorId
                        && responses.TryGetValue(currentExecutorId, out var completedSb))
                    {
                        turnStopwatch.Stop();
                        stages.Add(BuildStageResult(
                            currentExecutorId,
                            completedSb.ToString(),
                            diagnosticsAccessor,
                            turnStopwatch.Elapsed,
                            turnStartedAt));

                        turnStopwatch.Restart();
                        turnStartedAt = DateTimeOffset.UtcNow;
                    }

                    if (currentExecutorId != update.ExecutorId)
                    {
                        turnStopwatch.Restart();
                        turnStartedAt = DateTimeOffset.UtcNow;
                    }

                    currentExecutorId = update.ExecutorId;

                    if (!responses.TryGetValue(update.ExecutorId, out var sb))
                        responses[update.ExecutorId] = sb = new StringBuilder();

                    sb.Append(text);
                }

                // Capture the last agent's stage
                if (currentExecutorId is not null
                    && responses.TryGetValue(currentExecutorId, out var lastSb))
                {
                    turnStopwatch.Stop();
                    stages.Add(BuildStageResult(
                        currentExecutorId,
                        lastSb.ToString(),
                        diagnosticsAccessor,
                        turnStopwatch.Elapsed,
                        turnStartedAt));
                }
                }
                finally
                {
                    budgetRegistration?.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            succeeded = false;
            errorMessage = ex.Message;
        }

        pipelineStart.Stop();

        return new PipelineRunResult(
            stages: stages,
            totalDuration: pipelineStart.Elapsed,
            succeeded: succeeded,
            errorMessage: errorMessage);
    }

    private static IAgentStageResult BuildStageResult(
        string agentName,
        string responseText,
        IAgentDiagnosticsAccessor diagnosticsAccessor,
        TimeSpan turnDuration,
        DateTimeOffset turnStartedAt)
    {
        // Try middleware AsyncLocal diagnostics first (works for direct agent runs)
        var middlewareDiag = diagnosticsAccessor.LastRunDiagnostics;
        if (middlewareDiag is not null)
        {
            return new AgentStageResult(agentName, responseText, middlewareDiag);
        }

        // Fallback: drain per-LLM-call completions from the chat client middleware.
        // This works regardless of AsyncLocal propagation because the chat middleware
        // captures ALL calls on its ConcurrentQueue.
        var completions = ChatMiddlewareHolder.Instance?.DrainCompletions() ?? [];

        var totalTokens = new TokenUsage(
            InputTokens: completions.Sum(c => c.Tokens.InputTokens),
            OutputTokens: completions.Sum(c => c.Tokens.OutputTokens),
            TotalTokens: completions.Sum(c => c.Tokens.TotalTokens),
            CachedInputTokens: completions.Sum(c => c.Tokens.CachedInputTokens),
            ReasoningTokens: completions.Sum(c => c.Tokens.ReasoningTokens));

        return new AgentStageResult(
            agentName,
            responseText,
            new AgentRunDiagnostics(
                AgentName: agentName,
                TotalDuration: turnDuration,
                AggregateTokenUsage: totalTokens,
                ChatCompletions: completions,
                ToolCalls: [],
                TotalInputMessages: 0,
                TotalOutputMessages: 0,
                Succeeded: true,
                ErrorMessage: null,
                StartedAt: turnStartedAt,
                CompletedAt: DateTimeOffset.UtcNow));
    }
}
