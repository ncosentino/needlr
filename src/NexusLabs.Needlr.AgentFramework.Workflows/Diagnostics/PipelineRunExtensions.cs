using System.Diagnostics;
using System.Text;

using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

using ProgressEvents = NexusLabs.Needlr.AgentFramework.Progress;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;

/// <summary>
/// Extension methods for running workflows with per-stage diagnostics and real-time
/// progress reporting.
/// </summary>
public static class PipelineRunExtensions
{
    /// <summary>
    /// Executes the workflow with per-stage diagnostics (no progress reporting).
    /// </summary>
    public static Task<IPipelineRunResult> RunWithDiagnosticsAsync(
        this Workflow workflow,
        string message,
        IAgentDiagnosticsAccessor diagnosticsAccessor,
        CancellationToken cancellationToken = default) =>
        RunWithDiagnosticsAsync(workflow, message, new WorkflowRunOptions
        {
            DiagnosticsAccessor = diagnosticsAccessor,
            CancellationToken = cancellationToken,
        });

    /// <summary>
    /// Executes the workflow with per-stage diagnostics and real-time progress reporting.
    /// </summary>
    public static Task<IPipelineRunResult> RunWithDiagnosticsAsync(
        this Workflow workflow,
        string message,
        IAgentDiagnosticsAccessor diagnosticsAccessor,
        ProgressEvents.IProgressReporter? progressReporter,
        CancellationToken cancellationToken = default) =>
        RunWithDiagnosticsAsync(workflow, message, new WorkflowRunOptions
        {
            DiagnosticsAccessor = diagnosticsAccessor,
            ProgressReporter = progressReporter,
            CancellationToken = cancellationToken,
        });

    /// <summary>
    /// Executes the workflow with per-stage diagnostics, real-time progress reporting,
    /// and per-LLM-call completion draining via the provided collector.
    /// </summary>
    public static Task<IPipelineRunResult> RunWithDiagnosticsAsync(
        this Workflow workflow,
        string message,
        IAgentDiagnosticsAccessor diagnosticsAccessor,
        ProgressEvents.IProgressReporter? progressReporter,
        IChatCompletionCollector? completionCollector,
        ProgressEvents.IProgressReporterAccessor? progressReporterAccessor = null,
        CancellationToken cancellationToken = default) =>
        RunWithDiagnosticsAsync(workflow, message, new WorkflowRunOptions
        {
            DiagnosticsAccessor = diagnosticsAccessor,
            ProgressReporter = progressReporter,
            CompletionCollector = completionCollector,
            ProgressReporterAccessor = progressReporterAccessor,
            CancellationToken = cancellationToken,
        });

    /// <summary>
    /// Executes the workflow with per-stage diagnostics and progress reporting
    /// configured via <see cref="WorkflowRunOptions"/>.
    /// </summary>
    /// <param name="workflow">The workflow to execute.</param>
    /// <param name="message">The user message to send.</param>
    /// <param name="options">Configuration for diagnostics, progress, and completion collection.</param>
    public static async Task<IPipelineRunResult> RunWithDiagnosticsAsync(
        this Workflow workflow,
        string message,
        WorkflowRunOptions options)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentException.ThrowIfNullOrEmpty(message);
        ArgumentNullException.ThrowIfNull(options);

        var diagnosticsAccessor = options.DiagnosticsAccessor;
        var reporter = options.ProgressReporter ?? ProgressEvents.NullProgressReporter.Instance;
        var collector = options.CompletionCollector ?? NullChatCompletionCollector.Instance;
        var progressReporterAccessor = options.ProgressReporterAccessor;
        var cancellationToken = options.CancellationToken;
        var pipelineStart = Stopwatch.StartNew();
        var stages = new List<IAgentStageResult>();
        var responses = new Dictionary<string, StringBuilder>();
        string? currentExecutorId = null;
        var turnStopwatch = new Stopwatch();
        var turnStartedAt = DateTimeOffset.UtcNow;
        bool succeeded = true;
        string? errorMessage = null;
        int superStepCount = 0;

        collector.DrainCompletions(); // drain stale

        // Set the progress reporter on the AsyncLocal accessor so chat/tool middleware
        // can emit LLM call and tool call events in real-time.
        var progressScope = progressReporterAccessor?.BeginScope(reporter);

        reporter.Report(new ProgressEvents.WorkflowStartedEvent(
            Timestamp: DateTimeOffset.UtcNow,
            WorkflowId: reporter.WorkflowId,
            AgentId: null,
            ParentAgentId: null,
            Depth: 0,
            SequenceNumber: reporter.NextSequence()));

        try
        {
            using (diagnosticsAccessor.BeginCapture())
            {
                await using var run = await InProcessExecution.RunStreamingAsync(
                    workflow,
                    new ChatMessage(ChatRole.User, message),
                    cancellationToken: cancellationToken);

                await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

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
                    if (evt is ExecutorInvokedEvent invoked)
                    {
                        reporter.Report(new ProgressEvents.AgentInvokedEvent(
                            Timestamp: DateTimeOffset.UtcNow,
                            WorkflowId: reporter.WorkflowId,
                            AgentId: invoked.ExecutorId,
                            ParentAgentId: null,
                            Depth: 1,
                            SequenceNumber: reporter.NextSequence(),
                            AgentName: invoked.ExecutorId ?? "unknown"));
                        continue;
                    }

                    if (evt is ExecutorFailedEvent executorFailed)
                    {
                        succeeded = false;
                        errorMessage = executorFailed.Data?.Message;
                        reporter.Report(new ProgressEvents.AgentFailedEvent(
                            Timestamp: DateTimeOffset.UtcNow,
                            WorkflowId: reporter.WorkflowId,
                            AgentId: executorFailed.ExecutorId,
                            ParentAgentId: null,
                            Depth: 1,
                            SequenceNumber: reporter.NextSequence(),
                            AgentName: executorFailed.ExecutorId ?? "unknown",
                            ErrorMessage: executorFailed.Data?.Message ?? "unknown error"));
                        continue;
                    }

                    if (evt is WorkflowErrorEvent workflowError)
                    {
                        succeeded = false;
                        errorMessage = workflowError.Exception?.Message;
                        continue;
                    }

                    if (evt is SuperStepStartedEvent)
                    {
                        superStepCount++;
                        reporter.Report(new ProgressEvents.SuperStepStartedProgressEvent(
                            Timestamp: DateTimeOffset.UtcNow,
                            WorkflowId: reporter.WorkflowId,
                            AgentId: null,
                            ParentAgentId: null,
                            Depth: 0,
                            SequenceNumber: reporter.NextSequence(),
                            StepNumber: superStepCount));
                        continue;
                    }

                    if (evt is SuperStepCompletedEvent)
                    {
                        reporter.Report(new ProgressEvents.SuperStepCompletedProgressEvent(
                            Timestamp: DateTimeOffset.UtcNow,
                            WorkflowId: reporter.WorkflowId,
                            AgentId: null,
                            ParentAgentId: null,
                            Depth: 0,
                            SequenceNumber: reporter.NextSequence(),
                            StepNumber: superStepCount));
                        continue;
                    }

                    if (evt is not AgentResponseUpdateEvent update
                        || update.ExecutorId is null
                        || update.Data is null)
                    {
                        continue;
                    }

                    var text = update.Data.ToString();
                    if (string.IsNullOrEmpty(text))
                        continue;

                    // Turn boundary
                    if (currentExecutorId is not null
                        && currentExecutorId != update.ExecutorId
                        && responses.TryGetValue(currentExecutorId, out var completedSb))
                    {
                        turnStopwatch.Stop();
                        stages.Add(BuildStageResult(
                            currentExecutorId,
                            completedSb.ToString(),
                            diagnosticsAccessor,
                            collector,
                            turnStopwatch.Elapsed,
                            turnStartedAt));

                        reporter.Report(new ProgressEvents.AgentCompletedEvent(
                            Timestamp: DateTimeOffset.UtcNow,
                            WorkflowId: reporter.WorkflowId,
                            AgentId: currentExecutorId,
                            ParentAgentId: null,
                            Depth: 1,
                            SequenceNumber: reporter.NextSequence(),
                            AgentName: currentExecutorId,
                            Duration: turnStopwatch.Elapsed,
                            TotalTokens: stages[^1].Diagnostics?.AggregateTokenUsage.TotalTokens ?? 0));

                        reporter.Report(new ProgressEvents.AgentHandoffEvent(
                            Timestamp: DateTimeOffset.UtcNow,
                            WorkflowId: reporter.WorkflowId,
                            AgentId: null,
                            ParentAgentId: null,
                            Depth: 0,
                            SequenceNumber: reporter.NextSequence(),
                            FromAgentId: currentExecutorId,
                            ToAgentId: update.ExecutorId));

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
                        collector,
                        turnStopwatch.Elapsed,
                        turnStartedAt));

                    reporter.Report(new ProgressEvents.AgentCompletedEvent(
                        Timestamp: DateTimeOffset.UtcNow,
                        WorkflowId: reporter.WorkflowId,
                        AgentId: currentExecutorId,
                        ParentAgentId: null,
                        Depth: 1,
                        SequenceNumber: reporter.NextSequence(),
                        AgentName: currentExecutorId,
                        Duration: turnStopwatch.Elapsed,
                        TotalTokens: stages[^1].Diagnostics?.AggregateTokenUsage.TotalTokens ?? 0));
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

            // If we know which agent was running when the exception propagated
            // out of the stream, emit an AgentFailedEvent for it so sinks see
            // the per-agent failure before the trailing WorkflowCompletedEvent.
            if (currentExecutorId is not null)
            {
                reporter.Report(new ProgressEvents.AgentFailedEvent(
                    Timestamp: DateTimeOffset.UtcNow,
                    WorkflowId: reporter.WorkflowId,
                    AgentId: currentExecutorId,
                    ParentAgentId: null,
                    Depth: 1,
                    SequenceNumber: reporter.NextSequence(),
                    AgentName: currentExecutorId,
                    ErrorMessage: ex.Message));
            }
        }

        pipelineStart.Stop();

        reporter.Report(new ProgressEvents.WorkflowCompletedEvent(
            Timestamp: DateTimeOffset.UtcNow,
            WorkflowId: reporter.WorkflowId,
            AgentId: null,
            ParentAgentId: null,
            Depth: 0,
            SequenceNumber: reporter.NextSequence(),
            Succeeded: succeeded,
            ErrorMessage: errorMessage,
            TotalDuration: pipelineStart.Elapsed));

        progressScope?.Dispose();

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
        IChatCompletionCollector completionCollector,
        TimeSpan turnDuration,
        DateTimeOffset turnStartedAt)
    {
        var middlewareDiag = diagnosticsAccessor.LastRunDiagnostics;
        if (middlewareDiag is not null)
        {
            return new AgentStageResult(agentName, responseText, middlewareDiag);
        }

        var completions = completionCollector.DrainCompletions();

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
