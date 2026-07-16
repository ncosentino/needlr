namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Inert <see cref="ILangfuseSession"/> returned when Langfuse export is not configured. All
/// members are no-ops so calling code never needs to branch on whether credentials are present.
/// </summary>
[DoNotAutoRegister]
internal sealed class DisabledLangfuseSession :
    ILangfuseSession,
    ILangfuseExperimentItemScopeProviderFactory,
    ILangfuseExperimentResultSinkFactory
{
    private readonly ILangfuseClient _client;

    private static readonly LangfuseShutdownOutcome ShutdownOutcome = new(
        isFinal: true,
        LangfuseProviderShutdownStatus.NotConfigured,
        LangfuseProviderShutdownStatus.NotConfigured);

    public DisabledLangfuseSession()
        : this(new DisabledLangfuseClient())
    {
    }

    public DisabledLangfuseSession(ILangfuseClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <inheritdoc />
    public bool IsEnabled => _client.IsEnabled;

    /// <inheritdoc />
    public LangfusePublicationHealth PublicationHealth => _client.PublicationHealth;

    /// <inheritdoc />
    public ILangfuseScoreClient Scores => _client.Scores;

    /// <inheritdoc />
    public ILangfuseDatasetClient Datasets => _client.Datasets;

    /// <inheritdoc />
    public ILangfuseScoreConfigClient ScoreConfigs => _client.ScoreConfigs;

    /// <inheritdoc />
    public ILangfuseMetricsClient Metrics => _client.Metrics;

    /// <inheritdoc />
    public ILangfuseModelClient Models => _client.Models;

    /// <inheritdoc />
    public ILangfusePromptClient Prompts => _client.Prompts;

    /// <inheritdoc />
    public bool Flush(TimeSpan? timeout = null) => true;

    /// <inheritdoc />
    public LangfuseShutdownOutcome Shutdown(TimeSpan timeout)
    {
        _ = LangfuseTimeout.ToShutdownMilliseconds(timeout);
        return ShutdownOutcome;
    }

    /// <inheritdoc />
    public ILangfuseScenario BeginScenario(
        string name,
        string? sessionId = null,
        string? userId = null,
        IEnumerable<string>? tags = null,
        IReadOnlyDictionary<string, string>? metadata = null) =>
        _client.BeginScenario(name, sessionId, userId, tags, metadata);

    /// <inheritdoc />
    public ILangfuseExperimentRun BeginExperimentRun(
        string datasetName,
        string runName,
        LangfuseExperimentRunOptions? options = null) =>
        _client.BeginExperimentRun(datasetName, runName, options);

    /// <inheritdoc />
    LangfuseExperimentItemScopeProvider<TCase, TOutput>
        ILangfuseExperimentItemScopeProviderFactory.CreateExperimentItemScopeProvider<TCase, TOutput>(
            ILangfuseExperimentRun run,
            LangfuseExperimentItemScopeOptions<TCase>? options) =>
        GetScopeProviderFactory()
            .CreateExperimentItemScopeProvider<TCase, TOutput>(run, options);

    /// <inheritdoc />
    LangfuseExperimentItemScopeProvider<TCase, TOutput>
        ILangfuseExperimentItemScopeProviderFactory.CreateLocalExperimentItemScopeProvider<TCase, TOutput>(
            LangfuseExperimentItemScopeOptions<TCase>? options) =>
        GetScopeProviderFactory()
            .CreateLocalExperimentItemScopeProvider<TCase, TOutput>(options);

    LangfuseExperimentResultSink<TCase, TOutput>
        ILangfuseExperimentResultSinkFactory.CreateExperimentResultSink<TCase, TOutput>(
            ILangfuseExperimentRun run,
            LangfuseExperimentResultSinkOptions<TCase, TOutput>? options) =>
        GetResultSinkFactory()
            .CreateExperimentResultSink<TCase, TOutput>(run, options);

    LangfuseExperimentResultSink<TCase, TOutput>
        ILangfuseExperimentResultSinkFactory.CreateLocalExperimentResultSink<TCase, TOutput>(
            LangfuseExperimentResultSinkOptions<TCase, TOutput>? options) =>
        GetResultSinkFactory()
            .CreateLocalExperimentResultSink<TCase, TOutput>(options);

    /// <inheritdoc />
    public Task AddTraceCommentAsync(string traceId, string content, CancellationToken cancellationToken = default) =>
        _client.AddTraceCommentAsync(traceId, content, cancellationToken);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private ILangfuseExperimentItemScopeProviderFactory GetScopeProviderFactory() =>
        _client as ILangfuseExperimentItemScopeProviderFactory
        ?? throw new NotSupportedException(
            "The configured Langfuse client does not expose the built-in experiment trial lifecycle.");

    private ILangfuseExperimentResultSinkFactory GetResultSinkFactory() =>
        _client as ILangfuseExperimentResultSinkFactory
        ?? throw new NotSupportedException(
            "The configured Langfuse client does not expose the built-in experiment result-sink capability.");
}
