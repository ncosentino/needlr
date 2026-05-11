namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Configuration options for pipeline-shape OpenTelemetry metrics emitted by
/// <see cref="IPipelineMetrics"/>. Sibling of
/// <see cref="AgentFrameworkMetricsOptions"/> — that type scopes the
/// per-agent-run meter; this one scopes the pipeline-runner meter.
/// </summary>
/// <remarks>
/// <para>
/// Configure via the syringe:
/// </para>
/// <code>
/// .UsingAgentFramework(af => af
///     .ConfigurePipelineMetrics(o =>
///     {
///         o.MeterName = "MyApp.Pipelines";
///         o.ActivitySourceName = "MyApp.Pipelines";
///     }))
/// </code>
/// <para>
/// When unconfigured, <see cref="IPipelineMetrics"/> resolves to
/// <see cref="NoOpPipelineMetrics"/> — observability is opt-in with zero overhead
/// by default. Same posture as <see cref="AgentFrameworkMetricsOptions"/>.
/// </para>
/// </remarks>
[DoNotAutoRegister]
public sealed class PipelineMetricsOptions
{
    /// <summary>
    /// The name used for the <see cref="System.Diagnostics.Metrics.Meter"/> that
    /// emits pipeline-shape counters and histograms (<c>pipeline.run.*</c>,
    /// <c>pipeline.stage.*</c>). Defaults to
    /// <c>"NexusLabs.Needlr.AgentFramework.Pipelines"</c>.
    /// </summary>
    public string MeterName { get; set; } = "NexusLabs.Needlr.AgentFramework.Pipelines";

    /// <summary>
    /// The name used for the <see cref="System.Diagnostics.ActivitySource"/> that
    /// emits the parent <c>pipeline.run</c> span and child <c>pipeline.stage</c>
    /// spans. Defaults to <see cref="MeterName"/> when <see langword="null"/>.
    /// </summary>
    public string? ActivitySourceName { get; set; }

    internal string ResolvedActivitySourceName => ActivitySourceName ?? MeterName;
}
