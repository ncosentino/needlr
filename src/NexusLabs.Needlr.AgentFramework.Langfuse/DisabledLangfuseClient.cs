namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Coherent no-op <see cref="ILangfuseClient"/> used when Langfuse is disabled or unconfigured.
/// </summary>
[DoNotAutoRegister]
internal sealed class DisabledLangfuseClient : ILangfuseClient
{
    public DisabledLangfuseClient()
    {
        PublicationHealth = new LangfusePublicationHealth(isEnabled: false);
        Scores = new DisabledLangfuseScoreClient();
        Datasets = new DisabledLangfuseDatasetClient();
        ScoreConfigs = new DisabledLangfuseScoreConfigClient();
        Metrics = new DisabledLangfuseMetricsClient();
        Models = new DisabledLangfuseModelClient();
        Prompts = new DisabledLangfusePromptClient();
    }

    /// <inheritdoc />
    public bool IsEnabled => false;

    /// <inheritdoc />
    public LangfusePublicationHealth PublicationHealth { get; }

    /// <inheritdoc />
    public ILangfuseScoreClient Scores { get; }

    /// <inheritdoc />
    public ILangfuseDatasetClient Datasets { get; }

    /// <inheritdoc />
    public ILangfuseScoreConfigClient ScoreConfigs { get; }

    /// <inheritdoc />
    public ILangfuseMetricsClient Metrics { get; }

    /// <inheritdoc />
    public ILangfuseModelClient Models { get; }

    /// <inheritdoc />
    public ILangfusePromptClient Prompts { get; }

    /// <inheritdoc />
    public ILangfuseScenario BeginScenario(
        string name,
        string? sessionId = null,
        string? userId = null,
        IEnumerable<string>? tags = null,
        IReadOnlyDictionary<string, string>? metadata = null) =>
        new DisabledLangfuseScenario();

    /// <inheritdoc />
    public ILangfuseExperimentRun BeginExperimentRun(
        string datasetName,
        string runName,
        LangfuseExperimentRunOptions? options = null) =>
        new DisabledLangfuseExperimentRun(datasetName, runName, options);

    /// <inheritdoc />
    public Task AddTraceCommentAsync(
        string traceId,
        string content,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
