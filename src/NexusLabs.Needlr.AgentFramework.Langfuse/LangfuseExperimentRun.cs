using System.Text.Json;

using Microsoft.Extensions.AI.Evaluation;

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
    private readonly LangfuseExperimentRunState _state;
    private readonly Action<string>? _diagnostics;

    public LangfuseExperimentRun(
        LangfuseApiClient apiClient,
        LangfuseScoreRecorder recorder,
        string datasetName,
        string runName,
        LangfuseExperimentRunOptions? options,
        Action<string>? diagnostics)
        : this(
            apiClient,
            CreateScoreClient(recorder),
            recorder,
            datasetName,
            runName,
            options,
            diagnostics)
    {
    }

    public LangfuseExperimentRun(
        LangfuseApiClient apiClient,
        ILangfuseScoreClient scores,
        LangfuseScoreRecorder recorder,
        string datasetName,
        string runName,
        LangfuseExperimentRunOptions? options,
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
        _state = new LangfuseExperimentRunState(disabled: false);
        _diagnostics = diagnostics;
        DatasetName = datasetName;
        RunName = runName;
        options ??= new LangfuseExperimentRunOptions();
        Description = options.NormalizeDescription();
        Metadata = options.FreezeMetadata();
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
    public string? Description { get; }

    /// <inheritdoc />
    public JsonElement? Metadata { get; }

    /// <inheritdoc />
    public string? DatasetRunId => _state.DatasetRunId;

    /// <inheritdoc />
    public LangfuseDatasetRunIdentityStatus IdentityStatus => _state.IdentityStatus;

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
            LangfuseExperimentItemLinkResult link;
            if (recordedTraceId is { Length: > 0 } traceId)
            {
                link = await LinkRunItemAsync(
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
                link = new LangfuseExperimentItemLinkResult(
                    LangfuseExperimentItemLinkStatus.NotSampled,
                    datasetRunItemId: null,
                    datasetRunId: null,
                    failure: null);
                _state.RecordItemLink(link.Status);
            }

            var value = await callback(scenario, cancellationToken).ConfigureAwait(false);
            return new LangfuseExperimentItemResult<T>(value, recordedTraceId, link);
        }
        finally
        {
            scenario.Dispose();
        }
    }

    /// <inheritdoc />
    public Task<LangfuseExperimentRunScoreResult> RecordScoreAsync(
        string name,
        double value,
        string? comment = null,
        CancellationToken cancellationToken = default) =>
        RecordScoreAsync(
            name,
            (target, observer, token) => _recorder.RecordNumericResultAsync(
                target,
                name,
                value,
                comment,
                observer,
                token),
            cancellationToken);

    /// <inheritdoc />
    public Task<LangfuseExperimentRunScoreResult> RecordScoreAsync(
        string name,
        bool value,
        string? comment = null,
        CancellationToken cancellationToken = default) =>
        RecordScoreAsync(
            name,
            (target, observer, token) => _recorder.RecordBooleanResultAsync(
                target,
                name,
                value,
                comment,
                observer,
                token),
            cancellationToken);

    /// <inheritdoc />
    public Task<LangfuseExperimentRunScoreResult> RecordScoreAsync(
        string name,
        string value,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        return RecordScoreAsync(
            name,
            (target, observer, token) => _recorder.RecordCategoricalResultAsync(
                target,
                name,
                value,
                comment,
                observer,
                token),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LangfuseExperimentRunScoreResult>> RecordEvaluationAsync(
        EvaluationResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        cancellationToken.ThrowIfCancellationRequested();

        var datasetRunId = DatasetRunId;
        if (datasetRunId is null)
        {
            var unavailable = new List<LangfuseExperimentRunScoreResult>();
            foreach (var metric in result.Metrics.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!LangfuseScoreRecorder.HasPublishableValue(metric))
                {
                    var skipped = new LangfuseExperimentRunScoreResult(
                        metric.Name,
                        LangfuseExperimentRunScoreStatus.Skipped,
                        datasetRunId: null,
                        failure: null);
                    _state.RecordRunScore(skipped.Status);
                    unavailable.Add(skipped);
                    continue;
                }

                unavailable.Add(
                    await RecordUnavailableScoreAsync(metric.Name, cancellationToken).ConfigureAwait(false));
            }

            return unavailable;
        }

        var outcomes = new List<LangfuseExperimentRunScoreResult>();
        _state.BeginOperation();
        try
        {
            await _recorder.RecordEvaluationResultsAsync(
                LangfuseScoreTarget.DatasetRun(datasetRunId),
                result,
                scoreResult =>
                {
                    var outcome = ToRunScoreResult(scoreResult, datasetRunId);
                    _state.RecordRunScore(outcome.Status);
                    outcomes.Add(outcome);
                },
                cancellationToken).ConfigureAwait(false);
            return outcomes;
        }
        finally
        {
            _state.EndOperation();
        }
    }

    /// <inheritdoc />
    public LangfuseExperimentRunPublicationSnapshot GetPublicationSnapshot() =>
        _state.GetSnapshot();

    private async Task<LangfuseExperimentItemLinkResult> LinkRunItemAsync(
        string datasetItemId,
        string traceId,
        LangfuseExperimentItemLinkFailureMode failureMode,
        CancellationToken cancellationToken)
    {
        _state.BeginOperation();
        try
        {
            var request = new LangfuseCreateDatasetRunItemRequest
            {
                RunName = RunName,
                RunDescription = Description,
                Metadata = Metadata,
                DatasetItemId = datasetItemId,
                TraceId = traceId,
            };

            LangfuseCreateDatasetRunItemResponse? response;
            try
            {
                response = await _apiClient
                    .PostAsync<LangfuseCreateDatasetRunItemRequest, LangfuseCreateDatasetRunItemResponse>(
                        "api/public/dataset-run-items",
                        request,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                return HandleLinkFailure(
                    datasetItemId,
                    traceId,
                    failureMode,
                    new LangfuseException("Langfuse returned an invalid dataset-run-item response.", ex),
                    LangfusePublicationFailureCode.InvalidResponse);
            }
            catch (LangfuseException ex)
            {
                return HandleLinkFailure(
                    datasetItemId,
                    traceId,
                    failureMode,
                    ex,
                    LangfusePublicationFailureCode.ApiRejected);
            }

            var validationFailure = ValidateLinkResponse(response, datasetItemId, traceId);
            if (validationFailure is not null)
            {
                return HandleLinkFailure(
                    datasetItemId,
                    traceId,
                    failureMode,
                    new LangfuseException(validationFailure.Message),
                    validationFailure.Code);
            }

            var status = _state.ObserveDatasetRunId(response!.DatasetRunId);
            if (status is LangfuseExperimentItemLinkStatus.Inconsistent)
            {
                var failure = new LangfusePublicationFailure(
                    LangfusePublicationFailureCode.InconsistentDatasetRunIdentity,
                    $"Langfuse returned dataset run '{response.DatasetRunId}' for item '{datasetItemId}', " +
                    "which conflicts with another successful item link.");
                var inconsistent = new LangfuseExperimentItemLinkResult(
                    status,
                    response.Id,
                    response.DatasetRunId,
                    failure);
                _state.RecordItemLink(status);
                _diagnostics?.Invoke(failure.Message);
                if (failureMode is LangfuseExperimentItemLinkFailureMode.Strict)
                {
                    throw new LangfuseException(failure.Message);
                }

                return inconsistent;
            }

            var linked = new LangfuseExperimentItemLinkResult(
                LangfuseExperimentItemLinkStatus.Linked,
                response.Id,
                response.DatasetRunId,
                failure: null);
            _state.RecordItemLink(linked.Status);
            return linked;
        }
        finally
        {
            _state.EndOperation();
        }
    }

    private LangfuseExperimentItemLinkResult HandleLinkFailure(
        string datasetItemId,
        string traceId,
        LangfuseExperimentItemLinkFailureMode failureMode,
        LangfuseException exception,
        LangfusePublicationFailureCode failureCode)
    {
        var failure = new LangfusePublicationFailure(failureCode, exception.Message);
        var result = new LangfuseExperimentItemLinkResult(
            LangfuseExperimentItemLinkStatus.Failed,
            datasetRunItemId: null,
            datasetRunId: null,
            failure);
        _state.RecordItemLink(result.Status);
        _diagnostics?.Invoke(
            $"Langfuse dataset run item link failed for item '{datasetItemId}' in run '{RunName}' " +
            $"for trace '{traceId}': {exception.Message}");
        if (failureMode is LangfuseExperimentItemLinkFailureMode.Strict)
        {
            throw exception;
        }

        return result;
    }

    private LangfusePublicationFailure? ValidateLinkResponse(
        LangfuseCreateDatasetRunItemResponse? response,
        string datasetItemId,
        string traceId)
    {
        if (response is null
            || string.IsNullOrWhiteSpace(response.Id)
            || string.IsNullOrWhiteSpace(response.DatasetRunId)
            || !string.Equals(response.DatasetRunName, RunName, StringComparison.Ordinal)
            || !string.Equals(response.DatasetItemId, datasetItemId, StringComparison.Ordinal)
            || !string.Equals(response.TraceId, traceId, StringComparison.Ordinal))
        {
            return new LangfusePublicationFailure(
                LangfusePublicationFailureCode.InvalidResponse,
                $"Langfuse returned an invalid dataset-run-item response for item '{datasetItemId}' in run '{RunName}'.");
        }

        return null;
    }

    private async Task<LangfuseExperimentRunScoreResult> RecordScoreAsync(
        string name,
        Func<
            LangfuseScoreTarget,
            Action<LangfuseScoreRecordResult>,
            CancellationToken,
            Task<LangfuseScoreRecordResult>> recordScore,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(recordScore);
        cancellationToken.ThrowIfCancellationRequested();

        var datasetRunId = DatasetRunId;
        if (datasetRunId is null)
        {
            return await RecordUnavailableScoreAsync(name, cancellationToken).ConfigureAwait(false);
        }

        LangfuseExperimentRunScoreResult? observed = null;
        _state.BeginOperation();
        try
        {
            var scoreResult = await recordScore(
                LangfuseScoreTarget.DatasetRun(datasetRunId),
                result =>
                {
                    observed = ToRunScoreResult(result, datasetRunId);
                    _state.RecordRunScore(observed.Status);
                },
                cancellationToken).ConfigureAwait(false);
            return observed ?? ToRunScoreResult(scoreResult, datasetRunId);
        }
        finally
        {
            _state.EndOperation();
        }
    }

    private async Task<LangfuseExperimentRunScoreResult> RecordUnavailableScoreAsync(
        string name,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _state.BeginOperation();
        try
        {
            var failureCode = IdentityStatus is LangfuseDatasetRunIdentityStatus.Inconsistent
                ? LangfusePublicationFailureCode.InconsistentDatasetRunIdentity
                : LangfusePublicationFailureCode.DatasetRunIdentityUnavailable;
            var message = IdentityStatus is LangfuseDatasetRunIdentityStatus.Inconsistent
                ? $"Cannot record dataset-run score '{name}': item links returned inconsistent dataset-run ids."
                : $"Cannot record dataset-run score '{name}': no successful item link has resolved the dataset-run id.";
            var failure = new LangfusePublicationFailure(failureCode, message);
            var result = new LangfuseExperimentRunScoreResult(
                name,
                LangfuseExperimentRunScoreStatus.NotAttempted,
                datasetRunId: null,
                failure);
            _state.RecordRunScore(result.Status);

            await _recorder.RecordUnavailableResultAsync(
                name,
                message,
                resultObserver: null,
                cancellationToken).ConfigureAwait(false);
            return result;
        }
        finally
        {
            _state.EndOperation();
        }
    }

    private static LangfuseExperimentRunScoreResult ToRunScoreResult(
        LangfuseScoreRecordResult result,
        string datasetRunId)
    {
        var status = result.Status switch
        {
            LangfuseScoreRecordStatus.Accepted => LangfuseExperimentRunScoreStatus.Accepted,
            LangfuseScoreRecordStatus.Failed => LangfuseExperimentRunScoreStatus.Failed,
            LangfuseScoreRecordStatus.Skipped => LangfuseExperimentRunScoreStatus.Skipped,
            _ => throw new ArgumentOutOfRangeException(nameof(result), result.Status, "The score record status is not defined."),
        };
        var failure = result.Failure is null
            ? null
            : new LangfusePublicationFailure(
                LangfusePublicationFailureCode.ApiRejected,
                result.Failure.Message);
        return new LangfuseExperimentRunScoreResult(
            result.Name,
            status,
            datasetRunId,
            failure);
    }
}
