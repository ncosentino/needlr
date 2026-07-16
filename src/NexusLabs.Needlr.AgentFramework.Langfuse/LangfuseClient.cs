namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Shared enabled Langfuse client composition used by both hosted and standalone integrations.
/// </summary>
[DoNotAutoRegister]
internal sealed class LangfuseClient :
    ILangfuseClient,
    ILangfuseExperimentItemScopeProviderFactory,
    ILangfuseExperimentResultSinkFactory
{
    private readonly LangfuseClientComposition _composition;

    public LangfuseClient(
        LangfuseHttpTransport transport,
        LangfuseEndpoints endpoints,
        LangfuseOptions options)
        : this(new LangfuseClientComposition(transport, endpoints, options))
    {
    }

    public LangfuseClient(
        LangfuseClientComposition composition,
        ILangfuseScoreClient scores,
        ILangfuseDatasetClient datasets,
        ILangfuseScoreConfigClient scoreConfigs,
        ILangfuseMetricsClient metrics,
        ILangfuseModelClient models,
        ILangfusePromptClient prompts)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(scores);
        ArgumentNullException.ThrowIfNull(datasets);
        ArgumentNullException.ThrowIfNull(scoreConfigs);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(prompts);
        _composition = composition;
        Scores = scores;
        Datasets = datasets;
        ScoreConfigs = scoreConfigs;
        Metrics = metrics;
        Models = models;
        Prompts = prompts;
    }

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public LangfusePublicationHealth PublicationHealth => _composition.Health;

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
        new LangfuseScenario(
            Scores,
            _composition.Recorder,
            name,
            sessionId,
            userId,
            tags,
            metadata);

    /// <inheritdoc />
    public ILangfuseExperimentRun BeginExperimentRun(
        string datasetName,
        string runName,
        LangfuseExperimentRunOptions? options = null) =>
        new LangfuseExperimentRun(
            _composition.ApiClient,
            Scores,
            _composition.Recorder,
            datasetName,
            runName,
            options,
            _composition.Diagnostics,
            _composition.Health);

    /// <inheritdoc />
    LangfuseExperimentItemScopeProvider<TCase, TOutput>
        ILangfuseExperimentItemScopeProviderFactory.CreateExperimentItemScopeProvider<TCase, TOutput>(
            ILangfuseExperimentRun run,
            LangfuseExperimentItemScopeOptions<TCase>? options)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (run is not ILangfuseExperimentTrialLifecycleFactory lifecycleFactory)
        {
            throw new ArgumentException(
                "The supplied experiment run does not expose the built-in Langfuse trial lifecycle.",
                nameof(run));
        }

        return new LangfuseExperimentItemScopeProvider<TCase, TOutput>(
            lifecycleFactory,
            linkHostedItem: true,
            options);
    }

    /// <inheritdoc />
    LangfuseExperimentItemScopeProvider<TCase, TOutput>
        ILangfuseExperimentItemScopeProviderFactory.CreateLocalExperimentItemScopeProvider<TCase, TOutput>(
            LangfuseExperimentItemScopeOptions<TCase>? options) =>
        new(
            new LangfuseExperimentTrialLifecycleFactory(
                request => new LangfuseScenario(
                    Scores,
                    _composition.Recorder,
                    request.ScenarioName,
                    sessionId: null,
                    userId: null,
                    request.Tags,
                    request.Metadata,
                    activateOnCreate: false),
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
            _composition.Recorder,
            isEnabled: true,
            run,
            options);
    }

    LangfuseExperimentResultSink<TCase, TOutput>
        ILangfuseExperimentResultSinkFactory.CreateLocalExperimentResultSink<TCase, TOutput>(
            LangfuseExperimentResultSinkOptions<TCase, TOutput>? options) =>
        new(
            _composition.Recorder,
            isEnabled: true,
            run: null,
            options);

    /// <inheritdoc />
    public Task AddTraceCommentAsync(
        string traceId,
        string content,
        CancellationToken cancellationToken = default) =>
        _composition.CommentRecorder.AddTraceCommentAsync(traceId, content, cancellationToken);

    internal LangfuseClient(LangfuseClientComposition composition)
        : this(
            composition,
            composition.Scores,
            composition.Datasets,
            composition.ScoreConfigs,
            composition.Metrics,
            composition.Models,
            composition.Prompts)
    {
    }
}
