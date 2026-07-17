namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Coherent disabled <see cref="ILangfuseClient"/> used when Langfuse is unconfigured.
/// </summary>
[DoNotAutoRegister]
internal sealed class DisabledLangfuseClient :
    ILangfuseClient,
    ILangfuseExperimentItemScopeProviderFactory,
    ILangfuseExperimentResultSinkFactory
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
        string runName) =>
        BeginExperimentRun(datasetName, runName, options: null);

    /// <inheritdoc />
    public ILangfuseExperimentRun BeginExperimentRun(
        string datasetName,
        string runName,
        LangfuseExperimentRunOptions? options) =>
        new DisabledLangfuseExperimentRun(datasetName, runName, options);

    /// <inheritdoc />
    LangfuseExperimentItemScopeProvider<TCase, TOutput>
        ILangfuseExperimentItemScopeProviderFactory.CreateExperimentItemScopeProvider<TCase, TOutput>(
            ILangfuseExperimentRun run,
            LangfuseExperimentItemScopeOptions<TCase>? options) =>
        run.CreateHostedExperimentItemScopeProvider<TCase, TOutput>(options);

    /// <inheritdoc />
    LangfuseExperimentItemScopeProvider<TCase, TOutput>
        ILangfuseExperimentItemScopeProviderFactory.CreateLocalExperimentItemScopeProvider<TCase, TOutput>(
            LangfuseExperimentItemScopeOptions<TCase>? options) =>
        new(
            new LangfuseExperimentTrialLifecycleFactory(
                _ => new DisabledLangfuseScenario(),
                itemLinker: null),
            linkHostedItem: false,
            options);

    LangfuseExperimentResultSink<TCase, TOutput>
        ILangfuseExperimentResultSinkFactory.CreateExperimentResultSink<TCase, TOutput>(
            ILangfuseExperimentRun run,
            LangfuseExperimentResultSinkOptions<TCase, TOutput>? options)
    {
        ArgumentNullException.ThrowIfNull(run);
        return new LangfuseExperimentResultSink<TCase, TOutput>(
            recorder: null,
            isEnabled: false,
            run,
            options);
    }

    LangfuseExperimentResultSink<TCase, TOutput>
        ILangfuseExperimentResultSinkFactory.CreateLocalExperimentResultSink<TCase, TOutput>(
            LangfuseExperimentResultSinkOptions<TCase, TOutput>? options) =>
        new(
            recorder: null,
            isEnabled: false,
            run: null,
            options);

    /// <inheritdoc />
    public Task AddTraceCommentAsync(
        string traceId,
        string content,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
