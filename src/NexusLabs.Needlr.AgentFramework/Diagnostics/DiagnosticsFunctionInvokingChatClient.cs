using System.Diagnostics;

using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Progress;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// A <see cref="FunctionInvokingChatClient"/> that records per-tool-call diagnostics,
/// OTel metrics, and Activity spans for every function invocation. This is the MEAI-native
/// equivalent of the MAF <c>DiagnosticsFunctionCallingMiddleware</c> in Workflows.
/// </summary>
/// <remarks>
/// <para>
/// Use this when the chat pipeline includes <c>FunctionInvokingChatClient</c> (auto
/// tool calling) rather than the <c>IterativeAgentLoop</c>. The loop does its own
/// tool-call recording; using both would produce duplicates.
/// </para>
/// <para>
/// Records are written to the AsyncLocal <see cref="AgentRunDiagnosticsBuilder"/> and
/// OTel metrics via <see cref="IAgentMetrics"/>. Progress events are emitted to
/// <see cref="IProgressReporterAccessor"/> when available.
/// </para>
/// </remarks>
[DoNotAutoRegister]
public sealed class DiagnosticsFunctionInvokingChatClient : FunctionInvokingChatClient
{
    private readonly IAgentMetrics? _metrics;
    private readonly IProgressReporterAccessor? _progressAccessor;

    /// <summary>
    /// Creates a new diagnostics-enabled <see cref="FunctionInvokingChatClient"/>.
    /// </summary>
    /// <param name="innerClient">The inner chat client to delegate to.</param>
    /// <param name="metrics">Optional OTel metrics recorder.</param>
    /// <param name="progressAccessor">Optional progress reporter for real-time events.</param>
    public DiagnosticsFunctionInvokingChatClient(
        IChatClient innerClient,
        IAgentMetrics? metrics = null,
        IProgressReporterAccessor? progressAccessor = null)
        : base(innerClient)
    {
        _metrics = metrics;
        _progressAccessor = progressAccessor;
    }

    /// <inheritdoc />
    protected override async ValueTask<object?> InvokeFunctionAsync(
        FunctionInvocationContext context,
        CancellationToken cancellationToken)
    {
        var diagnosticsBuilder = AgentRunDiagnosticsBuilder.GetCurrent();
        var sequence = diagnosticsBuilder?.NextToolCallSequence() ?? -1;
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        var toolName = context.Function?.Name ?? "unknown";

        using var activity = _metrics?.ActivitySource.StartActivity(
            $"agent.tool {toolName}", ActivityKind.Internal);
        activity?.SetTag("agent.tool.name", toolName);
        activity?.SetTag("agent.tool.sequence", sequence);
        activity?.SetTag("gen_ai.agent.name", diagnosticsBuilder?.AgentName);

        var reporter = _progressAccessor?.Current;
        reporter?.Report(new ToolCallStartedEvent(
            Timestamp: startedAt,
            WorkflowId: reporter.WorkflowId,
            AgentId: reporter.AgentId,
            ParentAgentId: diagnosticsBuilder?.ParentAgentName,
            Depth: reporter.Depth,
            SequenceNumber: reporter.NextSequence(),
            ToolName: toolName));

        var customMetrics = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        ToolMetricsAccessor.CurrentToolMetrics.Value = customMetrics;

        try
        {
            var result = await base.InvokeFunctionAsync(context, cancellationToken)
                .ConfigureAwait(false);

            stopwatch.Stop();

            activity?.SetTag("status", "success");

            if (activity is not null && customMetrics.Count > 0)
            {
                foreach (var (key, value) in customMetrics)
                {
                    activity.SetTag($"tool.custom.{key}", value);
                }
            }

            _metrics?.RecordToolCall(
                toolName, stopwatch.Elapsed, succeeded: true,
                agentName: diagnosticsBuilder?.AgentName);

            var arguments = SnapshotArguments(context.Arguments);

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
                Arguments = arguments,
                Result = result,
                ArgumentsCharCount = DiagnosticsCharCounter.JsonLength(arguments),
                ResultCharCount = DiagnosticsCharCounter.JsonLength(result),
            };

            diagnosticsBuilder?.AddToolCall(toolDiag);

            reporter?.Report(new ToolCallCompletedEvent(
                Timestamp: DateTimeOffset.UtcNow,
                WorkflowId: reporter.WorkflowId,
                AgentId: reporter.AgentId,
                ParentAgentId: diagnosticsBuilder?.ParentAgentName,
                Depth: reporter.Depth,
                SequenceNumber: reporter.NextSequence(),
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

            _metrics?.RecordToolCall(
                toolName, stopwatch.Elapsed, succeeded: false,
                agentName: diagnosticsBuilder?.AgentName);

            var arguments = SnapshotArguments(context.Arguments);

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
                Arguments = arguments,
                ArgumentsCharCount = DiagnosticsCharCounter.JsonLength(arguments),
            };

            diagnosticsBuilder?.AddToolCall(failedToolDiag);

            reporter?.Report(new ToolCallFailedEvent(
                Timestamp: DateTimeOffset.UtcNow,
                WorkflowId: reporter.WorkflowId,
                AgentId: reporter.AgentId,
                ParentAgentId: diagnosticsBuilder?.ParentAgentName,
                Depth: reporter.Depth,
                SequenceNumber: reporter.NextSequence(),
                ToolName: toolName,
                ErrorMessage: ex.Message,
                Duration: stopwatch.Elapsed));

            throw;
        }
        finally
        {
            ToolMetricsAccessor.CurrentToolMetrics.Value = null;
        }
    }

    private static IReadOnlyDictionary<string, object?>? SnapshotArguments(
        IReadOnlyDictionary<string, object?>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
        {
            return null;
        }

        return new Dictionary<string, object?>(arguments);
    }
}
