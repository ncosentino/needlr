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
        LangfuseOptions options)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(options);

        var scoreApiClient = new LangfuseScoreApiClient(
            transport.HttpClient,
            endpoints.ScoresEndpoint,
            endpoints.AuthorizationHeaderValue);
        ApiClient = new LangfuseApiClient(
            transport.HttpClient,
            endpoints.BaseUrl,
            endpoints.AuthorizationHeaderValue);
        FailureSink = new LangfuseScoreFailureSink(
            options.ScoreFailureMode,
            options.ScoreErrorCallback);
        Recorder = new LangfuseScoreRecorder(
            scoreApiClient,
            FailureSink,
            options.NormalizeScoreNames);
        CommentRecorder = new LangfuseCommentRecorder(
            ApiClient,
            options.DiagnosticsCallback);
        Diagnostics = options.DiagnosticsCallback;

        Scores = new LangfuseScoreClient(Recorder, FailureSink);
        Datasets = new LangfuseDatasetClient(ApiClient);
        ScoreConfigs = new LangfuseScoreConfigClient(ApiClient);
        Metrics = new LangfuseMetricsClient(ApiClient);
        Models = new LangfuseModelClient(ApiClient);
        Prompts = new LangfusePromptClient(ApiClient);
    }

    public LangfuseApiClient ApiClient { get; }

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
