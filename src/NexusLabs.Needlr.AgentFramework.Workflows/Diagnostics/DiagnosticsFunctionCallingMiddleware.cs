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
    internal static void Wire(AIAgentBuilder builder, IAgentMetrics metrics, IProgressReporterAccessor progressAccessor)
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

                progressAccessor.Current.Report(new ToolCallStartedEvent(
                    Timestamp: startedAt,
                    WorkflowId: progressAccessor.Current.WorkflowId,
                    AgentId: progressAccessor.Current.AgentId,
                    ParentAgentId: null,
                    Depth: progressAccessor.Current.Depth,
                    SequenceNumber: progressAccessor.Current.NextSequence(),
                    ToolName: toolName));

                var customMetrics = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                ToolMetricsAccessor.CurrentToolMetrics.Value = customMetrics;

                try
                {
                    var result = await next(context, cancellationToken).ConfigureAwait(false);
                    stopwatch.Stop();

                    metrics.RecordToolCall(toolName, stopwatch.Elapsed, succeeded: true);

                    diagnosticsBuilder?.AddToolCall(new ToolCallDiagnostics(
                        Sequence: sequence,
                        ToolName: toolName,
                        Duration: stopwatch.Elapsed,
                        Succeeded: true,
                        ErrorMessage: null,
                        StartedAt: startedAt,
                        CompletedAt: DateTimeOffset.UtcNow,
                        CustomMetrics: customMetrics.Count > 0 ? customMetrics : null));

                    progressAccessor.Current.Report(new ToolCallCompletedEvent(
                        Timestamp: DateTimeOffset.UtcNow,
                        WorkflowId: progressAccessor.Current.WorkflowId,
                        AgentId: progressAccessor.Current.AgentId,
                        ParentAgentId: null,
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

                    metrics.RecordToolCall(toolName, stopwatch.Elapsed, succeeded: false);

                    diagnosticsBuilder?.AddToolCall(new ToolCallDiagnostics(
                        Sequence: sequence,
                        ToolName: toolName,
                        Duration: stopwatch.Elapsed,
                        Succeeded: false,
                        ErrorMessage: ex.Message,
                        StartedAt: startedAt,
                        CompletedAt: DateTimeOffset.UtcNow,
                        CustomMetrics: customMetrics.Count > 0 ? customMetrics : null));

                    progressAccessor.Current.Report(new ToolCallFailedEvent(
                        Timestamp: DateTimeOffset.UtcNow,
                        WorkflowId: progressAccessor.Current.WorkflowId,
                        AgentId: progressAccessor.Current.AgentId,
                        ParentAgentId: null,
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
}
