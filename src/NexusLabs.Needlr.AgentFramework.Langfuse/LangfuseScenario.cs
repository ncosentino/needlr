using System.Diagnostics;
using System.Text.Json;

using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Active <see cref="ILangfuseScenario"/> backed by a root OpenTelemetry span and a shared
/// <see cref="LangfuseScoreRecorder"/>.
/// </summary>
[DoNotAutoRegister]
internal sealed class LangfuseScenario : ILangfuseActivatableScenario
{
    private readonly Activity? _activity;
    private readonly Activity? _initialPreviousActivity;
    private readonly bool _activatedOnCreate;
    private readonly LangfuseScoreRecorder _recorder;
    private readonly ILangfuseScoreClient _scores;
    private readonly string? _sessionId;
    private LangfuseTraceContext _traceContext;
    private int _disposed;

    public LangfuseScenario(
        LangfuseScoreRecorder recorder,
        string name,
        string? sessionId,
        string? userId,
        IEnumerable<string>? tags,
        IReadOnlyDictionary<string, string>? metadata,
        bool activateOnCreate = true)
        : this(
            CreateScoreClient(recorder),
            recorder,
            name,
            sessionId,
            userId,
            tags,
            metadata,
            activateOnCreate)
    {
    }

    public LangfuseScenario(
        ILangfuseScoreClient scores,
        LangfuseScoreRecorder recorder,
        string name,
        string? sessionId,
        string? userId,
        IEnumerable<string>? tags,
        IReadOnlyDictionary<string, string>? metadata,
        bool activateOnCreate = true)
    {
        ArgumentNullException.ThrowIfNull(scores);
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        _scores = scores;
        _recorder = recorder;
        _sessionId = sessionId;
        _traceContext = CreateTraceContext(name, sessionId, userId, tags, metadata);
        var previousActivity = Activity.Current;
        Activity? activity = null;
        Activity.Current = null;
        try
        {
            activity = LangfuseActivitySource.Source.StartActivity(name, ActivityKind.Internal);
        }
        finally
        {
            Activity.Current = previousActivity;
        }
        _activity = activity;

        ApplyTraceAttributes(_activity, _traceContext);
        LangfuseTraceContext.Attach(_activity, _traceContext);
        if (activateOnCreate && _activity is not null)
        {
            _initialPreviousActivity = Activity.Current;
            Activity.Current = _activity;
            _activatedOnCreate = true;
        }
    }

    private static ILangfuseScoreClient CreateScoreClient(LangfuseScoreRecorder recorder)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        return new LangfuseScoreClient(recorder, recorder.FailureSink);
    }

    /// <inheritdoc />
    public string? TraceId => _activity?.TraceId.ToString();

    /// <inheritdoc />
    public Activity? Activity => _activity;

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public IDisposable? Activate()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
        if (_activity is null)
        {
            return null;
        }

        var previousActivity = Activity.Current;
        Activity.Current = _activity;
        return new ScenarioActivation(_activity, previousActivity);
    }

    /// <inheritdoc />
    public Task RecordScoreAsync(string name, double value, LangfuseScoreOptions? options = null, CancellationToken cancellationToken = default) =>
        TraceId is { Length: > 0 } id
            ? _scores.RecordScoreAsync(id, name, value, options, cancellationToken)
            : _recorder.RecordSkippedAsync(name, cancellationToken);

    /// <inheritdoc />
    public Task RecordScoreAsync(string name, bool value, LangfuseScoreOptions? options = null, CancellationToken cancellationToken = default) =>
        TraceId is { Length: > 0 } id
            ? _scores.RecordScoreAsync(id, name, value, options, cancellationToken)
            : _recorder.RecordSkippedAsync(name, cancellationToken);

    /// <inheritdoc />
    public Task RecordScoreAsync(string name, string value, LangfuseScoreOptions? options = null, CancellationToken cancellationToken = default) =>
        TraceId is { Length: > 0 } id
            ? _scores.RecordScoreAsync(id, name, value, options, cancellationToken)
            : _recorder.RecordSkippedAsync(name, cancellationToken);

    /// <inheritdoc />
    public Task RecordEvaluationAsync(
        EvaluationResult result,
        LangfuseEvaluationScoreOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        return TraceId is { Length: > 0 } id
            ? _scores.RecordEvaluationAsync(id, result, options, cancellationToken)
            : _recorder.RecordSkippedAsync("evaluation", cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        var restoreInitialActivity =
            _activatedOnCreate
            && ReferenceEquals(Activity.Current, _activity);
        _activity?.Dispose();
        if (restoreInitialActivity)
        {
            Activity.Current = _initialPreviousActivity;
        }
    }

    /// <inheritdoc />
    public void SetTracePublic(bool isPublic = true) =>
        _activity?.SetTag("langfuse.trace.public", isPublic);

    /// <inheritdoc />
    public void SetVersion(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        _activity?.SetTag("langfuse.version", version);
        _traceContext = _traceContext with { Version = version };
        LangfuseTraceContext.Attach(_activity, _traceContext);
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
        _traceContext = _traceContext with { PromptName = name, PromptVersion = version };
        LangfuseTraceContext.Attach(_activity, _traceContext);
    }

    /// <inheritdoc />
    public void SetPrompt(LangfusePrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        SetPrompt(prompt.Name, prompt.Version);
    }

    private static string ToAttributeValue(object value) =>
        value as string ?? JsonSerializer.Serialize(value);

    /// <inheritdoc />
    public Task RecordSessionScoreAsync(string name, double value, LangfuseScoreOptions? options = null, CancellationToken cancellationToken = default) =>
        _sessionId is { Length: > 0 } sid
            ? _scores.RecordSessionScoreAsync(sid, name, value, options, cancellationToken)
            : SkipSessionScore(name, cancellationToken);

    /// <inheritdoc />
    public Task RecordSessionScoreAsync(string name, bool value, LangfuseScoreOptions? options = null, CancellationToken cancellationToken = default) =>
        _sessionId is { Length: > 0 } sid
            ? _scores.RecordSessionScoreAsync(sid, name, value, options, cancellationToken)
            : SkipSessionScore(name, cancellationToken);

    /// <inheritdoc />
    public Task RecordSessionScoreAsync(string name, string value, LangfuseScoreOptions? options = null, CancellationToken cancellationToken = default) =>
        _sessionId is { Length: > 0 } sid
            ? _scores.RecordSessionScoreAsync(sid, name, value, options, cancellationToken)
            : SkipSessionScore(name, cancellationToken);

    private Task SkipSessionScore(string name, CancellationToken cancellationToken) =>
        _recorder.RecordSkippedAsync(
            name,
            $"Cannot record session score '{name}': this scenario has no session id. " +
            "Pass a sessionId when beginning the scenario to enable session-level scoring.",
            cancellationToken);

    private static LangfuseTraceContext CreateTraceContext(
        string name,
        string? sessionId,
        string? userId,
        IEnumerable<string>? tags,
        IReadOnlyDictionary<string, string>? metadata)
    {
        var tagArray = tags?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToArray() ?? [];
        var metadataCopy = metadata?
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);

        return new LangfuseTraceContext
        {
            Name = name,
            SessionId = string.IsNullOrWhiteSpace(sessionId) ? null : sessionId,
            UserId = string.IsNullOrWhiteSpace(userId) ? null : userId,
            Tags = tagArray,
            Metadata = metadataCopy,
        };
    }

    private static void ApplyTraceAttributes(Activity? activity, LangfuseTraceContext context)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetTag("langfuse.trace.name", context.Name);

        if (context.SessionId is not null)
        {
            activity.SetTag("langfuse.session.id", context.SessionId);
        }

        if (context.UserId is not null)
        {
            activity.SetTag("langfuse.user.id", context.UserId);
        }

        if (context.Tags.Count > 0)
        {
            activity.SetTag("langfuse.trace.tags", context.Tags.ToArray());
        }

        foreach (var entry in context.Metadata)
        {
            activity.SetTag($"langfuse.trace.metadata.{entry.Key}", entry.Value);
        }
    }

    private sealed class ScenarioActivation(
        Activity activity,
        Activity? previousActivity) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0
                && ReferenceEquals(Activity.Current, activity))
            {
                Activity.Current = previousActivity;
            }
        }
    }
}
