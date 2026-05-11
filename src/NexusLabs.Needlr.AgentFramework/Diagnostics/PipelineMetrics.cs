using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Default <see cref="IPipelineMetrics"/> implementation using <see cref="Meter"/>
/// for counters/histograms and <see cref="ActivitySource"/> for distributed
/// tracing spans. Compatible with OpenTelemetry — both metrics and traces are
/// exported when listeners are registered against the configured source name.
/// </summary>
/// <remarks>
/// Source names default to <c>"NexusLabs.Needlr.AgentFramework.Pipelines"</c> but
/// can be overridden via <see cref="PipelineMetricsOptions.MeterName"/> and
/// <see cref="PipelineMetricsOptions.ActivitySourceName"/> to match consumers'
/// existing dashboard queries.
/// </remarks>
[DoNotAutoRegister]
internal sealed class PipelineMetrics : IPipelineMetrics, IDisposable
{
    private readonly Meter _meter;
    private readonly ActivitySource _activitySource;
    private readonly Counter<long> _runsStarted;
    private readonly Counter<long> _runsCompleted;
    private readonly Histogram<double> _runDuration;
    private readonly Counter<long> _stagesCompleted;
    private readonly Histogram<double> _stageDuration;
    private readonly Counter<long> _stageTokens;
    private readonly Counter<long> _stageToolFailed;

    public PipelineMetrics() : this(new PipelineMetricsOptions()) { }

    public PipelineMetrics(PipelineMetricsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _meter = new Meter(options.MeterName);
        _activitySource = new ActivitySource(options.ResolvedActivitySourceName);

        _runsStarted = _meter.CreateCounter<long>(
            "pipeline.run.started",
            description: "Pipeline runs started");

        _runsCompleted = _meter.CreateCounter<long>(
            "pipeline.run.completed",
            description: "Pipeline runs completed");

        _runDuration = _meter.CreateHistogram<double>(
            "pipeline.run.duration",
            unit: "s",
            description: "Pipeline run execution duration");

        _stagesCompleted = _meter.CreateCounter<long>(
            "pipeline.stage.completed",
            description: "Pipeline stages completed");

        _stageDuration = _meter.CreateHistogram<double>(
            "pipeline.stage.duration",
            unit: "s",
            description: "Pipeline stage execution duration");

        _stageTokens = _meter.CreateCounter<long>(
            "pipeline.stage.tokens",
            description: "Tokens consumed by a pipeline stage, broken down by token kind");

        _stageToolFailed = _meter.CreateCounter<long>(
            "pipeline.stage.tool.failed",
            description: "Failed tool invocations in a pipeline stage");
    }

    /// <inheritdoc />
    public ActivitySource ActivitySource => _activitySource;

    /// <inheritdoc />
    public void RecordPipelineStarted(string pipelineName) =>
        _runsStarted.Add(1, new KeyValuePair<string, object?>("pipeline_name", pipelineName));

    /// <inheritdoc />
    public void RecordPipelineCompleted(string pipelineName, bool succeeded, TimeSpan duration)
    {
        var pipelineTag = new KeyValuePair<string, object?>("pipeline_name", pipelineName);
        var outcomeTag = new KeyValuePair<string, object?>("outcome", succeeded ? "Succeeded" : "Failed");

        _runsCompleted.Add(1, pipelineTag, outcomeTag);
        _runDuration.Record(duration.TotalSeconds, pipelineTag, outcomeTag);
    }

    /// <inheritdoc />
    public void RecordStageCompleted(string pipelineName, IAgentStageResult stage, TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(stage);

        var pipelineTag = new KeyValuePair<string, object?>("pipeline_name", pipelineName);
        var stageTag = new KeyValuePair<string, object?>("stage_name", stage.AgentName);
        var phaseTag = new KeyValuePair<string, object?>("phase_name", stage.PhaseName ?? "(none)");
        var outcomeTag = new KeyValuePair<string, object?>("outcome", stage.Outcome.ToString());
        var terminationTag = new KeyValuePair<string, object?>(
            "termination_cause",
            stage.Termination?.ToTagValue() ?? "Unspecified");

        _stagesCompleted.Add(1, pipelineTag, stageTag, outcomeTag, terminationTag, phaseTag);

        if (stage.Outcome == StageOutcome.Skipped)
            return;

        _stageDuration.Record(duration.TotalSeconds, pipelineTag, stageTag, outcomeTag, phaseTag);

        if (stage.Diagnostics is { } diagnostics)
        {
            EmitTokenCounts(diagnostics.AggregateTokenUsage, pipelineTag, stageTag);
            EmitFailedToolCalls(diagnostics.ToolCalls, pipelineTag, stageTag);
        }
    }

    private void EmitTokenCounts(
        TokenUsage tokens,
        KeyValuePair<string, object?> pipelineTag,
        KeyValuePair<string, object?> stageTag)
    {
        EmitTokenKind(tokens.InputTokens, "input", pipelineTag, stageTag);
        EmitTokenKind(tokens.OutputTokens, "output", pipelineTag, stageTag);
        EmitTokenKind(tokens.CachedInputTokens, "cached_input", pipelineTag, stageTag);
        EmitTokenKind(tokens.ReasoningTokens, "reasoning", pipelineTag, stageTag);
    }

    private void EmitTokenKind(
        long count,
        string kind,
        KeyValuePair<string, object?> pipelineTag,
        KeyValuePair<string, object?> stageTag)
    {
        if (count <= 0)
            return;

        _stageTokens.Add(
            count,
            pipelineTag,
            stageTag,
            new KeyValuePair<string, object?>("token_kind", kind));
    }

    private void EmitFailedToolCalls(
        IReadOnlyList<ToolCallDiagnostics> toolCalls,
        KeyValuePair<string, object?> pipelineTag,
        KeyValuePair<string, object?> stageTag)
    {
        foreach (var tool in toolCalls)
        {
            if (tool.Succeeded)
                continue;

            _stageToolFailed.Add(
                1,
                pipelineTag,
                stageTag,
                new KeyValuePair<string, object?>("tool_name", tool.ToolName));
        }
    }

    /// <summary>Disposes the underlying <see cref="Meter"/> and <see cref="ActivitySource"/>.</summary>
    public void Dispose()
    {
        _meter.Dispose();
        _activitySource.Dispose();
    }
}
