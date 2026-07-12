using System.Diagnostics;
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
    private readonly int _defaultShutdownTimeoutMilliseconds;
    private LangfuseShutdownOutcome? _shutdownOutcome;
    private int _shutdownState;

    private static readonly LangfuseShutdownOutcome ShutdownInProgressOutcome = new(
        isFinal: false,
        LangfuseProviderShutdownStatus.NotAttempted,
        LangfuseProviderShutdownStatus.NotAttempted);

    public LangfuseSession(
        TracerProvider tracerProvider,
        MeterProvider? meterProvider,
        HttpClient httpClient,
        LangfuseScoreRecorder recorder,
        LangfuseScoreFailureSink failureSink,
        LangfuseCommentRecorder commentRecorder,
        LangfuseApiClient apiClient,
        Action<string>? diagnostics,
        TimeSpan defaultShutdownTimeout)
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
        _defaultShutdownTimeoutMilliseconds = LangfuseTimeout.ToShutdownMilliseconds(defaultShutdownTimeout);

        Datasets = new LangfuseDatasetClient(apiClient);
        ScoreConfigs = new LangfuseScoreConfigClient(apiClient);
        Metrics = new LangfuseMetricsClient(apiClient);
        Models = new LangfuseModelClient(apiClient);
        Prompts = new LangfusePromptClient(apiClient);
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
    public ILangfuseMetricsClient Metrics { get; }

    /// <inheritdoc />
    public ILangfuseModelClient Models { get; }

    /// <inheritdoc />
    public ILangfusePromptClient Prompts { get; }

    /// <inheritdoc />
    public bool Flush(TimeSpan? timeout = null)
    {
        ThrowIfShutdownStarted();

        var timeoutMs = LangfuseTimeout.ToFlushMilliseconds(timeout);
        var traces = _tracerProvider.ForceFlush(timeoutMs);
        var metrics = _meterProvider?.ForceFlush(timeoutMs) ?? true;
        return traces && metrics;
    }

    /// <inheritdoc />
    public LangfuseShutdownOutcome Shutdown(TimeSpan timeout) =>
        Shutdown(LangfuseTimeout.ToShutdownMilliseconds(timeout));

    /// <inheritdoc />
    public ILangfuseScenario BeginScenario(
        string name,
        string? sessionId = null,
        string? userId = null,
        IEnumerable<string>? tags = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ThrowIfShutdownStarted();
        return new LangfuseScenario(_recorder, name, sessionId, userId, tags, metadata);
    }

    /// <inheritdoc />
    public ILangfuseExperimentRun BeginExperimentRun(string datasetName, string runName, string? runDescription = null)
    {
        ThrowIfShutdownStarted();
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
    public Task AddTraceCommentAsync(string traceId, string content, CancellationToken cancellationToken = default)
    {
        ThrowIfShutdownStarted();
        return _commentRecorder.AddTraceCommentAsync(traceId, content, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose() => _ = Shutdown(_defaultShutdownTimeoutMilliseconds);

    private LangfuseShutdownOutcome Shutdown(int timeoutMilliseconds)
    {
        if (Interlocked.CompareExchange(ref _shutdownState, 1, 0) != 0)
        {
            return Volatile.Read(ref _shutdownOutcome) ?? ShutdownInProgressOutcome;
        }

        LangfuseShutdownOutcome? outcome = null;
        try
        {
            var stopwatch = timeoutMilliseconds == Timeout.Infinite
                ? null
                : Stopwatch.StartNew();
            var tracesCompleted = _tracerProvider.Shutdown(timeoutMilliseconds);
            var traceStatus = tracesCompleted
                ? LangfuseProviderShutdownStatus.Completed
                : LangfuseProviderShutdownStatus.Incomplete;

            LangfuseProviderShutdownStatus metricStatus;
            if (_meterProvider is null)
            {
                metricStatus = LangfuseProviderShutdownStatus.NotConfigured;
            }
            else
            {
                var remainingTimeoutMilliseconds = GetRemainingTimeoutMilliseconds(
                    timeoutMilliseconds,
                    stopwatch);
                var metricsCompleted = _meterProvider.Shutdown(remainingTimeoutMilliseconds);
                metricStatus = metricsCompleted
                    ? LangfuseProviderShutdownStatus.Completed
                    : LangfuseProviderShutdownStatus.Incomplete;
            }

            outcome = new LangfuseShutdownOutcome(
                isFinal: true,
                traceStatus,
                metricStatus);
            return outcome;
        }
        finally
        {
            try
            {
                DisposeOwnedResources();
            }
            finally
            {
                outcome ??= new LangfuseShutdownOutcome(
                    isFinal: true,
                    LangfuseProviderShutdownStatus.Incomplete,
                    _meterProvider is null
                        ? LangfuseProviderShutdownStatus.NotConfigured
                        : LangfuseProviderShutdownStatus.Incomplete);
                Volatile.Write(ref _shutdownOutcome, outcome);
                Volatile.Write(ref _shutdownState, 2);
            }
        }
    }

    private static int GetRemainingTimeoutMilliseconds(
        int timeoutMilliseconds,
        Stopwatch? stopwatch)
    {
        if (timeoutMilliseconds == Timeout.Infinite)
        {
            return Timeout.Infinite;
        }

        var elapsedMilliseconds = stopwatch is null
            ? 0
            : (long)Math.Ceiling(stopwatch.Elapsed.TotalMilliseconds);
        return (int)Math.Max(timeoutMilliseconds - elapsedMilliseconds, 0);
    }

    private void DisposeOwnedResources()
    {
        try
        {
            _tracerProvider.Dispose();
        }
        finally
        {
            try
            {
                _meterProvider?.Dispose();
            }
            finally
            {
                _httpClient.Dispose();
            }
        }
    }

    private void ThrowIfShutdownStarted()
    {
        if (Volatile.Read(ref _shutdownState) != 0)
        {
            throw new ObjectDisposedException(nameof(LangfuseSession));
        }
    }
}
