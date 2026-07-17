using System.Text.Json;

using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Default <see cref="ILangfuseExperimentRun"/>. Executes each dataset item inside an active
/// scenario and links its trace to the run via <c>POST /api/public/dataset-run-items</c>.
/// </summary>
[DoNotAutoRegister]
internal sealed class LangfuseExperimentRun :
    ILangfuseExperimentRun,
    ILangfuseExperimentTrialLifecycleFactory,
    ILangfuseExperimentItemLinker,
    ILangfuseExperimentScorePublisher
{
    private readonly LangfuseApiClient _apiClient;
    private readonly LangfuseScoreRecorder _recorder;
    private readonly ILangfuseScoreClient _scores;
    private readonly LangfuseExperimentRunState _state;
    private readonly Action<string>? _diagnostics;
    private readonly LangfusePublicationHealth _health;
    private readonly LangfuseExperimentTrialLifecycleFactory _lifecycleFactory;

    public LangfuseExperimentRun(
        LangfuseApiClient apiClient,
        LangfuseScoreRecorder recorder,
        string datasetName,
        string runName,
        LangfuseExperimentRunOptions? options,
        Action<string>? diagnostics,
        LangfusePublicationHealth? health = null)
        : this(
            apiClient,
            CreateScoreClient(recorder),
            recorder,
            datasetName,
            runName,
            options,
            diagnostics,
            health)
    {
    }

    public LangfuseExperimentRun(
        LangfuseApiClient apiClient,
        ILangfuseScoreClient scores,
        LangfuseScoreRecorder recorder,
        string datasetName,
        string runName,
        LangfuseExperimentRunOptions? options,
        Action<string>? diagnostics,
        LangfusePublicationHealth? health = null)
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
        _health = health ?? new LangfusePublicationHealth(isEnabled: true);
        DatasetName = datasetName;
        RunName = runName;
        options ??= new LangfuseExperimentRunOptions();
        Description = options.NormalizeDescription();
        DatasetVersion = options.NormalizeDatasetVersion();
        Metadata = options.FreezeMetadata();
        _lifecycleFactory = new LangfuseExperimentTrialLifecycleFactory(
            request => new LangfuseScenario(
                _scores,
                _recorder,
                request.ScenarioName,
                sessionId: null,
                userId: null,
                request.Tags,
                request.Metadata,
                activateOnCreate: false),
            this);
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
    public DateTimeOffset? DatasetVersion { get; }

    /// <inheritdoc />
    public JsonElement? Metadata { get; }

    /// <inheritdoc />
    public string? DatasetRunId => _state.DatasetRunId;

    /// <inheritdoc />
    public LangfuseDatasetRunIdentityStatus IdentityStatus => _state.IdentityStatus;

    /// <inheritdoc />
    public Task<LangfuseExperimentItemResult<T>> RunItemAsync<T>(
        string datasetItemId,
        Func<ILangfuseScenario, CancellationToken, Task<T>> callback,
        LangfuseExperimentItemOptions? options = null,
        CancellationToken cancellationToken = default) =>
        _lifecycleFactory.RunItemAsync(
            DatasetName,
            datasetItemId,
            callback,
            options,
            "A hosted Langfuse item lifecycle did not produce a link result.",
            cancellationToken);

    /// <inheritdoc />
    public Task<LangfuseExperimentRunScoreResult> RecordScoreAsync(
        string name,
        double value,
        LangfuseScoreOptions? options = null,
        CancellationToken cancellationToken = default) =>
        RecordScoreAsync(
            name,
            (target, observer, token) => _recorder.RecordNumericResultAsync(
                target,
                name,
                value,
                options,
                observer,
                token),
            options,
            observer: null,
            cancellationToken);

    /// <inheritdoc />
    public Task<LangfuseExperimentRunScoreResult> RecordScoreAsync(
        string name,
        bool value,
        LangfuseScoreOptions? options = null,
        CancellationToken cancellationToken = default) =>
        RecordScoreAsync(
            name,
            (target, observer, token) => _recorder.RecordBooleanResultAsync(
                target,
                name,
                value,
                options,
                observer,
                token),
            options,
            observer: null,
            cancellationToken);

    /// <inheritdoc />
    public Task<LangfuseExperimentRunScoreResult> RecordScoreAsync(
        string name,
        string value,
        LangfuseScoreOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        return RecordScoreAsync(
            name,
            (target, observer, token) => _recorder.RecordCategoricalResultAsync(
                target,
                name,
                value,
                options,
                observer,
                token),
            options,
            observer: null,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LangfuseExperimentRunScoreResult>> RecordEvaluationAsync(
        EvaluationResult result,
        LangfuseEvaluationScoreOptions? options = null,
        CancellationToken cancellationToken = default) =>
        await RecordEvaluationAsync(
            result,
            options,
            observer: null,
            cancellationToken).ConfigureAwait(false);

    async Task<IReadOnlyList<LangfuseExperimentRunScoreResult>>
        ILangfuseExperimentScorePublisher.RecordEvaluationAsync(
            EvaluationResult result,
            LangfuseEvaluationScoreOptions? options,
            Action<LangfuseExperimentRunScoreResult> observer,
            CancellationToken cancellationToken) =>
        await RecordEvaluationAsync(
            result,
            options,
            observer,
            cancellationToken).ConfigureAwait(false);

    Task<LangfuseExperimentRunScoreResult>
        ILangfuseExperimentScorePublisher.RecordCategoricalScoreAsync(
            string name,
            string value,
            LangfuseScoreOptions? options,
            Action<LangfuseExperimentRunScoreResult> observer,
            CancellationToken cancellationToken) =>
        RecordScoreAsync(
            name,
            (target, resultObserver, token) => _recorder.RecordCategoricalResultAsync(
                target,
                name,
                value,
                options,
                resultObserver,
                token),
            options,
            observer,
            cancellationToken);

    private async Task<IReadOnlyList<LangfuseExperimentRunScoreResult>> RecordEvaluationAsync(
        EvaluationResult result,
        LangfuseEvaluationScoreOptions? options,
        Action<LangfuseExperimentRunScoreResult>? observer,
        CancellationToken cancellationToken)
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
                        GetEvaluationScoreId(metric, options),
                        metric.Name,
                        LangfuseExperimentScoreStatus.Skipped,
                        datasetRunId: null,
                        failure: null);
                    _state.RecordRunScore(skipped.Status);
                    observer?.Invoke(skipped);
                    unavailable.Add(skipped);
                    continue;
                }

                unavailable.Add(
                    await RecordUnavailableScoreAsync(
                        metric.Name,
                        GetEvaluationScoreId(metric, options),
                        observer,
                        cancellationToken).ConfigureAwait(false));
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
                options,
                scoreResult =>
                {
                    var outcome = ToRunScoreResult(scoreResult, datasetRunId);
                    _state.RecordRunScore(outcome.Status);
                    observer?.Invoke(outcome);
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

    ValueTask<LangfuseExperimentTrialLifecycle>
        ILangfuseExperimentTrialLifecycleFactory.EnterAsync(
            LangfuseExperimentTrialLifecycleRequest request,
            CancellationToken cancellationToken) =>
        _lifecycleFactory.EnterAsync(request, cancellationToken);

    async ValueTask<LangfuseExperimentItemLinkResult>
        ILangfuseExperimentItemLinker.CreateLinkAsync(
            string datasetItemId,
            string? recordedTraceId,
            LangfuseExperimentItemLinkFailureMode failureMode,
            CancellationToken cancellationToken)
    {
        if (recordedTraceId is not { Length: > 0 } traceId)
        {
            _diagnostics?.Invoke(
                $"Langfuse dataset run item skipped for item '{datasetItemId}' in run '{RunName}': " +
                "no sampled trace was available to link.");
            var notSampled = new LangfuseExperimentItemLinkResult(
                LangfuseExperimentItemLinkStatus.NotSampled,
                datasetRunItemId: null,
                datasetRunId: null,
                failure: null);
            _state.RecordItemLink(notSampled.Status);
            return notSampled;
        }

        return await LinkRunItemAsync(
            datasetItemId,
            traceId,
            failureMode,
            cancellationToken).ConfigureAwait(false);
    }

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
                DatasetVersion = DatasetVersion,
            };

            LangfuseCreateDatasetRunItemResponse? response;
            _health.BeginItemLink();
            try
            {
                response = await _apiClient
                    .PostAsync<LangfuseCreateDatasetRunItemRequest, LangfuseCreateDatasetRunItemResponse>(
                        "api/public/dataset-run-items",
                        request,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _health.CancelItemLink();
                throw;
            }
            catch (JsonException ex)
            {
                _health.CompleteItemLink(succeeded: false);
                return HandleLinkFailure(
                    datasetItemId,
                    traceId,
                    failureMode,
                    new LangfuseException("Langfuse returned an invalid dataset-run-item response.", ex),
                    LangfusePublicationFailureCode.InvalidResponse);
            }
            catch (LangfuseException ex)
            {
                _health.CompleteItemLink(succeeded: false);
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
                _health.CompleteItemLink(succeeded: false);
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
                _health.CompleteItemLink(succeeded: false);
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
            _health.CompleteItemLink(succeeded: true);
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
        LangfuseScoreOptions? options,
        Action<LangfuseExperimentRunScoreResult>? observer,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(recordScore);
        cancellationToken.ThrowIfCancellationRequested();
        var scoreId = GetScoreId(options);

        var datasetRunId = DatasetRunId;
        if (datasetRunId is null)
        {
            return await RecordUnavailableScoreAsync(
                name,
                scoreId,
                observer,
                cancellationToken).ConfigureAwait(false);
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
                    observer?.Invoke(observed);
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
        string? scoreId,
        Action<LangfuseExperimentRunScoreResult>? observer,
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
                scoreId,
                name,
                LangfuseExperimentScoreStatus.NotAttempted,
                datasetRunId: null,
                failure);
            _state.RecordRunScore(result.Status);
            observer?.Invoke(result);

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
            LangfuseScoreRecordStatus.Accepted => LangfuseExperimentScoreStatus.Accepted,
            LangfuseScoreRecordStatus.Failed => LangfuseExperimentScoreStatus.Failed,
            LangfuseScoreRecordStatus.Skipped => LangfuseExperimentScoreStatus.Skipped,
            _ => throw new ArgumentOutOfRangeException(nameof(result), result.Status, "The score record status is not defined."),
        };
        var failure = result.Failure is null
            ? null
            : new LangfusePublicationFailure(
                LangfusePublicationFailureCode.ApiRejected,
                result.Failure.Message);
        return new LangfuseExperimentRunScoreResult(
            result.ScoreId,
            result.Name,
            status,
            datasetRunId,
            failure);
    }

    private static string? GetScoreId(LangfuseScoreOptions? options)
    {
        if (options?.Id is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(options.Id);
        }

        return options?.Id;
    }

    private static string? GetEvaluationScoreId(
        EvaluationMetric metric,
        LangfuseEvaluationScoreOptions? options)
    {
        var scoreId = options?.ScoreIdProvider?.Invoke(metric);
        if (scoreId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scoreId);
        }

        return scoreId;
    }
}
