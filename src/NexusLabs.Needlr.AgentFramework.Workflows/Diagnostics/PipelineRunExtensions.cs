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
        var collector = options.CompletionCollector
            ?? diagnosticsAccessor.CompletionCollector
            ?? NullChatCompletionCollector.Instance;
        var toolCollector = diagnosticsAccessor.ToolCallCollector;
        var progressReporterAccessor = options.ProgressReporterAccessor;
        var cancellationToken = options.CancellationToken;
        var pipelineStart = Stopwatch.StartNew();
        var stages = new List<IAgentStageResult>();
        var responses = new Dictionary<string, StringBuilder>();
        var invocations = new List<(string ExecutorId, DateTimeOffset InvokedAt)>();
        string? currentExecutorId = null;
        bool succeeded = true;
        string? errorMessage = null;
        Exception? caughtException = null;
        int superStepCount = 0;

        collector.DrainCompletions(); // drain stale
        toolCollector?.DrainToolCalls(); // drain stale

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
                        var invokedId = invoked.ExecutorId ?? "unknown";
                        invocations.Add((invokedId, DateTimeOffset.UtcNow));

                        reporter.Report(new ProgressEvents.AgentInvokedEvent(
                            Timestamp: DateTimeOffset.UtcNow,
                            WorkflowId: reporter.WorkflowId,
                            AgentId: invokedId,
                            ParentAgentId: null,
                            Depth: 1,
                            SequenceNumber: reporter.NextSequence(),
                            AgentName: invokedId));
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

                    // Emit handoff progress event at turn boundaries
                    if (currentExecutorId is not null
                        && currentExecutorId != update.ExecutorId)
                    {
                        reporter.Report(new ProgressEvents.AgentHandoffEvent(
                            Timestamp: DateTimeOffset.UtcNow,
                            WorkflowId: reporter.WorkflowId,
                            AgentId: null,
                            ParentAgentId: null,
                            Depth: 0,
                            SequenceNumber: reporter.NextSequence(),
                            FromAgentId: currentExecutorId,
                            ToAgentId: update.ExecutorId));
                    }

                    currentExecutorId = update.ExecutorId;

                    if (!responses.TryGetValue(update.ExecutorId, out var sb))
                        responses[update.ExecutorId] = sb = new StringBuilder();

                    sb.Append(text);
                }

                // Drain all completions and partition them across agent stages.
                // Event loop timestamps are unreliable (events are buffered), so we use
                // completion timestamps (captured at actual LLM call time) for both
                // duration calculation and agent attribution.
                var allCompletions = collector.DrainCompletions()
                    .OrderBy(c => c.StartedAt)
                    .ToList();

                // Drain tool calls from the collector for the fallback path.
                var allToolCalls = toolCollector?.DrainToolCalls()
                    ?.OrderBy(t => t.StartedAt)
                    .ToList()
                    ?? [];

                // Filter invocations to only real agents (have response text or completions
                // attributed by name). Skips non-agent executors like "GroupChatHost".
                var agentInvocations = invocations
                    .Where(inv => responses.ContainsKey(inv.ExecutorId))
                    .Select(inv => inv.ExecutorId)
                    .Distinct()
                    .ToList();

                // Partition completions by agent using name matching or temporal gaps.
                var partitioned = PartitionCompletionsByAgent(
                    allCompletions, agentInvocations);

                // Partition tool calls by agent using AgentName attribution.
                var partitionedToolCalls = PartitionToolCallsByAgent(
                    allToolCalls, agentInvocations);

                for (int i = 0; i < agentInvocations.Count; i++)
                {
                    var executorId = agentInvocations[i];
                    var stageCompletions = i < partitioned.Count
                        ? partitioned[i] : [];
                    var stageToolCalls = i < partitionedToolCalls.Count
                        ? partitionedToolCalls[i] : [];

                    var responseText = responses.TryGetValue(executorId, out var respSb)
                        ? respSb.ToString()
                        : string.Empty;

                    // Duration from completion timestamps (reliable), not event timestamps.
                    var duration = stageCompletions.Count > 0
                        ? stageCompletions[^1].CompletedAt - stageCompletions[0].StartedAt
                        : TimeSpan.Zero;
                    var startedAt = stageCompletions.Count > 0
                        ? stageCompletions[0].StartedAt
                        : DateTimeOffset.UtcNow;

                    stages.Add(BuildStageResultFromCompletions(
                        executorId,
                        responseText,
                        diagnosticsAccessor,
                        stageCompletions,
                        stageToolCalls,
                        duration,
                        startedAt));

                    reporter.Report(new ProgressEvents.AgentCompletedEvent(
                        Timestamp: DateTimeOffset.UtcNow,
                        WorkflowId: reporter.WorkflowId,
                        AgentId: executorId,
                        ParentAgentId: null,
                        Depth: 1,
                        SequenceNumber: reporter.NextSequence(),
                        AgentName: executorId,
                        Duration: duration,
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
            errorMessage = ex.ToString();
            caughtException = ex;

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
            errorMessage: errorMessage,
            exception: caughtException);
    }

    private static IAgentStageResult BuildStageResultFromCompletions(
        string agentName,
        string responseText,
        IAgentDiagnosticsAccessor diagnosticsAccessor,
        IReadOnlyList<ChatCompletionDiagnostics> completions,
        IReadOnlyList<ToolCallDiagnostics> toolCalls,
        TimeSpan turnDuration,
        DateTimeOffset turnStartedAt)
    {
        var finalResponse = string.IsNullOrEmpty(responseText)
            ? null
            : new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText));

        var middlewareDiag = diagnosticsAccessor.LastRunDiagnostics;
        if (middlewareDiag is not null)
        {
            return new AgentStageResult(agentName, finalResponse, middlewareDiag);
        }

        var totalTokens = new TokenUsage(
            InputTokens: completions.Sum(c => c.Tokens.InputTokens),
            OutputTokens: completions.Sum(c => c.Tokens.OutputTokens),
            TotalTokens: completions.Sum(c => c.Tokens.TotalTokens),
            CachedInputTokens: completions.Sum(c => c.Tokens.CachedInputTokens),
            ReasoningTokens: completions.Sum(c => c.Tokens.ReasoningTokens));

        return new AgentStageResult(
            agentName,
            finalResponse,
            new AgentRunDiagnostics(
                AgentName: agentName,
                TotalDuration: turnDuration,
                AggregateTokenUsage: totalTokens,
                ChatCompletions: completions,
                ToolCalls: toolCalls,
                TotalInputMessages: 0,
                TotalOutputMessages: 0,
                Succeeded: true,
                ErrorMessage: null,
                StartedAt: turnStartedAt,
                CompletedAt: DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Partitions an ordered list of completions into groups, one per agent invocation.
    /// First attempts name-based matching. When completions lack agent names, uses temporal
    /// gap analysis: finds the N-1 largest gaps between consecutive completions (where N is
    /// the agent count) and splits at those boundaries.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<ChatCompletionDiagnostics>> PartitionCompletionsByAgent(
        List<ChatCompletionDiagnostics> sorted,
        IReadOnlyList<string> agentExecutorIds)
    {
        if (sorted.Count == 0 || agentExecutorIds.Count == 0)
            return agentExecutorIds.Select(_ => (IReadOnlyList<ChatCompletionDiagnostics>)[]).ToList();

        // Try name-based partitioning first.
        var byName = new List<IReadOnlyList<ChatCompletionDiagnostics>>();
        bool allMatched = true;
        foreach (var executorId in agentExecutorIds)
        {
            var matched = sorted
                .Where(c => c.AgentName is not null
                    && (executorId.Equals(c.AgentName, StringComparison.Ordinal)
                        || executorId.StartsWith(c.AgentName + "_", StringComparison.Ordinal)))
                .ToList();
            byName.Add(matched);
            if (matched.Count == 0)
                allMatched = false;
        }

        if (allMatched)
            return byName;

        // Fall back to round-robin interleaving: in a RoundRobinGroupChatManager,
        // agents alternate turns. Completion[i] belongs to agent[i % N].
        var interleaved = agentExecutorIds
            .Select(_ => new List<ChatCompletionDiagnostics>())
            .ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            interleaved[i % agentExecutorIds.Count].Add(sorted[i]);
        }

        return interleaved.Select(l => (IReadOnlyList<ChatCompletionDiagnostics>)l).ToList();
    }

    /// <summary>
    /// Partitions tool calls by agent using the <see cref="ToolCallDiagnostics.AgentName"/>
    /// field. Tool calls without an agent name are distributed to the first agent bucket.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<ToolCallDiagnostics>> PartitionToolCallsByAgent(
        List<ToolCallDiagnostics> sorted,
        IReadOnlyList<string> agentExecutorIds)
    {
        if (sorted.Count == 0 || agentExecutorIds.Count == 0)
            return agentExecutorIds.Select(_ => (IReadOnlyList<ToolCallDiagnostics>)[]).ToList();

        var buckets = agentExecutorIds
            .Select(_ => new List<ToolCallDiagnostics>())
            .ToList();

        foreach (var tc in sorted)
        {
            var matched = false;
            for (int i = 0; i < agentExecutorIds.Count; i++)
            {
                if (tc.AgentName is not null
                    && (agentExecutorIds[i].Equals(tc.AgentName, StringComparison.Ordinal)
                        || agentExecutorIds[i].StartsWith(tc.AgentName + "_", StringComparison.Ordinal)))
                {
                    buckets[i].Add(tc);
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                buckets[0].Add(tc);
            }
        }

        return buckets.Select(l => (IReadOnlyList<ToolCallDiagnostics>)l).ToList();
    }
}
