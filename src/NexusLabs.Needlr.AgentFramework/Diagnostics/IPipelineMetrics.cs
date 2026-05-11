using System.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Records pipeline-shape execution metrics for observability. The default
/// implementation emits <see cref="System.Diagnostics.Metrics.Meter"/> counters /
/// histograms and <see cref="ActivitySource"/> activities compatible with
/// OpenTelemetry. Sibling of <see cref="IAgentMetrics"/> — that interface scopes
/// to per-agent-run signals; this one scopes to per-pipeline and per-stage signals
/// emitted by <c>SequentialPipelineRunner</c>.
/// </summary>
/// <remarks>
/// <para>
/// Registered via DI — consumers can replace with a no-op
/// (<see cref="NoOpPipelineMetrics"/>) or a custom implementation. The runner
/// resolves <see cref="IPipelineMetrics"/> from DI and calls these methods at the
/// appropriate emission points; consumers do not call them directly.
/// </para>
/// <para>
/// If OpenTelemetry is wired in the host (e.g., via <c>AddOpenTelemetry()</c>),
/// these metrics are exported automatically — no Needlr-specific configuration
/// required beyond ensuring the configured meter / activity-source name is added
/// to the OpenTelemetry pipeline.
/// </para>
/// </remarks>
public interface IPipelineMetrics
{
    /// <summary>
    /// Records that a pipeline run has started. Called once at the beginning of
    /// every pipeline invocation.
    /// </summary>
    /// <param name="pipelineName">
    /// The pipeline's stable identifier — used as the <c>pipeline_name</c> tag on
    /// every metric this run emits. Sourced from
    /// <c>SequentialPipelineOptions.PipelineName</c> when supplied, otherwise from
    /// the progress reporter's <c>WorkflowId</c>.
    /// </param>
    void RecordPipelineStarted(string pipelineName);

    /// <summary>
    /// Records that a pipeline run has completed. Called once at the end of every
    /// pipeline invocation, on both success and failure paths.
    /// </summary>
    /// <param name="pipelineName">The pipeline's stable identifier (see <see cref="RecordPipelineStarted"/>).</param>
    /// <param name="succeeded">Whether all stages completed successfully.</param>
    /// <param name="duration">Wall-clock duration of the entire pipeline run.</param>
    void RecordPipelineCompleted(string pipelineName, bool succeeded, TimeSpan duration);

    /// <summary>
    /// Records that a single pipeline stage has completed. Called once per stage
    /// (skipped, failed, succeeded, partial-failed) immediately after the stage's
    /// <see cref="IAgentStageResult"/> is constructed.
    /// </summary>
    /// <param name="pipelineName">The pipeline's stable identifier (see <see cref="RecordPipelineStarted"/>).</param>
    /// <param name="stage">The completed stage's result; the implementation derives
    /// per-stage tag values (<c>stage_name</c>, <c>phase_name</c>, <c>outcome</c>,
    /// <c>termination_cause</c>) and emits per-token-kind / per-tool-failure
    /// instruments from <see cref="IAgentRunDiagnostics"/>.</param>
    /// <param name="duration">Wall-clock duration of this stage's execution.
    /// <see cref="TimeSpan.Zero"/> for skipped stages (no work was done).</param>
    void RecordStageCompleted(string pipelineName, IAgentStageResult stage, TimeSpan duration);

    /// <summary>
    /// Gets the <see cref="ActivitySource"/> the runner uses to create
    /// <c>pipeline.run</c> and <c>pipeline.stage</c> spans. Exported via
    /// OpenTelemetry when a listener is registered against the configured source name.
    /// </summary>
    ActivitySource ActivitySource { get; }
}
