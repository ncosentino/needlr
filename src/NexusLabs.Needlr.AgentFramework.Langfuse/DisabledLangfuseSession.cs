namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Inert <see cref="ILangfuseSession"/> returned when Langfuse export is not configured. All
/// members are no-ops so calling code never needs to branch on whether credentials are present.
/// </summary>
[DoNotAutoRegister]
internal sealed class DisabledLangfuseSession : ILangfuseSession
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
    public Task AddTraceCommentAsync(string traceId, string content, CancellationToken cancellationToken = default) =>
        _client.AddTraceCommentAsync(traceId, content, cancellationToken);

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
