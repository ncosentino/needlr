using System.Threading;

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Active <see cref="ILangfuseSession"/> backed by OpenTelemetry tracer and (optionally) meter
/// providers that export to Langfuse over OTLP/HTTP.
/// </summary>
internal sealed class LangfuseSession : ILangfuseSession
{
    private readonly TracerProvider _tracerProvider;
    private readonly MeterProvider? _meterProvider;
    private readonly HttpClient _httpClient;
    private readonly LangfuseScoreRecorder _recorder;
    private readonly LangfuseScoreFailureSink _failureSink;
    private readonly LangfuseCommentRecorder _commentRecorder;
    private readonly LangfuseApiClient _apiClient;
    private readonly Action<string>? _diagnostics;
    private int _disposed;

    public LangfuseSession(
        TracerProvider tracerProvider,
        MeterProvider? meterProvider,
        HttpClient httpClient,
        LangfuseScoreRecorder recorder,
        LangfuseScoreFailureSink failureSink,
        LangfuseCommentRecorder commentRecorder,
        LangfuseApiClient apiClient,
        Action<string>? diagnostics)
    {
        ArgumentNullException.ThrowIfNull(tracerProvider);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentNullException.ThrowIfNull(failureSink);
        ArgumentNullException.ThrowIfNull(commentRecorder);
        ArgumentNullException.ThrowIfNull(apiClient);

        _tracerProvider = tracerProvider;
        _meterProvider = meterProvider;
        _httpClient = httpClient;
        _recorder = recorder;
        _failureSink = failureSink;
        _commentRecorder = commentRecorder;
        _apiClient = apiClient;
        _diagnostics = diagnostics;

        Datasets = new LangfuseDatasetClient(apiClient);
        ScoreConfigs = new LangfuseScoreConfigClient(apiClient);
    }

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public int ScoresFailed => _failureSink.FailedCount;

    /// <inheritdoc />
    public ILangfuseDatasetClient Datasets { get; }

    /// <inheritdoc />
    public ILangfuseScoreConfigClient ScoreConfigs { get; }

    /// <inheritdoc />
    public bool Flush(TimeSpan? timeout = null)
    {
        var timeoutMs = ToTimeoutMilliseconds(timeout);
        var traces = _tracerProvider.ForceFlush(timeoutMs);
        var metrics = _meterProvider?.ForceFlush(timeoutMs) ?? true;
        return traces && metrics;
    }

    /// <inheritdoc />
    public ILangfuseScenario BeginScenario(
        string name,
        string? sessionId = null,
        string? userId = null,
        IEnumerable<string>? tags = null,
        IReadOnlyDictionary<string, string>? metadata = null) =>
        new LangfuseScenario(_recorder, name, sessionId, userId, tags, metadata);

    /// <inheritdoc />
    public ILangfuseExperimentRun BeginExperimentRun(string datasetName, string runName, string? runDescription = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetName);
        ArgumentException.ThrowIfNullOrWhiteSpace(runName);

        return new LangfuseExperimentRun(
            _apiClient,
            _recorder,
            datasetName,
            runName,
            runDescription,
            _diagnostics);
    }

    /// <inheritdoc />
    public Task AddTraceCommentAsync(string traceId, string content, CancellationToken cancellationToken = default) =>
        _commentRecorder.AddTraceCommentAsync(traceId, content, cancellationToken);

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Flush();
        _tracerProvider.Dispose();
        _meterProvider?.Dispose();
        _httpClient.Dispose();
    }

    private static int ToTimeoutMilliseconds(TimeSpan? timeout)
    {
        if (timeout is null)
        {
            return Timeout.Infinite;
        }

        if (timeout.Value == Timeout.InfiniteTimeSpan)
        {
            return Timeout.Infinite;
        }

        var ms = (long)timeout.Value.TotalMilliseconds;
        return ms < 0 ? Timeout.Infinite : (int)Math.Min(ms, int.MaxValue);
    }
}
