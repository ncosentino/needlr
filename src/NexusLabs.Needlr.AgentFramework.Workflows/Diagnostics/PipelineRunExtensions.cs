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
    /// timing captured at each turn boundary from the event stream.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Per-stage diagnostics come from two sources:
    /// <list type="number">
    ///   <item>
    ///     <see cref="IAgentDiagnosticsAccessor.LastRunDiagnostics"/> — set by middleware
    ///     if AsyncLocal propagation works through the workflow execution context.
    ///   </item>
    ///   <item>
    ///     Event-stream timing — wall-clock duration measured from when each agent's first
    ///     event arrives to when the next agent's first event arrives (or stream ends).
    ///     This always works regardless of AsyncLocal propagation.
    ///   </item>
    /// </list>
    /// </para>
    /// </remarks>
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

        try
        {
            using (diagnosticsAccessor.BeginCapture())
            {
                await using var run = await InProcessExecution.RunStreamingAsync(
                    workflow,
                    new ChatMessage(ChatRole.User, message),
                    cancellationToken: cancellationToken);

                await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

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
                        var middlewareDiag = diagnosticsAccessor.LastRunDiagnostics;

                        stages.Add(new AgentStageResult(
                            AgentName: currentExecutorId,
                            ResponseText: completedSb.ToString(),
                            Diagnostics: middlewareDiag ?? BuildFallbackDiagnostics(
                                currentExecutorId, turnStopwatch.Elapsed, turnStartedAt)));

                        // Reset for next turn
                        turnStopwatch.Restart();
                        turnStartedAt = DateTimeOffset.UtcNow;
                    }

                    // First event for a new agent — start timing
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
                    var middlewareDiag = diagnosticsAccessor.LastRunDiagnostics;

                    stages.Add(new AgentStageResult(
                        AgentName: currentExecutorId,
                        ResponseText: lastSb.ToString(),
                        Diagnostics: middlewareDiag ?? BuildFallbackDiagnostics(
                            currentExecutorId, turnStopwatch.Elapsed, turnStartedAt)));
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

    /// <summary>
    /// Builds a minimal diagnostics record from event-stream timing when the middleware's
    /// AsyncLocal diagnostics didn't propagate through the workflow execution context.
    /// </summary>
    private static IAgentRunDiagnostics BuildFallbackDiagnostics(
        string agentName, TimeSpan duration, DateTimeOffset startedAt) =>
        new AgentRunDiagnostics(
            AgentName: agentName,
            TotalDuration: duration,
            AggregateTokenUsage: new TokenUsage(0, 0, 0, 0, 0),
            ChatCompletions: [],
            ToolCalls: [],
            TotalInputMessages: 0,
            TotalOutputMessages: 0,
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: startedAt,
            CompletedAt: DateTimeOffset.UtcNow);
}
