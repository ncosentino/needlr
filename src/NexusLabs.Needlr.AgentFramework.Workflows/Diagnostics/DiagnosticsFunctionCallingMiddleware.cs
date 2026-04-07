using System.Diagnostics;

using Microsoft.Agents.AI;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;

/// <summary>
/// Innermost middleware layer: wraps each tool/function invocation to capture per-call
/// timing and custom metrics attached by the tool via <see cref="IToolMetricsAccessor"/>.
/// Emits <see cref="IAgentMetrics"/> for each tool call.
/// </summary>
internal static class DiagnosticsFunctionCallingMiddleware
{
    /// <summary>
    /// Wires the function-calling diagnostics middleware onto the given builder.
    /// </summary>
    internal static void Wire(AIAgentBuilder builder, IAgentMetrics metrics)
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

                    throw;
                }
                finally
                {
                    ToolMetricsAccessor.CurrentToolMetrics.Value = null;
                }
            });
    }
}
