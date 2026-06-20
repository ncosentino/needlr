namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Inert <see cref="ILangfuseSession"/> returned when Langfuse export is not configured. All
/// members are no-ops so calling code never needs to branch on whether credentials are present.
/// </summary>
internal sealed class DisabledLangfuseSession : ILangfuseSession
{
    /// <inheritdoc />
    public bool IsEnabled => false;

    /// <inheritdoc />
    public int ScoresFailed => 0;

    /// <inheritdoc />
    public ILangfuseDatasetClient Datasets { get; } = new DisabledLangfuseDatasetClient();

    /// <inheritdoc />
    public ILangfuseScoreConfigClient ScoreConfigs { get; } = new DisabledLangfuseScoreConfigClient();

    /// <inheritdoc />
    public ILangfuseMetricsClient Metrics { get; } = new DisabledLangfuseMetricsClient();

    /// <inheritdoc />
    public ILangfuseModelClient Models { get; } = new DisabledLangfuseModelClient();

    /// <inheritdoc />
    public ILangfusePromptClient Prompts { get; } = new DisabledLangfusePromptClient();

    /// <inheritdoc />
    public bool Flush(TimeSpan? timeout = null) => true;

    /// <inheritdoc />
    public ILangfuseScenario BeginScenario(
        string name,
        string? sessionId = null,
        string? userId = null,
        IEnumerable<string>? tags = null,
        IReadOnlyDictionary<string, string>? metadata = null) =>
        new DisabledLangfuseScenario();

    /// <inheritdoc />
    public ILangfuseExperimentRun BeginExperimentRun(string datasetName, string runName, string? runDescription = null) =>
        new DisabledLangfuseExperimentRun(datasetName, runName);

    /// <inheritdoc />
    public Task AddTraceCommentAsync(string traceId, string content, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
