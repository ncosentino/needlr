namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Represents an active Langfuse export session. Owns the OpenTelemetry tracer/meter providers
/// that forward Needlr agent telemetry to Langfuse and flushes pending telemetry on disposal.
/// </summary>
/// <remarks>
/// Obtain an instance from <see cref="LangfuseTelemetry.Start(LangfuseOptions)"/>. Keep it alive
/// for the lifetime over which agent runs and evaluations should be captured (for example, an
/// xUnit collection fixture), then dispose it to force a final flush.
/// </remarks>
public interface ILangfuseSession : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether this session is exporting telemetry. <see langword="false"/>
    /// when the supplied <see cref="LangfuseOptions"/> were not configured (for example, missing
    /// credentials), in which case the session is an inert no-op.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Gets the cumulative number of evaluation-score uploads that have failed across all scenarios
    /// created from this session. Always <c>0</c> under
    /// <see cref="LangfuseScoreFailureMode.Strict"/> (failures throw instead) and for a disabled
    /// session. Useful as an assertion target or a health indicator.
    /// </summary>
    int ScoresFailed { get; }

    /// <summary>
    /// Flushes any buffered telemetry to Langfuse. Useful before reading results or at the end of
    /// a test run. No-op when <see cref="IsEnabled"/> is <see langword="false"/>.
    /// </summary>
    /// <param name="timeout">
    /// Maximum time to wait for the flush to complete, or <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>
    /// to wait indefinitely. When <see langword="null"/>, a provider default is used.
    /// </param>
    /// <returns><see langword="true"/> if the flush succeeded; otherwise <see langword="false"/>.</returns>
    bool Flush(TimeSpan? timeout = null);

    /// <summary>
    /// Begins a Langfuse trace scoped to a single eval scenario or agent run. Agent telemetry
    /// produced while the returned scenario is active nests under its trace, and evaluation scores
    /// can be attached to it.
    /// </summary>
    /// <param name="name">The trace name shown in Langfuse (for example the eval scenario name).</param>
    /// <param name="sessionId">
    /// An optional Langfuse session id used to group related traces (for example multiple
    /// iterations of one scenario).
    /// </param>
    /// <param name="userId">An optional end-user identifier associated with the trace.</param>
    /// <param name="tags">Optional tags used to categorize the trace in Langfuse.</param>
    /// <param name="metadata">Optional filterable key/value metadata attached to the trace.</param>
    /// <returns>
    /// An <see cref="ILangfuseScenario"/> to dispose when the scenario completes. When this session
    /// is disabled the returned scenario is an inert no-op.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is <see langword="null"/> or whitespace.</exception>
    ILangfuseScenario BeginScenario(
        string name,
        string? sessionId = null,
        string? userId = null,
        IEnumerable<string>? tags = null,
        IReadOnlyDictionary<string, string>? metadata = null);

    /// <summary>
    /// Gets the client for creating and populating Langfuse datasets (the eval cases experiment
    /// runs are scored against). Inert when this session is disabled.
    /// </summary>
    ILangfuseDatasetClient Datasets { get; }

    /// <summary>
    /// Gets the client for idempotently registering Langfuse score configs so eval scores render
    /// with defined data types, ranges, and category sets. Inert when this session is disabled.
    /// </summary>
    ILangfuseScoreConfigClient ScoreConfigs { get; }

    /// <summary>
    /// Gets the client for reading aggregates back from Langfuse via the Metrics API — for example
    /// to drive a CI quality gate from recorded eval scores. Inert when this session is disabled.
    /// </summary>
    ILangfuseMetricsClient Metrics { get; }

    /// <summary>
    /// Gets the client for registering model price definitions so Langfuse can compute cost for
    /// generations whose model it does not price by default. Inert when this session is disabled.
    /// </summary>
    ILangfuseModelClient Models { get; }

    /// <summary>
    /// Begins a Langfuse experiment (dataset run). Each item begun on the returned run links its
    /// trace to the run so the recorded scores aggregate into the experiment-comparison view.
    /// </summary>
    /// <param name="datasetName">
    /// The dataset to score against. The dataset and its items must already exist — see
    /// <see cref="Datasets"/>.
    /// </param>
    /// <param name="runName">
    /// A caller-supplied run name (for example a git SHA or CI run id) so runs are comparable.
    /// </param>
    /// <param name="runDescription">An optional description applied to the run.</param>
    /// <returns>
    /// An <see cref="ILangfuseExperimentRun"/>. When this session is disabled the run is inert.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="datasetName"/> or <paramref name="runName"/> is <see langword="null"/> or whitespace.
    /// </exception>
    ILangfuseExperimentRun BeginExperimentRun(string datasetName, string runName, string? runDescription = null);

    /// <summary>
    /// Attaches a comment to an existing trace — for example a CI run URL, git commit, or a failing
    /// assertion message. No-op when this session is disabled.
    /// </summary>
    /// <remarks>
    /// Call this <strong>after</strong> <see cref="Flush"/> and after the trace has been ingested:
    /// unlike scores, Langfuse rejects a comment whose target trace does not yet exist. Comment
    /// failures are non-fatal and are reported through <c>DiagnosticsCallback</c>.
    /// </remarks>
    /// <param name="traceId">The id of an existing Langfuse trace.</param>
    /// <param name="content">The comment content (Langfuse limits this to 5000 characters).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once Langfuse has accepted the comment or the failure has been reported.</returns>
    Task AddTraceCommentAsync(string traceId, string content, CancellationToken cancellationToken = default);
}
