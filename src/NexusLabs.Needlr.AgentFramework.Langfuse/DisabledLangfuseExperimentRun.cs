using System.Text.Json;

using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Inert <see cref="ILangfuseExperimentRun"/> returned by a disabled session. Executes callbacks
/// with inert scenarios and links nothing, so experiment code runs unchanged without credentials.
/// </summary>
[DoNotAutoRegister]
internal sealed class DisabledLangfuseExperimentRun : ILangfuseExperimentRun
{
    private readonly LangfuseExperimentRunState _state = new(disabled: true);

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
        Metadata = options.FreezeMetadata();
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
    public string? DatasetRunId => null;

    /// <inheritdoc />
    public LangfuseDatasetRunIdentityStatus IdentityStatus =>
        LangfuseDatasetRunIdentityStatus.Disabled;

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

        using var scenario = new DisabledLangfuseScenario();
        var value = await callback(scenario, cancellationToken).ConfigureAwait(false);
        var link = new LangfuseExperimentItemLinkResult(
            LangfuseExperimentItemLinkStatus.Disabled,
            datasetRunItemId: null,
            datasetRunId: null,
            failure: null);
        _state.RecordItemLink(link.Status);
        return new LangfuseExperimentItemResult<T>(
            value,
            traceId: null,
            link);
    }

    /// <inheritdoc />
    public Task<LangfuseExperimentRunScoreResult> RecordScoreAsync(
        string name,
        double value,
        LangfuseScoreOptions? options = null,
        CancellationToken cancellationToken = default) =>
        RecordDisabledScoreAsync(name, options, cancellationToken);

    /// <inheritdoc />
    public Task<LangfuseExperimentRunScoreResult> RecordScoreAsync(
        string name,
        bool value,
        LangfuseScoreOptions? options = null,
        CancellationToken cancellationToken = default) =>
        RecordDisabledScoreAsync(name, options, cancellationToken);

    /// <inheritdoc />
    public Task<LangfuseExperimentRunScoreResult> RecordScoreAsync(
        string name,
        string value,
        LangfuseScoreOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        return RecordDisabledScoreAsync(name, options, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<LangfuseExperimentRunScoreResult>> RecordEvaluationAsync(
        EvaluationResult result,
        LangfuseEvaluationScoreOptions? options = null,
        CancellationToken cancellationToken = default)
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

    /// <inheritdoc />
    public LangfuseExperimentRunPublicationSnapshot GetPublicationSnapshot() =>
        _state.GetSnapshot();

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
            LangfuseExperimentRunScoreStatus.Disabled,
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
            LangfuseExperimentRunScoreStatus.Skipped,
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
