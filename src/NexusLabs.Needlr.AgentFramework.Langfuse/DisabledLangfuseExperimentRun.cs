using System.Text.Json;

using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Inert <see cref="ILangfuseExperimentRun"/> returned by a disabled session. Executes callbacks
/// with inert scenarios and links nothing, so experiment code runs unchanged without credentials.
/// </summary>
[DoNotAutoRegister]
internal sealed class DisabledLangfuseExperimentRun :
    ILangfuseExperimentRun,
    ILangfuseExperimentTrialLifecycleFactory,
    ILangfuseExperimentItemLinker,
    ILangfuseExperimentScorePublisher
{
    private readonly LangfuseExperimentRunState _state = new(disabled: true);
    private readonly LangfuseExperimentTrialLifecycleFactory _lifecycleFactory;

    public DisabledLangfuseExperimentRun(
        string datasetName,
        string runName,
        LangfuseExperimentRunOptions? options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetName);
        ArgumentException.ThrowIfNullOrWhiteSpace(runName);

        DatasetName = datasetName;
        RunName = runName;
        options ??= new LangfuseExperimentRunOptions();
        Description = options.NormalizeDescription();
        DatasetVersion = options.NormalizeDatasetVersion();
        Metadata = options.FreezeMetadata();
        _lifecycleFactory = new LangfuseExperimentTrialLifecycleFactory(
            _ => new DisabledLangfuseScenario(),
            this);
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
    public string? DatasetRunId => null;

    /// <inheritdoc />
    public LangfuseDatasetRunIdentityStatus IdentityStatus =>
        LangfuseDatasetRunIdentityStatus.Disabled;

    /// <inheritdoc />
    public Task<LangfuseExperimentItemResult<T>> RunItemAsync<T>(
        string datasetItemId,
        Func<ILangfuseScenario, CancellationToken, Task<T>> callback) =>
        RunItemAsync(
            datasetItemId,
            callback,
            options: null,
            CancellationToken.None);

    /// <inheritdoc />
    public Task<LangfuseExperimentItemResult<T>> RunItemAsync<T>(
        string datasetItemId,
        Func<ILangfuseScenario, CancellationToken, Task<T>> callback,
        LangfuseExperimentItemOptions? options,
        CancellationToken cancellationToken) =>
        _lifecycleFactory.RunItemAsync(
            DatasetName,
            datasetItemId,
            callback,
            options,
            "A disabled hosted Langfuse item lifecycle did not produce a link result.",
            cancellationToken);

    /// <inheritdoc />
    public Task<LangfuseExperimentRunScoreResult> RecordScoreAsync(
        string name,
        double value) =>
        RecordScoreAsync(name, value, options: null, CancellationToken.None);

    /// <inheritdoc />
    public Task<LangfuseExperimentRunScoreResult> RecordScoreAsync(
        string name,
        double value,
        LangfuseScoreOptions? options,
        CancellationToken cancellationToken) =>
        RecordDisabledScoreAsync(name, options, cancellationToken);

    /// <inheritdoc />
    public Task<LangfuseExperimentRunScoreResult> RecordScoreAsync(
        string name,
        bool value) =>
        RecordScoreAsync(name, value, options: null, CancellationToken.None);

    /// <inheritdoc />
    public Task<LangfuseExperimentRunScoreResult> RecordScoreAsync(
        string name,
        bool value,
        LangfuseScoreOptions? options,
        CancellationToken cancellationToken) =>
        RecordDisabledScoreAsync(name, options, cancellationToken);

    /// <inheritdoc />
    public Task<LangfuseExperimentRunScoreResult> RecordScoreAsync(
        string name,
        string value) =>
        RecordScoreAsync(name, value, options: null, CancellationToken.None);

    /// <inheritdoc />
    public Task<LangfuseExperimentRunScoreResult> RecordScoreAsync(
        string name,
        string value,
        LangfuseScoreOptions? options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        return RecordDisabledScoreAsync(name, options, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<LangfuseExperimentRunScoreResult>> RecordEvaluationAsync(
        EvaluationResult result) =>
        RecordEvaluationAsync(result, options: null, CancellationToken.None);

    /// <inheritdoc />
    public Task<IReadOnlyList<LangfuseExperimentRunScoreResult>> RecordEvaluationAsync(
        EvaluationResult result,
        LangfuseEvaluationScoreOptions? options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        cancellationToken.ThrowIfCancellationRequested();

        var outcomes = result.Metrics.Values
            .Select(metric => LangfuseScoreRecorder.HasPublishableValue(metric)
                ? CreateDisabledScore(metric.Name, GetEvaluationScoreId(metric, options))
                : CreateSkippedScore(metric.Name, GetEvaluationScoreId(metric, options)))
            .ToArray();
        return Task.FromResult<IReadOnlyList<LangfuseExperimentRunScoreResult>>(outcomes);
    }

    async Task<IReadOnlyList<LangfuseExperimentRunScoreResult>>
        ILangfuseExperimentScorePublisher.RecordEvaluationAsync(
            EvaluationResult result,
            LangfuseEvaluationScoreOptions? options,
            Action<LangfuseExperimentRunScoreResult> observer,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observer);
            var scores = await RecordEvaluationAsync(
                result,
                options,
                cancellationToken).ConfigureAwait(false);
            foreach (var score in scores)
            {
                observer(score);
            }

            return scores;
    }

    async Task<LangfuseExperimentRunScoreResult>
            ILangfuseExperimentScorePublisher.RecordCategoricalScoreAsync(
            string name,
            string value,
            LangfuseScoreOptions? options,
            Action<LangfuseExperimentRunScoreResult> observer,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observer);
        var score = await RecordScoreAsync(
            name,
            value,
            options,
            cancellationToken).ConfigureAwait(false);
        observer(score);
        return score;
    }

    /// <inheritdoc />
    public LangfuseExperimentRunPublicationSnapshot GetPublicationSnapshot() =>
        _state.GetSnapshot();

    ValueTask<LangfuseExperimentTrialLifecycle>
        ILangfuseExperimentTrialLifecycleFactory.EnterAsync(
            LangfuseExperimentTrialLifecycleRequest request,
            CancellationToken cancellationToken) =>
        _lifecycleFactory.EnterAsync(request, cancellationToken);

    ValueTask<LangfuseExperimentItemLinkResult>
        ILangfuseExperimentItemLinker.CreateLinkAsync(
            string datasetItemId,
            string? recordedTraceId,
            LangfuseExperimentItemLinkFailureMode failureMode,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var link = new LangfuseExperimentItemLinkResult(
            LangfuseExperimentItemLinkStatus.Disabled,
            datasetRunItemId: null,
            datasetRunId: null,
            failure: null);
        _state.RecordItemLink(link.Status);
        return ValueTask.FromResult(link);
    }

    private Task<LangfuseExperimentRunScoreResult> RecordDisabledScoreAsync(
        string name,
        LangfuseScoreOptions? options,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CreateDisabledScore(name, GetScoreId(options)));
    }

    private LangfuseExperimentRunScoreResult CreateDisabledScore(string name, string? scoreId)
    {
        var result = new LangfuseExperimentRunScoreResult(
            scoreId,
            name,
            LangfuseExperimentScoreStatus.Disabled,
            datasetRunId: null,
            failure: null);
        _state.RecordRunScore(result.Status);
        return result;
    }

    private LangfuseExperimentRunScoreResult CreateSkippedScore(string name, string? scoreId)
    {
        var result = new LangfuseExperimentRunScoreResult(
            scoreId,
            name,
            LangfuseExperimentScoreStatus.Skipped,
            datasetRunId: null,
            failure: null);
        _state.RecordRunScore(result.Status);
        return result;
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
