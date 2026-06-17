using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Registers Langfuse OTLP export on an <see cref="IServiceCollection"/> for ASP.NET Core and
/// generic-host applications.
/// </summary>
/// <remarks>
/// Unlike <see cref="LangfuseTelemetry.Start(LangfuseOptions)"/> — which builds standalone
/// providers for evals and console apps — this integrates with the host's
/// <c>AddOpenTelemetry()</c> pipeline so the tracer and meter providers share the application
/// lifecycle. When the supplied options are not configured (for example, missing credentials),
/// registration is skipped and the application starts normally without exporting.
/// </remarks>
public static class LangfuseServiceCollectionExtensions
{
    /// <summary>
    /// Adds Langfuse OTLP/HTTP export of Needlr agent telemetry to the host's OpenTelemetry
    /// pipeline.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">
    /// Optional callback to customise the <see cref="LangfuseOptions"/>. When <see langword="null"/>,
    /// options are read from the environment via <see cref="LangfuseOptions.FromEnvironment"/>.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddNeedlrLangfuse(
        this IServiceCollection services,
        Action<LangfuseOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = LangfuseOptions.FromEnvironment();
        configure?.Invoke(options);

        if (!options.IsConfigured)
        {
            LangfuseExportGuard.WarnIfCredentialsWithoutTarget(options);
            services.TryAddSingleton<ILangfuseScoreClient>(new DisabledLangfuseScoreClient());
            return services;
        }

        var endpoints = LangfuseEndpoints.Resolve(options);

        var otelBuilder = services
            .AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                if (string.IsNullOrWhiteSpace(options.ServiceVersion))
                {
                    resource.AddService(options.ServiceName);
                }
                else
                {
                    resource.AddService(options.ServiceName, serviceVersion: options.ServiceVersion);
                }
            })
            .WithTracing(tracing => tracing
                .AddSource(LangfuseActivitySource.Name)
                .AddSource(options.AgentActivitySourceName)
                .AddSource([.. options.AdditionalActivitySources])
                .AddProcessor(new LangfuseTraceAttributeProcessor())
                .AddOtlpExporter(otlp => ConfigureOtlp(otlp, endpoints.TracesEndpoint, endpoints.Headers)));

        if (options.IncludeMetrics)
        {
            otelBuilder.WithMetrics(metrics => metrics
                .AddMeter(options.AgentMeterName)
                .AddMeter(options.GenAiMeterName)
                .AddMeter([.. options.AdditionalMeters])
                .AddOtlpExporter(otlp => ConfigureOtlp(otlp, endpoints.MetricsEndpoint, endpoints.Headers)));
        }

        var httpClient = new HttpClient();
        var apiClient = new LangfuseScoreApiClient(
            httpClient,
            endpoints.ScoresEndpoint,
            endpoints.AuthorizationHeaderValue);
        var failureSink = new LangfuseScoreFailureSink(options.ScoreFailureMode, options.ScoreErrorCallback);
        var recorder = new LangfuseScoreRecorder(apiClient, failureSink, options.NormalizeScoreNames);

        services.TryAddSingleton<ILangfuseScoreClient>(new LangfuseScoreClient(recorder, failureSink));

        return services;
    }

    private static void ConfigureOtlp(OtlpExporterOptions otlp, Uri endpoint, string headers)
    {
        otlp.Endpoint = endpoint;
        otlp.Protocol = OtlpExportProtocol.HttpProtobuf;
        otlp.Headers = headers;
    }
}
