using System.Diagnostics;

using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Active <see cref="ILangfuseScenario"/> backed by a root OpenTelemetry span and a shared
/// <see cref="LangfuseScoreRecorder"/>.
/// </summary>
internal sealed class LangfuseScenario : ILangfuseScenario
{
    private readonly Activity? _activity;
    private readonly LangfuseScoreRecorder _recorder;

    public LangfuseScenario(
        LangfuseScoreRecorder recorder,
        string name,
        string? sessionId,
        string? userId,
        IEnumerable<string>? tags,
        IReadOnlyDictionary<string, string>? metadata)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        _recorder = recorder;
        _activity = LangfuseActivitySource.Source.StartActivity(name, ActivityKind.Internal);

        ApplyTraceAttributes(_activity, name, sessionId, userId, tags, metadata);
    }

    /// <inheritdoc />
    public string? TraceId => _activity?.TraceId.ToString();

    /// <inheritdoc />
    public Activity? Activity => _activity;

    /// <inheritdoc />
    public Task RecordScoreAsync(string name, double value, string? comment = null, CancellationToken cancellationToken = default) =>
        TraceId is { Length: > 0 } id
            ? _recorder.RecordNumericAsync(id, name, value, comment, cancellationToken)
            : _recorder.RecordSkippedAsync(name, cancellationToken);

    /// <inheritdoc />
    public Task RecordScoreAsync(string name, bool value, string? comment = null, CancellationToken cancellationToken = default) =>
        TraceId is { Length: > 0 } id
            ? _recorder.RecordBooleanAsync(id, name, value, comment, cancellationToken)
            : _recorder.RecordSkippedAsync(name, cancellationToken);

    /// <inheritdoc />
    public Task RecordScoreAsync(string name, string value, string? comment = null, CancellationToken cancellationToken = default) =>
        TraceId is { Length: > 0 } id
            ? _recorder.RecordCategoricalAsync(id, name, value, comment, cancellationToken)
            : _recorder.RecordSkippedAsync(name, cancellationToken);

    /// <inheritdoc />
    public Task RecordEvaluationAsync(EvaluationResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        return TraceId is { Length: > 0 } id
            ? _recorder.RecordEvaluationAsync(id, result, cancellationToken)
            : _recorder.RecordSkippedAsync("evaluation", cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose() => _activity?.Dispose();

    private static void ApplyTraceAttributes(
        Activity? activity,
        string name,
        string? sessionId,
        string? userId,
        IEnumerable<string>? tags,
        IReadOnlyDictionary<string, string>? metadata)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetTag("langfuse.trace.name", name);

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            activity.SetTag("langfuse.session.id", sessionId);
            activity.SetBaggage("session.id", sessionId);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            activity.SetTag("langfuse.user.id", userId);
            activity.SetBaggage("user.id", userId);
        }

        if (tags is not null)
        {
            var tagArray = tags.Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();
            if (tagArray.Length > 0)
            {
                activity.SetTag("langfuse.trace.tags", tagArray);
            }
        }

        if (metadata is not null)
        {
            foreach (var entry in metadata)
            {
                if (!string.IsNullOrWhiteSpace(entry.Key))
                {
                    activity.SetTag($"langfuse.trace.metadata.{entry.Key}", entry.Value);
                }
            }
        }
    }
}
