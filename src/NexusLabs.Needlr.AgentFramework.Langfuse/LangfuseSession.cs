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
[DoNotAutoRegister]
internal sealed class LangfuseSession :
    ILangfuseSession,
    ILangfuseExperimentItemScopeProviderFactory,
    ILangfuseExperimentResultSinkFactory
{
    private readonly TracerProvider _tracerProvider;
    private readonly MeterProvider? _meterProvider;
    private readonly ILangfuseClient _client;
    private readonly LangfuseHttpTransport _transport;
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
        LangfuseHttpTransport transport,
        ILangfuseClient client,
        TimeSpan defaultShutdownTimeout)
    {
        ArgumentNullException.ThrowIfNull(tracerProvider);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(client);

        _tracerProvider = tracerProvider;
        _meterProvider = meterProvider;
        _transport = transport;
        _client = client;
        _defaultShutdownTimeoutMilliseconds = LangfuseTimeout.ToShutdownMilliseconds(defaultShutdownTimeout);
    }

    /// <inheritdoc />
    public bool IsEnabled => _client.IsEnabled;

    /// <inheritdoc />
    public LangfusePublicationHealth PublicationHealth => _client.PublicationHealth;

    /// <inheritdoc />
    public ILangfuseScoreClient Scores => _client.Scores;

    /// <inheritdoc />
    public ILangfuseDatasetClient Datasets => _client.Datasets;

    /// <inheritdoc />
    public ILangfuseScoreConfigClient ScoreConfigs => _client.ScoreConfigs;

    /// <inheritdoc />
    public ILangfuseMetricsClient Metrics => _client.Metrics;

    /// <inheritdoc />
    public ILangfuseModelClient Models => _client.Models;

    /// <inheritdoc />
    public ILangfusePromptClient Prompts => _client.Prompts;

    /// <inheritdoc />
    public bool Flush(TimeSpan? timeout = null)
    {
        ThrowIfShutdownStarted();

        var timeoutMs = LangfuseTimeout.ToFlushMilliseconds(timeout);
        PublicationHealth.BeginDrain();
        var stopwatch = Stopwatch.StartNew();
        var completed = false;
        try
        {
            var traces = _tracerProvider.ForceFlush(timeoutMs);
            var metrics = _meterProvider?.ForceFlush(timeoutMs) ?? true;
            completed = traces && metrics;
            return completed;
        }
        finally
        {
            stopwatch.Stop();
            PublicationHealth.CompleteDrain(completed, stopwatch.Elapsed);
        }
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
        return _client.BeginScenario(name, sessionId, userId, tags, metadata);
    }

    /// <inheritdoc />
    public ILangfuseExperimentRun BeginExperimentRun(
        string datasetName,
        string runName) =>
        BeginExperimentRun(datasetName, runName, options: null);

    /// <inheritdoc />
    public ILangfuseExperimentRun BeginExperimentRun(
        string datasetName,
        string runName,
        LangfuseExperimentRunOptions? options)
    {
        ThrowIfShutdownStarted();
        return _client.BeginExperimentRun(datasetName, runName, options);
    }

    /// <inheritdoc />
    LangfuseExperimentItemScopeProvider<TCase, TOutput>
        ILangfuseExperimentItemScopeProviderFactory.CreateExperimentItemScopeProvider<TCase, TOutput>(
            ILangfuseExperimentRun run,
            LangfuseExperimentItemScopeOptions<TCase>? options)
    {
        ThrowIfShutdownStarted();
        return GetScopeProviderFactory()
            .CreateExperimentItemScopeProvider<TCase, TOutput>(run, options);
    }

    /// <inheritdoc />
    LangfuseExperimentItemScopeProvider<TCase, TOutput>
        ILangfuseExperimentItemScopeProviderFactory.CreateLocalExperimentItemScopeProvider<TCase, TOutput>(
            LangfuseExperimentItemScopeOptions<TCase>? options)
    {
        ThrowIfShutdownStarted();
        return GetScopeProviderFactory()
            .CreateLocalExperimentItemScopeProvider<TCase, TOutput>(options);
    }

    LangfuseExperimentResultSink<TCase, TOutput>
        ILangfuseExperimentResultSinkFactory.CreateExperimentResultSink<TCase, TOutput>(
            ILangfuseExperimentRun run,
            LangfuseExperimentResultSinkOptions<TCase, TOutput>? options)
    {
        ThrowIfShutdownStarted();
        return GetResultSinkFactory()
            .CreateExperimentResultSink<TCase, TOutput>(run, options);
    }

    LangfuseExperimentResultSink<TCase, TOutput>
        ILangfuseExperimentResultSinkFactory.CreateLocalExperimentResultSink<TCase, TOutput>(
            LangfuseExperimentResultSinkOptions<TCase, TOutput>? options)
    {
        ThrowIfShutdownStarted();
        return GetResultSinkFactory()
            .CreateLocalExperimentResultSink<TCase, TOutput>(options);
    }

    /// <inheritdoc />
    public Task AddTraceCommentAsync(string traceId, string content, CancellationToken cancellationToken = default)
    {
        ThrowIfShutdownStarted();
        return _client.AddTraceCommentAsync(traceId, content, cancellationToken);
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
        var drainCompleted = false;
        PublicationHealth.BeginDrain();
        var drainStopwatch = Stopwatch.StartNew();
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
            drainCompleted =
                traceStatus is LangfuseProviderShutdownStatus.Completed
                && metricStatus is
                    LangfuseProviderShutdownStatus.Completed
                    or LangfuseProviderShutdownStatus.NotConfigured;
            return outcome;
        }
        finally
        {
            drainStopwatch.Stop();
            PublicationHealth.CompleteDrain(drainCompleted, drainStopwatch.Elapsed);
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

    private ILangfuseExperimentItemScopeProviderFactory GetScopeProviderFactory() =>
        _client.ResolveExperimentFactory<ILangfuseExperimentItemScopeProviderFactory>(
            "The configured Langfuse client does not expose the built-in experiment trial lifecycle.");

    private ILangfuseExperimentResultSinkFactory GetResultSinkFactory() =>
        _client.ResolveExperimentFactory<ILangfuseExperimentResultSinkFactory>(
            "The configured Langfuse client does not expose the built-in experiment result-sink capability.");

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
                _transport.Dispose();
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
