namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Default <see cref="ILangfuseExperimentRun"/>. Executes each dataset item inside an active
/// scenario and links its trace to the run via <c>POST /api/public/dataset-run-items</c>.
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
    public async Task<LangfuseExperimentItemResult<T>> RunItemAsync<T>(
        string datasetItemId,
        Func<ILangfuseScenario, CancellationToken, Task<T>> callback,
        LangfuseExperimentItemOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetItemId);
        ArgumentNullException.ThrowIfNull(callback);
        cancellationToken.ThrowIfCancellationRequested();

        options ??= new LangfuseExperimentItemOptions();
        options.Validate();

        var name = string.IsNullOrWhiteSpace(options.ScenarioName)
            ? $"{DatasetName}: {datasetItemId}"
            : options.ScenarioName;

        var scenario = new LangfuseScenario(
            _scores,
            _recorder,
            name,
            sessionId: null,
            userId: null,
            options.Tags,
            options.Metadata);

        try
        {
            var recordedTraceId = scenario.Activity?.Recorded == true
                ? scenario.TraceId
                : null;
            LangfuseExperimentItemLinkStatus linkStatus;
            if (recordedTraceId is { Length: > 0 } traceId)
            {
                linkStatus = await LinkRunItemAsync(
                    datasetItemId,
                    traceId,
                    options.LinkFailureMode,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _diagnostics?.Invoke(
                    $"Langfuse dataset run item skipped for item '{datasetItemId}' in run '{RunName}': " +
                    "no sampled trace was available to link.");
                linkStatus = LangfuseExperimentItemLinkStatus.NotSampled;
            }

            var value = await callback(scenario, cancellationToken).ConfigureAwait(false);
            return new LangfuseExperimentItemResult<T>(value, recordedTraceId, linkStatus);
        }
        finally
        {
            scenario.Dispose();
        }
    }

    private async Task<LangfuseExperimentItemLinkStatus> LinkRunItemAsync(
        string datasetItemId,
        string traceId,
        LangfuseExperimentItemLinkFailureMode failureMode,
        CancellationToken cancellationToken)
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
            return LangfuseExperimentItemLinkStatus.Linked;
        }
        catch (LangfuseException ex)
            when (failureMode is LangfuseExperimentItemLinkFailureMode.BestEffort)
        {
            _diagnostics?.Invoke(
                $"Langfuse dataset run item link failed for item '{datasetItemId}' in run '{RunName}': {ex.Message}");
            return LangfuseExperimentItemLinkStatus.Failed;
        }
    }
}
