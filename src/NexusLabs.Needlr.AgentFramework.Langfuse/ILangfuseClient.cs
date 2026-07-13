namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Provides the complete non-owning Langfuse client surface for scenarios, experiments, datasets,
/// score configuration, metrics, model pricing, prompts, comments, and known-trace scoring.
/// </summary>
/// <remarks>
/// <para>
/// Resolve this interface from dependency injection after calling
/// <see cref="LangfuseServiceCollectionExtensions.AddNeedlrLangfuse(Microsoft.Extensions.DependencyInjection.IServiceCollection, Action{LangfuseOptions}?)"/>.
/// The facade does not own the host's OpenTelemetry providers or HTTP transport and does not
/// implement <see cref="IDisposable"/>.
/// </para>
/// <para>
/// For standalone applications that must own and explicitly shut down their OpenTelemetry
/// providers, use <see cref="LangfuseTelemetry.Start(LangfuseOptions)"/> and retain the returned
/// <see cref="ILangfuseSession"/>.
/// </para>
/// </remarks>
public interface ILangfuseClient
{
    /// <summary>
    /// Gets a value indicating whether Langfuse export and API operations are enabled.
    /// <see langword="false"/> indicates a coherent no-op client.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Gets structured trace, REST publication, retry, and local-drain health.
    /// </summary>
    LangfusePublicationHealth PublicationHealth { get; }

    /// <summary>
    /// Gets the client for recording scores against known trace, observation, and session ids.
    /// </summary>
    ILangfuseScoreClient Scores { get; }

    /// <summary>
    /// Begins a Langfuse trace scoped to a single evaluation scenario or agent run.
    /// </summary>
    /// <param name="name">The trace name shown in Langfuse.</param>
    /// <param name="sessionId">An optional Langfuse session id used to group related traces.</param>
    /// <param name="userId">An optional end-user identifier associated with the trace.</param>
    /// <param name="tags">Optional tags used to categorize the trace.</param>
    /// <param name="metadata">Optional filterable key/value metadata attached to the trace.</param>
    /// <returns>
    /// An <see cref="ILangfuseScenario"/> to dispose when the scenario completes. A disabled client
    /// returns an inert no-op scenario.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is <see langword="null"/> or whitespace.</exception>
    ILangfuseScenario BeginScenario(
        string name,
        string? sessionId = null,
        string? userId = null,
        IEnumerable<string>? tags = null,
        IReadOnlyDictionary<string, string>? metadata = null);

    /// <summary>
    /// Gets the client for creating and populating Langfuse datasets.
    /// </summary>
    ILangfuseDatasetClient Datasets { get; }

    /// <summary>
    /// Gets the client for idempotently registering Langfuse score configurations.
    /// </summary>
    ILangfuseScoreConfigClient ScoreConfigs { get; }

    /// <summary>
    /// Gets the client for reading aggregates from the Langfuse Metrics API.
    /// </summary>
    ILangfuseMetricsClient Metrics { get; }

    /// <summary>
    /// Gets the client for registering Langfuse model price definitions.
    /// </summary>
    ILangfuseModelClient Models { get; }

    /// <summary>
    /// Gets the client for fetching and creating prompts in Langfuse prompt management.
    /// </summary>
    ILangfusePromptClient Prompts { get; }

    /// <summary>
    /// Begins a Langfuse experiment run whose item traces are linked to an existing dataset.
    /// </summary>
    /// <param name="datasetName">The existing dataset to score against.</param>
    /// <param name="runName">A caller-supplied run name used for experiment comparison.</param>
    /// <param name="options">Optional description and structured metadata for the run.</param>
    /// <returns>
    /// An <see cref="ILangfuseExperimentRun"/>. A disabled client returns an inert no-op run.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="datasetName"/> or <paramref name="runName"/> is <see langword="null"/> or whitespace.
    /// </exception>
    ILangfuseExperimentRun BeginExperimentRun(
        string datasetName,
        string runName,
        LangfuseExperimentRunOptions? options = null);

    /// <summary>
    /// Attaches a comment to an existing Langfuse trace.
    /// </summary>
    /// <param name="traceId">The id of an existing Langfuse trace.</param>
    /// <param name="content">The comment content.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// A task that completes when Langfuse accepts the comment or a non-fatal failure is reported.
    /// </returns>
    Task AddTraceCommentAsync(
        string traceId,
        string content,
        CancellationToken cancellationToken = default);
}
