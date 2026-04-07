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
    /// diagnostics captured at each turn boundary.
    /// </summary>
    /// <param name="workflow">The workflow to execute.</param>
    /// <param name="message">The user message to send.</param>
    /// <param name="diagnosticsAccessor">
    /// The diagnostics accessor used to read per-agent diagnostics after each turn completes.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="IPipelineRunResult"/> containing per-stage responses, diagnostics, and
    /// aggregate token usage.
    /// </returns>
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
        bool succeeded = true;
        string? errorMessage = null;

        try
        {
            await using var run = await InProcessExecution.RunStreamingAsync(
                workflow,
                new ChatMessage(ChatRole.User, message),
                cancellationToken: cancellationToken);

            await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

            using (diagnosticsAccessor.BeginCapture())
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

                    // Turn boundary: capture diagnostics for the completing agent
                    if (currentExecutorId is not null
                        && currentExecutorId != update.ExecutorId
                        && responses.TryGetValue(currentExecutorId, out var completedSb))
                    {
                        stages.Add(new AgentStageResult(
                            AgentName: currentExecutorId,
                            ResponseText: completedSb.ToString(),
                            Diagnostics: diagnosticsAccessor.LastRunDiagnostics));
                    }

                    currentExecutorId = update.ExecutorId;

                    if (!responses.TryGetValue(update.ExecutorId, out var sb))
                        responses[update.ExecutorId] = sb = new StringBuilder();

                    sb.Append(text);
                }

                // Capture the last agent's diagnostics
                if (currentExecutorId is not null
                    && responses.TryGetValue(currentExecutorId, out var lastSb))
                {
                    stages.Add(new AgentStageResult(
                        AgentName: currentExecutorId,
                        ResponseText: lastSb.ToString(),
                        Diagnostics: diagnosticsAccessor.LastRunDiagnostics));
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
}
