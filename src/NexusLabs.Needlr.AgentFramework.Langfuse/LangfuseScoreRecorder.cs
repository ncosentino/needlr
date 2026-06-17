using System.Text;

using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Maps typed scores and <see cref="EvaluationResult"/> metrics to Langfuse score payloads, posts
/// them via <see cref="LangfuseScoreApiClient"/>, and routes failures through a
/// <see cref="LangfuseScoreFailureSink"/>. Shared by <see cref="LangfuseScenario"/> (session path)
/// and <see cref="LangfuseScoreClient"/> (host path) so the mapping lives in one place.
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

    public Task RecordNumericAsync(string traceId, string name, double value, string? comment, CancellationToken cancellationToken) =>
        SendAsync(traceId, name, value, NumericDataType, comment, cancellationToken);

    public Task RecordBooleanAsync(string traceId, string name, bool value, string? comment, CancellationToken cancellationToken) =>
        SendAsync(traceId, name, value ? 1.0 : 0.0, BooleanDataType, comment, cancellationToken);

    public Task RecordCategoricalAsync(string traceId, string name, string value, string? comment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        return SendAsync(traceId, name, value, CategoricalDataType, comment, cancellationToken);
    }

    public async Task RecordEvaluationAsync(string traceId, EvaluationResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        foreach (var metric in result.Metrics.Values)
        {
            switch (metric)
            {
                case NumericMetric { Value: { } numeric }:
                    await RecordNumericAsync(traceId, metric.Name, numeric, metric.Reason, cancellationToken).ConfigureAwait(false);
                    break;
                case BooleanMetric { Value: { } boolean }:
                    await RecordBooleanAsync(traceId, metric.Name, boolean, metric.Reason, cancellationToken).ConfigureAwait(false);
                    break;
                case StringMetric { Value: { Length: > 0 } category }:
                    await RecordCategoricalAsync(traceId, metric.Name, category, metric.Reason, cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
    }

    private async Task SendAsync(string traceId, string name, object value, string dataType, string? comment, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(traceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var score = new LangfuseScore
        {
            TraceId = traceId,
            Name = NormalizeName(name),
            Value = value,
            DataType = dataType,
            Comment = comment,
        };

        try
        {
            await _apiClient.CreateAsync(score, cancellationToken).ConfigureAwait(false);
        }
        catch (LangfuseException ex)
        {
            _failureSink.Record(name, traceId, ex);
        }
    }

    /// <summary>
    /// Records a score that could not be attached because no sampled trace was available (for
    /// example, head sampling dropped the scenario span). Routed through the failure sink so it is
    /// surfaced rather than silently lost.
    /// </summary>
    public Task RecordSkippedAsync(string name, CancellationToken cancellationToken)
    {
        var failure = new LangfuseException(
            $"Cannot record score '{name}': no sampled trace was available to attach it to. " +
            "Ensure the Langfuse session is enabled and sampling is not dropping the scenario span.");

        try
        {
            _failureSink.Record(name, null, failure);
            return Task.CompletedTask;
        }
        catch (LangfuseException ex)
        {
            return Task.FromException(ex);
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
