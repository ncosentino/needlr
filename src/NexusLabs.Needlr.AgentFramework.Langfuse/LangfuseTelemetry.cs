using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Entry point for exporting Needlr agent telemetry to Langfuse without requiring a generic
/// host. Designed for evals, console apps, and test fixtures that build telemetry by hand.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Start(LangfuseOptions)"/> constructs standalone OpenTelemetry tracer and meter
/// providers that subscribe to Needlr's <c>gen_ai</c> activity source and meters and export them
/// to Langfuse over OTLP/HTTP. Dispose the returned <see cref="ILangfuseSession"/> to flush and
/// tear them down.
/// </para>
/// <para>
/// For ASP.NET Core / generic-host applications that already call <c>AddOpenTelemetry()</c>, use
/// <see cref="LangfuseServiceCollectionExtensions.AddNeedlrLangfuse(Microsoft.Extensions.DependencyInjection.IServiceCollection, Action{LangfuseOptions}?)"/>
/// instead so the providers participate in the host lifecycle.
/// </para>
/// <example>
/// <code>
/// using var langfuse = LangfuseTelemetry.Start(LangfuseOptions.FromEnvironment());
/// using (LangfuseTrace.BeginScenario("trip-planner: NYC -> Tokyo", sessionId: runId))
/// {
///     var run = await runner.RunAsync(...);
///     // ... evaluate and record scores ...
/// }
/// </code>
/// </example>
/// </remarks>
public static class LangfuseTelemetry
{
    /// <summary>
    /// Starts a Langfuse export session for the supplied options.
    /// </summary>
    /// <param name="options">
    /// The export configuration. When <see cref="LangfuseOptions.IsConfigured"/> is
    /// <see langword="false"/> (for example, missing credentials), a disabled no-op session is
    /// returned so callers never need to branch on configuration state.
    /// </param>
    /// <returns>
    /// An <see cref="ILangfuseSession"/> that exports telemetry until disposed.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public static ILangfuseSession Start(LangfuseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.IsConfigured)
        {
            LangfuseExportGuard.WarnIfCredentialsWithoutTarget(options);
            return new DisabledLangfuseSession();
        }

        var endpoints = LangfuseEndpoints.Resolve(options);
        var resource = BuildResource(options);

        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resource)
            .SetSampler(BuildSampler(options.SamplingRatio))
            .AddSource(LangfuseActivitySource.Name)
            .AddSource(options.AgentActivitySourceName)
            .AddSource([.. options.AdditionalActivitySources])
            .AddProcessor(new LangfuseTraceAttributeProcessor())
            .AddOtlpExporter(otlp => ConfigureOtlp(otlp, endpoints.TracesEndpoint, endpoints.Headers))
            .Build();

        MeterProvider? meterProvider = null;
        if (options.IncludeMetrics)
        {
            meterProvider = Sdk.CreateMeterProviderBuilder()
                .SetResourceBuilder(resource)
                .AddMeter(options.AgentMeterName)
                .AddMeter(options.GenAiMeterName)
                .AddMeter([.. options.AdditionalMeters])
                .AddOtlpExporter(otlp => ConfigureOtlp(otlp, endpoints.MetricsEndpoint, endpoints.Headers))
                .Build();
        }

        var httpClient = new HttpClient();
        var apiClient = new LangfuseScoreApiClient(
            httpClient,
            endpoints.ScoresEndpoint,
            endpoints.AuthorizationHeaderValue);

        var failureSink = new LangfuseScoreFailureSink(options.ScoreFailureMode, options.ScoreErrorCallback);
        var recorder = new LangfuseScoreRecorder(apiClient, failureSink, options.NormalizeScoreNames);

        return new LangfuseSession(tracerProvider, meterProvider, httpClient, recorder, failureSink);
    }

    private static ResourceBuilder BuildResource(LangfuseOptions options)
    {
        var builder = ResourceBuilder.CreateDefault();
        return string.IsNullOrWhiteSpace(options.ServiceVersion)
            ? builder.AddService(options.ServiceName)
            : builder.AddService(options.ServiceName, serviceVersion: options.ServiceVersion);
    }

    private static Sampler BuildSampler(double ratio)
    {
        if (ratio >= 1.0)
        {
            return new AlwaysOnSampler();
        }

        if (ratio <= 0.0)
        {
            return new AlwaysOffSampler();
        }

        return new TraceIdRatioBasedSampler(ratio);
    }

    private static void ConfigureOtlp(OtlpExporterOptions otlp, Uri endpoint, string headers)
    {
        otlp.Endpoint = endpoint;
        otlp.Protocol = OtlpExportProtocol.HttpProtobuf;
        otlp.Headers = headers;
    }
}
