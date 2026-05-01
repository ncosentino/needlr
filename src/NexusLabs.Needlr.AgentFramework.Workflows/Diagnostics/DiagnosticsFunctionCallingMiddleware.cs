using System.Collections.Concurrent;
using System.Diagnostics;

using Microsoft.Agents.AI;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Progress;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;

/// <summary>
/// Innermost middleware layer: wraps each tool/function invocation to capture per-call
/// timing and custom metrics. Emits <see cref="ToolCallStartedEvent"/> and
/// <see cref="ToolCallCompletedEvent"/> to the progress reporter in real-time.
/// </summary>
internal static class DiagnosticsFunctionCallingMiddleware
{
    internal static void Wire(
        AIAgentBuilder builder,
        IAgentMetrics metrics,
        IProgressReporterAccessor progressAccessor,
        IToolCallCollector? toolCallCollector = null)
    {
        FunctionInvocationDelegatingAgentBuilderExtensions.Use(
            builder,
            async (agent, context, next, cancellationToken) =>
            {
                var diagnosticsBuilder = AgentRunDiagnosticsBuilder.GetCurrent();
                var sequence = diagnosticsBuilder?.NextToolCallSequence() ?? -1;
                var startedAt = DateTimeOffset.UtcNow;
                var stopwatch = Stopwatch.StartNew();

                var toolName = context.Function?.Name ?? "unknown";

                using var activity = metrics.ActivitySource.StartActivity($"agent.tool {toolName}", ActivityKind.Internal);
                activity?.SetTag("agent.tool.name", toolName);
                activity?.SetTag("agent.tool.sequence", sequence);
                activity?.SetTag("gen_ai.agent.name", diagnosticsBuilder?.AgentName);

                progressAccessor.Current.Report(new ToolCallStartedEvent(
                    Timestamp: startedAt,
                    WorkflowId: progressAccessor.Current.WorkflowId,
                    AgentId: progressAccessor.Current.AgentId,
                    ParentAgentId: diagnosticsBuilder?.ParentAgentName,
                    Depth: progressAccessor.Current.Depth,
                    SequenceNumber: progressAccessor.Current.NextSequence(),
                    ToolName: toolName));

                var customMetrics = new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                ToolMetricsAccessor.CurrentToolMetrics.Value = customMetrics;

                try
                {
                    var result = await next(context, cancellationToken).ConfigureAwait(false);
                    stopwatch.Stop();

                    activity?.SetTag("status", "success");

                    if (activity is not null && customMetrics.Count > 0)
                    {
                        foreach (var (key, value) in customMetrics)
                            activity.SetTag($"tool.custom.{key}", value);
                    }

                    metrics.RecordToolCall(toolName, stopwatch.Elapsed, succeeded: true, agentName: diagnosticsBuilder?.AgentName);

                    var toolDiag = new ToolCallDiagnostics(
                        Sequence: sequence,
                        ToolName: toolName,
                        Duration: stopwatch.Elapsed,
                        Succeeded: true,
                        ErrorMessage: null,
                        StartedAt: startedAt,
                        CompletedAt: DateTimeOffset.UtcNow,
                        CustomMetrics: customMetrics.Count > 0 ? customMetrics : null)
                    {
                        AgentName = diagnosticsBuilder?.AgentName,
                        Arguments = SnapshotArguments(context.Arguments),
                        Result = result,
                        ArgumentsCharCount = DiagnosticsCharCounter.JsonLength(SnapshotArguments(context.Arguments)),
                        ResultCharCount = DiagnosticsCharCounter.JsonLength(result),
                    };
                    diagnosticsBuilder?.AddToolCall(toolDiag);
                    (toolCallCollector as ToolCallCollector)?.Add(toolDiag);

                    progressAccessor.Current.Report(new ToolCallCompletedEvent(
                        Timestamp: DateTimeOffset.UtcNow,
                        WorkflowId: progressAccessor.Current.WorkflowId,
                        AgentId: progressAccessor.Current.AgentId,
                        ParentAgentId: diagnosticsBuilder?.ParentAgentName,
                        Depth: progressAccessor.Current.Depth,
                        SequenceNumber: progressAccessor.Current.NextSequence(),
                        ToolName: toolName,
                        Duration: stopwatch.Elapsed,
                        CustomMetrics: customMetrics.Count > 0 ? customMetrics : null));

                    return result;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();

                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    activity?.SetTag("status", "failed");

                    metrics.RecordToolCall(toolName, stopwatch.Elapsed, succeeded: false, agentName: diagnosticsBuilder?.AgentName);

                    var failedToolDiag = new ToolCallDiagnostics(
                        Sequence: sequence,
                        ToolName: toolName,
                        Duration: stopwatch.Elapsed,
                        Succeeded: false,
                        ErrorMessage: ex.Message,
                        StartedAt: startedAt,
                        CompletedAt: DateTimeOffset.UtcNow,
                        CustomMetrics: customMetrics.Count > 0 ? customMetrics : null)
                    {
                        AgentName = diagnosticsBuilder?.AgentName,
                        Arguments = SnapshotArguments(context.Arguments),
                        ArgumentsCharCount = DiagnosticsCharCounter.JsonLength(SnapshotArguments(context.Arguments)),
                    };
                    diagnosticsBuilder?.AddToolCall(failedToolDiag);
                    (toolCallCollector as ToolCallCollector)?.Add(failedToolDiag);

                    progressAccessor.Current.Report(new ToolCallFailedEvent(
                        Timestamp: DateTimeOffset.UtcNow,
                        WorkflowId: progressAccessor.Current.WorkflowId,
                        AgentId: progressAccessor.Current.AgentId,
                        ParentAgentId: diagnosticsBuilder?.ParentAgentName,
                        Depth: progressAccessor.Current.Depth,
                        SequenceNumber: progressAccessor.Current.NextSequence(),
                        ToolName: toolName,
                        ErrorMessage: ex.Message,
                        Duration: stopwatch.Elapsed));

                    throw;
                }
                finally
                {
                    ToolMetricsAccessor.CurrentToolMetrics.Value = null;
                }
            });
    }

    private static IReadOnlyDictionary<string, object?>? SnapshotArguments(
        IDictionary<string, object?>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
        {
            return null;
        }

        return new Dictionary<string, object?>(arguments);
    }
}
