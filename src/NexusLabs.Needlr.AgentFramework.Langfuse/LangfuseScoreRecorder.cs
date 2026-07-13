using System.Text;

using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Maps typed scores and <see cref="EvaluationResult"/> metrics to Langfuse score payloads, posts
/// them via <see cref="LangfuseScoreApiClient"/>, and routes failures through a
/// <see cref="LangfuseScoreFailureSink"/>. Shared by <see cref="LangfuseScenario"/> (session path),
/// <see cref="LangfuseScoreClient"/> (host path), and <see cref="LangfuseExperimentRun"/>
/// (dataset-run path) so the mapping lives in one place.
/// </summary>
internal sealed class LangfuseScoreRecorder
{
    private const string NumericDataType = "NUMERIC";
    private const string BooleanDataType = "BOOLEAN";
    private const string CategoricalDataType = "CATEGORICAL";

    private readonly LangfuseScoreApiClient _apiClient;
    private readonly LangfuseScoreFailureSink _failureSink;
    private readonly bool _normalizeNames;
    private readonly LangfusePublicationHealth _health;

    public LangfuseScoreRecorder(
        LangfuseScoreApiClient apiClient,
        LangfuseScoreFailureSink failureSink,
        bool normalizeNames,
        LangfusePublicationHealth? health = null)
    {
        ArgumentNullException.ThrowIfNull(apiClient);
        ArgumentNullException.ThrowIfNull(failureSink);

        _apiClient = apiClient;
        _failureSink = failureSink;
        _normalizeNames = normalizeNames;
        _health = health ?? new LangfusePublicationHealth(isEnabled: true);
    }

    public LangfuseScoreFailureSink FailureSink => _failureSink;

    public LangfusePublicationHealth Health => _health;

    internal static bool HasPublishableValue(EvaluationMetric metric)
    {
        ArgumentNullException.ThrowIfNull(metric);
        return metric is
            NumericMetric { Value: not null }
            or BooleanMetric { Value: not null }
            or StringMetric { Value.Length: > 0 };
    }

    public async Task RecordNumericAsync(string traceId, string name, double value, LangfuseScoreOptions? options, CancellationToken cancellationToken) =>
        _ = await RecordNumericResultAsync(
            LangfuseScoreTarget.Trace(traceId),
            name,
            value,
            options,
            resultObserver: null,
            cancellationToken).ConfigureAwait(false);

    public async Task RecordBooleanAsync(string traceId, string name, bool value, LangfuseScoreOptions? options, CancellationToken cancellationToken) =>
        _ = await RecordBooleanResultAsync(
            LangfuseScoreTarget.Trace(traceId),
            name,
            value,
            options,
            resultObserver: null,
            cancellationToken).ConfigureAwait(false);

    public async Task RecordCategoricalAsync(string traceId, string name, string value, LangfuseScoreOptions? options, CancellationToken cancellationToken) =>
        _ = await RecordCategoricalResultAsync(
            LangfuseScoreTarget.Trace(traceId),
            name,
            value,
            options,
            resultObserver: null,
            cancellationToken).ConfigureAwait(false);

    public async Task RecordNumericAsync(LangfuseScoreTarget target, string name, double value, LangfuseScoreOptions? options, CancellationToken cancellationToken) =>
        _ = await RecordNumericResultAsync(
            target,
            name,
            value,
            options,
            resultObserver: null,
            cancellationToken).ConfigureAwait(false);

    public async Task RecordBooleanAsync(LangfuseScoreTarget target, string name, bool value, LangfuseScoreOptions? options, CancellationToken cancellationToken) =>
        _ = await RecordBooleanResultAsync(
            target,
            name,
            value,
            options,
            resultObserver: null,
            cancellationToken).ConfigureAwait(false);

    public async Task RecordCategoricalAsync(LangfuseScoreTarget target, string name, string value, LangfuseScoreOptions? options, CancellationToken cancellationToken) =>
        _ = await RecordCategoricalResultAsync(
            target,
            name,
            value,
            options,
            resultObserver: null,
            cancellationToken).ConfigureAwait(false);

    public Task<LangfuseScoreRecordResult> RecordNumericResultAsync(
        LangfuseScoreTarget target,
        string name,
        double value,
        LangfuseScoreOptions? options,
        Action<LangfuseScoreRecordResult>? resultObserver,
        CancellationToken cancellationToken) =>
        SendAsync(target, name, value, NumericDataType, options, resultObserver, cancellationToken);

    public Task<LangfuseScoreRecordResult> RecordBooleanResultAsync(
        LangfuseScoreTarget target,
        string name,
        bool value,
        LangfuseScoreOptions? options,
        Action<LangfuseScoreRecordResult>? resultObserver,
        CancellationToken cancellationToken) =>
        SendAsync(target, name, value ? 1.0 : 0.0, BooleanDataType, options, resultObserver, cancellationToken);

    public Task<LangfuseScoreRecordResult> RecordCategoricalResultAsync(
        LangfuseScoreTarget target,
        string name,
        string value,
        LangfuseScoreOptions? options,
        Action<LangfuseScoreRecordResult>? resultObserver,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        return SendAsync(target, name, value, CategoricalDataType, options, resultObserver, cancellationToken);
    }

    public async Task RecordEvaluationAsync(
        string traceId,
        EvaluationResult result,
        LangfuseEvaluationScoreOptions? options,
        CancellationToken cancellationToken)
    {
        _ = await RecordEvaluationResultsAsync(
            LangfuseScoreTarget.Trace(traceId),
            result,
            options,
            resultObserver: null,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LangfuseScoreRecordResult>> RecordEvaluationResultsAsync(
        LangfuseScoreTarget target,
        EvaluationResult result,
        LangfuseEvaluationScoreOptions? options,
        Action<LangfuseScoreRecordResult>? resultObserver,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        var results = new List<LangfuseScoreRecordResult>();
        foreach (var metric in result.Metrics.Values)
        {
            var scoreOptions = CreateMetricScoreOptions(metric, options);
            LangfuseScoreRecordResult scoreResult;
            switch (metric)
            {
                case NumericMetric { Value: { } numeric }:
                    scoreResult = await RecordNumericResultAsync(
                        target,
                        metric.Name,
                        numeric,
                        scoreOptions,
                        resultObserver,
                        cancellationToken).ConfigureAwait(false);
                    break;
                case BooleanMetric { Value: { } boolean }:
                    scoreResult = await RecordBooleanResultAsync(
                        target,
                        metric.Name,
                        boolean,
                        scoreOptions,
                        resultObserver,
                        cancellationToken).ConfigureAwait(false);
                    break;
                case StringMetric { Value: { Length: > 0 } category }:
                    scoreResult = await RecordCategoricalResultAsync(
                        target,
                        metric.Name,
                        category,
                        scoreOptions,
                        resultObserver,
                        cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    scoreResult = new LangfuseScoreRecordResult(
                        scoreOptions?.Id,
                        metric.Name,
                        LangfuseScoreRecordStatus.Skipped,
                        failure: null);
                    resultObserver?.Invoke(scoreResult);
                    break;
            }

            results.Add(scoreResult);
        }

        return results;
    }

    private async Task<LangfuseScoreRecordResult> SendAsync(
        LangfuseScoreTarget target,
        string name,
        object value,
        string dataType,
        LangfuseScoreOptions? options,
        Action<LangfuseScoreRecordResult>? resultObserver,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (options?.Id is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(options.Id);
        }

        var normalizedName = NormalizeName(name);

        var score = new LangfuseScore
        {
            TraceId = target.TraceId,
            ObservationId = target.ObservationId,
            SessionId = target.SessionId,
            DatasetRunId = target.DatasetRunId,
            Name = normalizedName,
            Value = value,
            DataType = dataType,
            Comment = options?.Comment,
            Id = options?.Id,
        };

        _health.BeginScoreUpload();
        try
        {
            var scoreId = await _apiClient.CreateAsync(score, cancellationToken).ConfigureAwait(false);
            _health.CompleteScoreUpload(succeeded: true);
            var result = new LangfuseScoreRecordResult(
                scoreId,
                normalizedName,
                LangfuseScoreRecordStatus.Accepted,
                failure: null);
            resultObserver?.Invoke(result);
            return result;
        }
        catch (OperationCanceledException)
        {
            _health.CancelScoreUpload();
            throw;
        }
        catch (LangfuseException ex)
        {
            _health.CompleteScoreUpload(succeeded: false);
            var result = new LangfuseScoreRecordResult(
                score.Id,
                normalizedName,
                LangfuseScoreRecordStatus.Failed,
                ex);
            resultObserver?.Invoke(result);
            _failureSink.Record(name, target.TraceId, ex);
            return result;
        }
    }

    /// <summary>
    /// Records a score that could not be attached because no sampled trace was available (for
    /// example, head sampling dropped the scenario span). Routed through the failure sink so it is
    /// surfaced rather than silently lost.
    /// </summary>
    public Task RecordSkippedAsync(string name, CancellationToken cancellationToken) =>
        RecordSkippedAsync(
            name,
            $"Cannot record score '{name}': no sampled trace was available to attach it to. " +
            "Ensure the Langfuse session is enabled and sampling is not dropping the scenario span.",
            cancellationToken);

    /// <summary>
    /// Records a score that was skipped for a specific reason, routed through the failure sink so it
    /// is surfaced rather than silently lost.
    /// </summary>
    /// <param name="name">The score name.</param>
    /// <param name="message">The reason the score was skipped.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task RecordSkippedAsync(string name, string message, CancellationToken cancellationToken) =>
        _ = await RecordUnavailableResultAsync(
            name,
            message,
            resultObserver: null,
            cancellationToken).ConfigureAwait(false);

    public Task<LangfuseScoreRecordResult> RecordUnavailableResultAsync(
        string name,
        string message,
        Action<LangfuseScoreRecordResult>? resultObserver,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var failure = new LangfuseException(message);
        var result = new LangfuseScoreRecordResult(
            scoreId: null,
            name,
            LangfuseScoreRecordStatus.Failed,
            failure);
        resultObserver?.Invoke(result);

        try
        {
            _failureSink.Record(name, null, failure);
            return Task.FromResult(result);
        }
        catch (LangfuseException ex)
        {
            return Task.FromException<LangfuseScoreRecordResult>(ex);
        }
    }

    private string NormalizeName(string name) => _normalizeNames ? ToSnakeCase(name) : name;

    private static LangfuseScoreOptions? CreateMetricScoreOptions(
        EvaluationMetric metric,
        LangfuseEvaluationScoreOptions? options)
    {
        var id = options?.ScoreIdProvider?.Invoke(metric);
        if (id is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
        }

        return id is null && metric.Reason is null
            ? null
            : new LangfuseScoreOptions
            {
                Id = id,
                Comment = metric.Reason,
            };
    }

    private static string ToSnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length);
        var previousWasUnderscore = false;

        foreach (var ch in name.Trim())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
                previousWasUnderscore = false;
            }
            else if (!previousWasUnderscore && builder.Length > 0)
            {
                builder.Append('_');
                previousWasUnderscore = true;
            }
        }

        return builder.ToString().Trim('_');
    }
}
