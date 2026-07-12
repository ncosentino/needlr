using System.Diagnostics;

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseSessionShutdownTests
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public void Shutdown_EnabledSession_DrainsProvidersAndReleasesOwnedResourcesExactlyOnce()
    {
        var (session, traceExporter, metricExporter, handler) = CreateSession(
            TimeSpan.FromSeconds(5),
            includeMetrics: true);

        Assert.True(session.IsEnabled, "Expected the lifecycle test to exercise an active session.");

        var first = session.Shutdown(TimeSpan.FromSeconds(1));
        var second = session.Shutdown(TimeSpan.Zero);
        session.Dispose();

        Assert.True(first.IsFinal, "Expected the owner shutdown call to return a final outcome.");
        Assert.Equal(LangfuseProviderShutdownStatus.Completed, first.Traces);
        Assert.Equal(LangfuseProviderShutdownStatus.Completed, first.Metrics);
        Assert.Same(first, second);
        Assert.Equal(1, traceExporter.ShutdownCalls);
        Assert.Equal(1, metricExporter!.ShutdownCalls);
        Assert.Equal(1, traceExporter.DisposeCalls);
        Assert.Equal(1, metricExporter.DisposeCalls);
        Assert.Equal(1, handler.DisposeCalls);
    }

    [Fact]
    public void Shutdown_UsesOneDeadlineAcrossTraceAndMetricProviders()
    {
        var (session, traceExporter, metricExporter, _) = CreateSession(
            TimeSpan.FromSeconds(5),
            includeMetrics: true);
        traceExporter.BlockShutdown();

        var outcome = session.Shutdown(TimeSpan.FromMilliseconds(25));

        Assert.True(outcome.IsFinal, "Expected the shutdown timeout to produce a final outcome.");
        Assert.Equal(LangfuseProviderShutdownStatus.Incomplete, outcome.Traces);
        Assert.Equal(LangfuseProviderShutdownStatus.Incomplete, outcome.Metrics);
        Assert.Equal(25, traceExporter.LastShutdownTimeoutMilliseconds);
        Assert.Equal(0, metricExporter!.LastShutdownTimeoutMilliseconds);
    }

    [Fact]
    public void Shutdown_ReportsTraceAndMetricCompletionIndependently()
    {
        var (session, _, metricExporter, _) = CreateSession(
            TimeSpan.FromSeconds(5),
            includeMetrics: true);
        metricExporter!.BlockShutdown();

        var outcome = session.Shutdown(TimeSpan.FromMilliseconds(25));

        Assert.True(outcome.IsFinal, "Expected provider-specific statuses in the final outcome.");
        Assert.Equal(LangfuseProviderShutdownStatus.Completed, outcome.Traces);
        Assert.Equal(LangfuseProviderShutdownStatus.Incomplete, outcome.Metrics);
    }

    [Fact]
    public async Task Shutdown_ConcurrentCallerReturnsDeterministicallyWithoutRepeatingDrain()
    {
        var (session, traceExporter, metricExporter, _) = CreateSession(
            TimeSpan.FromSeconds(5),
            includeMetrics: true);
        traceExporter.BlockShutdown();

        var ownerTask = Task.Run(
            () => session.Shutdown(TimeSpan.FromSeconds(5)),
            _cancellationToken);
        traceExporter.WaitForShutdown(_cancellationToken);

        var concurrent = session.Shutdown(TimeSpan.FromSeconds(1));

        Assert.False(concurrent.IsFinal, "Expected a concurrent non-owner call to report shutdown in progress.");
        Assert.Equal(LangfuseProviderShutdownStatus.NotAttempted, concurrent.Traces);
        Assert.Equal(LangfuseProviderShutdownStatus.NotAttempted, concurrent.Metrics);

        traceExporter.ReleaseShutdown();
        var owner = await ownerTask.WaitAsync(_cancellationToken);
        var repeated = session.Shutdown(TimeSpan.Zero);

        Assert.True(owner.IsFinal, "Expected the owner call to publish the final shutdown outcome.");
        Assert.Same(owner, repeated);
        Assert.Equal(1, traceExporter.ShutdownCalls);
        Assert.Equal(1, metricExporter!.ShutdownCalls);
    }

    [Fact]
    public void Dispose_UsesConfiguredBoundedTimeoutAndReleasesResourcesAfterIncompleteDrain()
    {
        var (session, traceExporter, metricExporter, handler) = CreateSession(
            TimeSpan.FromMilliseconds(20),
            includeMetrics: true);
        traceExporter.BlockShutdown();

        session.Dispose();
        session.Dispose();

        Assert.Equal(20, traceExporter.LastShutdownTimeoutMilliseconds);
        Assert.Equal(0, metricExporter!.LastShutdownTimeoutMilliseconds);
        Assert.Equal(1, traceExporter.ShutdownCalls);
        Assert.Equal(1, metricExporter.ShutdownCalls);
        Assert.Equal(1, traceExporter.DisposeCalls);
        Assert.Equal(1, metricExporter.DisposeCalls);
        Assert.Equal(1, handler.DisposeCalls);
    }

    [Fact]
    public void Shutdown_AfterCompletionRejectsNewSessionOperations()
    {
        var (session, _, _, _) = CreateSession(
            TimeSpan.FromSeconds(5),
            includeMetrics: false);
        session.Shutdown(TimeSpan.FromSeconds(1));

        Assert.Throws<ObjectDisposedException>(() => session.Flush(TimeSpan.Zero));
        Assert.Throws<ObjectDisposedException>(() => session.BeginScenario("after-shutdown"));
        Assert.Throws<ObjectDisposedException>(() => session.BeginExperimentRun("dataset", "run"));
        Assert.Throws<ObjectDisposedException>(() =>
        {
            _ = session.AddTraceCommentAsync(
                "trace-id",
                "after-shutdown",
                _cancellationToken);
        });
    }

    [Fact]
    public void Shutdown_InfiniteWaitRequiresExplicitInfiniteTimeSpan()
    {
        var (session, traceExporter, _, _) = CreateSession(
            TimeSpan.FromSeconds(5),
            includeMetrics: false);

        var outcome = session.Shutdown(Timeout.InfiniteTimeSpan);

        Assert.True(outcome.IsFinal, "Expected the explicit infinite timeout call to complete.");
        Assert.Equal(Timeout.Infinite, traceExporter.LastShutdownTimeoutMilliseconds);
        Assert.Equal(LangfuseProviderShutdownStatus.Completed, outcome.Traces);
        Assert.Equal(LangfuseProviderShutdownStatus.NotConfigured, outcome.Metrics);
    }

    [Fact]
    public void Shutdown_InvalidNegativeTimeoutThrowsWithoutStartingShutdown()
    {
        var (session, traceExporter, _, _) = CreateSession(
            TimeSpan.FromSeconds(5),
            includeMetrics: false);

        Assert.Throws<ArgumentOutOfRangeException>(() => session.Shutdown(TimeSpan.FromMilliseconds(-2)));

        Assert.Equal(0, traceExporter.ShutdownCalls);

        var outcome = session.Shutdown(TimeSpan.Zero);
        Assert.True(outcome.IsFinal, "Expected a valid call after validation failure to own shutdown.");
    }

    [Fact]
    public void Start_EnabledSessionRejectsInvalidDefaultShutdownTimeout()
    {
        var options = new LangfuseOptions
        {
            PublicKey = "pk",
            SecretKey = "sk",
            Host = "https://lf.example",
            ShutdownTimeout = TimeSpan.FromMilliseconds(-2),
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => LangfuseTelemetry.Start(options));
    }

    [Fact]
    public void ShutdownOutcome_PublicConstructorPreservesExternalImplementationValues()
    {
        Assert.Single(typeof(LangfuseShutdownOutcome).GetConstructors());

        var outcome = new LangfuseShutdownOutcome(
            isFinal: true,
            LangfuseProviderShutdownStatus.Completed,
            LangfuseProviderShutdownStatus.NotConfigured);

        Assert.True(outcome.IsFinal, "Expected the supplied final-state value to be preserved.");
        Assert.Equal(LangfuseProviderShutdownStatus.Completed, outcome.Traces);
        Assert.Equal(LangfuseProviderShutdownStatus.NotConfigured, outcome.Metrics);
    }

    private static (
        LangfuseSession Session,
        ControlledExporter<Activity> TraceExporter,
        ControlledExporter<Metric>? MetricExporter,
        TrackingHttpMessageHandler Handler) CreateSession(
            TimeSpan defaultShutdownTimeout,
            bool includeMetrics)
    {
        var traceExporter = new ControlledExporter<Activity>();
        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("LangfuseSessionShutdownTests")
            .AddProcessor(new SimpleActivityExportProcessor(traceExporter))
            .Build();

        ControlledExporter<Metric>? metricExporter = null;
        MeterProvider? meterProvider = null;
        if (includeMetrics)
        {
            metricExporter = new ControlledExporter<Metric>();
            meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddReader(new BaseExportingMetricReader(metricExporter))
                .Build();
        }

        var handler = new TrackingHttpMessageHandler();
        var transport = new LangfuseHttpTransport(
            new HttpClient(handler, disposeHandler: true));
        var options = new LangfuseOptions
        {
            PublicKey = "pk",
            SecretKey = "sk",
            Host = "https://lf.example",
            ScoreFailureMode = LangfuseScoreFailureMode.Strict,
        };
        var client = new LangfuseClient(
            transport,
            LangfuseEndpoints.Resolve(options),
            options);
        var session = new LangfuseSession(
            tracerProvider,
            meterProvider,
            transport,
            client,
            defaultShutdownTimeout);

        return (session, traceExporter, metricExporter, handler);
    }
}
