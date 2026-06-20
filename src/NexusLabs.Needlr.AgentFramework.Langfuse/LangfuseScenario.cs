using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

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
    private readonly string? _sessionId;

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
        _sessionId = sessionId;
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

    /// <inheritdoc />
    public void SetTracePublic(bool isPublic = true) =>
        _activity?.SetTag("langfuse.trace.public", isPublic);

    /// <inheritdoc />
    public void SetVersion(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        _activity?.SetTag("langfuse.version", version);
    }

    /// <inheritdoc />
    public void SetInput(object input)
    {
        ArgumentNullException.ThrowIfNull(input);
        _activity?.SetTag("langfuse.trace.input", ToAttributeValue(input));
    }

    /// <inheritdoc />
    public void SetOutput(object output)
    {
        ArgumentNullException.ThrowIfNull(output);
        _activity?.SetTag("langfuse.trace.output", ToAttributeValue(output));
    }

    /// <inheritdoc />
    public void SetPrompt(string name, int? version = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (_activity is null)
        {
            return;
        }

        _activity.SetBaggage(LangfuseTraceAttributeProcessor.PromptNameBaggageKey, name);

        if (version is { } v)
        {
            _activity.SetBaggage(
                LangfuseTraceAttributeProcessor.PromptVersionBaggageKey,
                v.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static string ToAttributeValue(object value) =>
        value as string ?? JsonSerializer.Serialize(value);

    /// <inheritdoc />
    public Task RecordSessionScoreAsync(string name, double value, string? comment = null, CancellationToken cancellationToken = default) =>
        _sessionId is { Length: > 0 } sid
            ? _recorder.RecordNumericAsync(LangfuseScoreTarget.Session(sid), name, value, comment, cancellationToken)
            : SkipSessionScore(name, cancellationToken);

    /// <inheritdoc />
    public Task RecordSessionScoreAsync(string name, bool value, string? comment = null, CancellationToken cancellationToken = default) =>
        _sessionId is { Length: > 0 } sid
            ? _recorder.RecordBooleanAsync(LangfuseScoreTarget.Session(sid), name, value, comment, cancellationToken)
            : SkipSessionScore(name, cancellationToken);

    /// <inheritdoc />
    public Task RecordSessionScoreAsync(string name, string value, string? comment = null, CancellationToken cancellationToken = default) =>
        _sessionId is { Length: > 0 } sid
            ? _recorder.RecordCategoricalAsync(LangfuseScoreTarget.Session(sid), name, value, comment, cancellationToken)
            : SkipSessionScore(name, cancellationToken);

    private Task SkipSessionScore(string name, CancellationToken cancellationToken) =>
        _recorder.RecordSkippedAsync(
            name,
            $"Cannot record session score '{name}': this scenario has no session id. " +
            "Pass a sessionId when beginning the scenario to enable session-level scoring.",
            cancellationToken);

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
