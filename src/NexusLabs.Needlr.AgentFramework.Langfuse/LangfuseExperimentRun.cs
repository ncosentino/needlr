namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Default <see cref="ILangfuseExperimentRun"/>. Starts a scenario per dataset item and links its
/// trace to the run via <c>POST /api/public/dataset-run-items</c>. Link failures are non-fatal —
/// routed to the diagnostics callback — so a Langfuse hiccup does not crash the eval; the gap is
/// surfaced rather than silently swallowed.
/// </summary>
internal sealed class LangfuseExperimentRun : ILangfuseExperimentRun
{
    private readonly LangfuseApiClient _apiClient;
    private readonly LangfuseScoreRecorder _recorder;
    private readonly ILangfuseScoreClient _scores;
    private readonly string? _runDescription;
    private readonly Action<string>? _diagnostics;

    public LangfuseExperimentRun(
        LangfuseApiClient apiClient,
        LangfuseScoreRecorder recorder,
        string datasetName,
        string runName,
        string? runDescription,
        Action<string>? diagnostics)
        : this(
            apiClient,
            CreateScoreClient(recorder),
            recorder,
            datasetName,
            runName,
            runDescription,
            diagnostics)
    {
    }

    public LangfuseExperimentRun(
        LangfuseApiClient apiClient,
        ILangfuseScoreClient scores,
        LangfuseScoreRecorder recorder,
        string datasetName,
        string runName,
        string? runDescription,
        Action<string>? diagnostics)
    {
        ArgumentNullException.ThrowIfNull(apiClient);
        ArgumentNullException.ThrowIfNull(scores);
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetName);
        ArgumentException.ThrowIfNullOrWhiteSpace(runName);

        _apiClient = apiClient;
        _scores = scores;
        _recorder = recorder;
        _runDescription = runDescription;
        _diagnostics = diagnostics;
        DatasetName = datasetName;
        RunName = runName;
    }

    private static ILangfuseScoreClient CreateScoreClient(LangfuseScoreRecorder recorder)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        return new LangfuseScoreClient(recorder, recorder.FailureSink);
    }

    /// <inheritdoc />
    public string DatasetName { get; }

    /// <inheritdoc />
    public string RunName { get; }

    /// <inheritdoc />
    public async Task<ILangfuseScenario> BeginItemAsync(
        string datasetItemId,
        string? scenarioName = null,
        IEnumerable<string>? tags = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetItemId);
        cancellationToken.ThrowIfCancellationRequested();

        var name = string.IsNullOrWhiteSpace(scenarioName)
            ? $"{DatasetName}: {datasetItemId}"
            : scenarioName;

        var scenario = new LangfuseScenario(
            _scores,
            _recorder,
            name,
            sessionId: null,
            userId: null,
            tags,
            metadata);

        try
        {
            if (scenario.TraceId is { Length: > 0 } traceId)
            {
                await LinkRunItemAsync(datasetItemId, traceId, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _diagnostics?.Invoke(
                    $"Langfuse dataset run item skipped for item '{datasetItemId}' in run '{RunName}': " +
                    "no sampled trace was available to link.");
            }

            return scenario;
        }
        catch
        {
            scenario.Dispose();
            throw;
        }
    }

    private async Task LinkRunItemAsync(string datasetItemId, string traceId, CancellationToken cancellationToken)
    {
        try
        {
            var request = new LangfuseCreateDatasetRunItemRequest
            {
                RunName = RunName,
                RunDescription = _runDescription,
                DatasetItemId = datasetItemId,
                TraceId = traceId,
            };

            await _apiClient
                .PostAsync("api/public/dataset-run-items", request, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (LangfuseException ex)
        {
            _diagnostics?.Invoke(
                $"Langfuse dataset run item link failed for item '{datasetItemId}' in run '{RunName}': {ex.Message}");
        }
    }
}
