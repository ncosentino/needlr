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

    public LangfuseScoreRecorder(
        LangfuseScoreApiClient apiClient,
        LangfuseScoreFailureSink failureSink,
        bool normalizeNames)
    {
        ArgumentNullException.ThrowIfNull(apiClient);
        ArgumentNullException.ThrowIfNull(failureSink);

        _apiClient = apiClient;
        _failureSink = failureSink;
        _normalizeNames = normalizeNames;
    }

    public LangfuseScoreFailureSink FailureSink => _failureSink;

    internal static bool HasPublishableValue(EvaluationMetric metric)
    {
        ArgumentNullException.ThrowIfNull(metric);
        return metric is
            NumericMetric { Value: not null }
            or BooleanMetric { Value: not null }
            or StringMetric { Value.Length: > 0 };
    }

    public async Task RecordNumericAsync(string traceId, string name, double value, string? comment, CancellationToken cancellationToken) =>
        _ = await RecordNumericResultAsync(
            LangfuseScoreTarget.Trace(traceId),
            name,
            value,
            comment,
            resultObserver: null,
            cancellationToken).ConfigureAwait(false);

    public async Task RecordBooleanAsync(string traceId, string name, bool value, string? comment, CancellationToken cancellationToken) =>
        _ = await RecordBooleanResultAsync(
            LangfuseScoreTarget.Trace(traceId),
            name,
            value,
            comment,
            resultObserver: null,
            cancellationToken).ConfigureAwait(false);

    public async Task RecordCategoricalAsync(string traceId, string name, string value, string? comment, CancellationToken cancellationToken) =>
        _ = await RecordCategoricalResultAsync(
            LangfuseScoreTarget.Trace(traceId),
            name,
            value,
            comment,
            resultObserver: null,
            cancellationToken).ConfigureAwait(false);

    public async Task RecordNumericAsync(LangfuseScoreTarget target, string name, double value, string? comment, CancellationToken cancellationToken) =>
        _ = await RecordNumericResultAsync(
            target,
            name,
            value,
            comment,
            resultObserver: null,
            cancellationToken).ConfigureAwait(false);

    public async Task RecordBooleanAsync(LangfuseScoreTarget target, string name, bool value, string? comment, CancellationToken cancellationToken) =>
        _ = await RecordBooleanResultAsync(
            target,
            name,
            value,
            comment,
            resultObserver: null,
            cancellationToken).ConfigureAwait(false);

    public async Task RecordCategoricalAsync(LangfuseScoreTarget target, string name, string value, string? comment, CancellationToken cancellationToken) =>
        _ = await RecordCategoricalResultAsync(
            target,
            name,
            value,
            comment,
            resultObserver: null,
            cancellationToken).ConfigureAwait(false);

    public Task<LangfuseScoreRecordResult> RecordNumericResultAsync(
        LangfuseScoreTarget target,
        string name,
        double value,
        string? comment,
        Action<LangfuseScoreRecordResult>? resultObserver,
        CancellationToken cancellationToken) =>
        SendAsync(target, name, value, NumericDataType, comment, resultObserver, cancellationToken);

    public Task<LangfuseScoreRecordResult> RecordBooleanResultAsync(
        LangfuseScoreTarget target,
        string name,
        bool value,
        string? comment,
        Action<LangfuseScoreRecordResult>? resultObserver,
        CancellationToken cancellationToken) =>
        SendAsync(target, name, value ? 1.0 : 0.0, BooleanDataType, comment, resultObserver, cancellationToken);

    public Task<LangfuseScoreRecordResult> RecordCategoricalResultAsync(
        LangfuseScoreTarget target,
        string name,
        string value,
        string? comment,
        Action<LangfuseScoreRecordResult>? resultObserver,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        return SendAsync(target, name, value, CategoricalDataType, comment, resultObserver, cancellationToken);
    }

    public async Task RecordEvaluationAsync(string traceId, EvaluationResult result, CancellationToken cancellationToken)
    {
        _ = await RecordEvaluationResultsAsync(
            LangfuseScoreTarget.Trace(traceId),
            result,
            resultObserver: null,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LangfuseScoreRecordResult>> RecordEvaluationResultsAsync(
        LangfuseScoreTarget target,
        EvaluationResult result,
        Action<LangfuseScoreRecordResult>? resultObserver,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        var results = new List<LangfuseScoreRecordResult>();
        foreach (var metric in result.Metrics.Values)
        {
            LangfuseScoreRecordResult scoreResult;
            switch (metric)
            {
                case NumericMetric { Value: { } numeric }:
                    scoreResult = await RecordNumericResultAsync(
                        target,
                        metric.Name,
                        numeric,
                        metric.Reason,
                        resultObserver,
                        cancellationToken).ConfigureAwait(false);
                    break;
                case BooleanMetric { Value: { } boolean }:
                    scoreResult = await RecordBooleanResultAsync(
                        target,
                        metric.Name,
                        boolean,
                        metric.Reason,
                        resultObserver,
                        cancellationToken).ConfigureAwait(false);
                    break;
                case StringMetric { Value: { Length: > 0 } category }:
                    scoreResult = await RecordCategoricalResultAsync(
                        target,
                        metric.Name,
                        category,
                        metric.Reason,
                        resultObserver,
                        cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    scoreResult = new LangfuseScoreRecordResult(
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
        string? comment,
        Action<LangfuseScoreRecordResult>? resultObserver,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
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
            Comment = comment,
        };

        try
        {
            await _apiClient.CreateAsync(score, cancellationToken).ConfigureAwait(false);
            var result = new LangfuseScoreRecordResult(
                normalizedName,
                LangfuseScoreRecordStatus.Accepted,
                failure: null);
            resultObserver?.Invoke(result);
            return result;
        }
        catch (LangfuseException ex)
        {
            var result = new LangfuseScoreRecordResult(
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
