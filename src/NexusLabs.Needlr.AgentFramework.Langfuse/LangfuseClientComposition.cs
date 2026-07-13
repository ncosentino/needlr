namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Owns the shared REST API, score recording, failure handling, and default specialized clients
/// used to compose hosted and standalone Langfuse facades.
/// </summary>
internal sealed class LangfuseClientComposition
{
    public LangfuseClientComposition(
        LangfuseHttpTransport transport,
        LangfuseEndpoints endpoints,
        LangfuseOptions options,
        ILangfuseResourceLockProvider? resourceLockProvider = null,
        LangfusePublicationHealth? health = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(options);
        resourceLockProvider ??= options.ResourceLockProvider;
        ArgumentNullException.ThrowIfNull(resourceLockProvider);
        Health = health ?? new LangfusePublicationHealth(isEnabled: true);

        ApiClient = new LangfuseApiClient(
            transport.HttpClient,
            endpoints.BaseUrl,
            endpoints.AuthorizationHeaderValue,
            options.Http,
            timeProvider: null,
            health: Health);
        var scoreApiClient = new LangfuseScoreApiClient(ApiClient);
        FailureSink = new LangfuseScoreFailureSink(
            options.ScoreFailureMode,
            options.ScoreErrorCallback);
        Recorder = new LangfuseScoreRecorder(
            scoreApiClient,
            FailureSink,
            options.NormalizeScoreNames,
            Health);
        CommentRecorder = new LangfuseCommentRecorder(
            ApiClient,
            options.DiagnosticsCallback);
        Diagnostics = options.DiagnosticsCallback;
        var resourceLockScope = $"{endpoints.BaseUrl.AbsoluteUri}\n{options.PublicKey}";

        Scores = new LangfuseScoreClient(Recorder, FailureSink);
        Datasets = new LangfuseDatasetClient(ApiClient);
        ScoreConfigs = new LangfuseScoreConfigClient(
            ApiClient,
            resourceLockProvider,
            resourceLockScope);
        Metrics = new LangfuseMetricsClient(ApiClient);
        Models = new LangfuseModelClient(
            ApiClient,
            resourceLockProvider,
            resourceLockScope);
        Prompts = new LangfusePromptClient(ApiClient);
    }

    public LangfuseApiClient ApiClient { get; }

    public LangfusePublicationHealth Health { get; }

    public LangfuseScoreFailureSink FailureSink { get; }

    public LangfuseScoreRecorder Recorder { get; }

    public LangfuseCommentRecorder CommentRecorder { get; }

    public Action<string>? Diagnostics { get; }

    public ILangfuseScoreClient Scores { get; }

    public ILangfuseDatasetClient Datasets { get; }

    public ILangfuseScoreConfigClient ScoreConfigs { get; }

    public ILangfuseMetricsClient Metrics { get; }

    public ILangfuseModelClient Models { get; }

    public ILangfusePromptClient Prompts { get; }
}
