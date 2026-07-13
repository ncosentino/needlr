using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Registers Langfuse OTLP export and a complete non-owning <see cref="ILangfuseClient"/> facade
/// on an <see cref="IServiceCollection"/> for dependency-injection-based applications.
/// </summary>
/// <remarks>
/// Unlike <see cref="LangfuseTelemetry.Start(LangfuseOptions)"/> — which builds standalone
/// providers for evals and console apps — this integrates with the host's
/// <c>AddOpenTelemetry()</c> pipeline so the tracer and meter providers share the application
/// lifecycle. The registered facade can create scenarios and experiment runs without constructing
/// a standalone <see cref="ILangfuseSession"/>. When options are not configured, the same facade
/// and specialized interfaces are registered as coherent no-ops.
/// </remarks>
public static class LangfuseServiceCollectionExtensions
{
    /// <summary>
    /// Gets the named <see cref="HttpClient"/> used by the built-in hosted Langfuse REST clients.
    /// </summary>
    /// <remarks>
    /// Register or configure this named client before calling <see cref="AddNeedlrLangfuse"/> to
    /// customize handlers, proxies, certificates, connection pooling, or other transport behavior.
    /// </remarks>
    public const string HttpClientName = "NexusLabs.Needlr.AgentFramework.Langfuse";

    /// <summary>
    /// Adds Langfuse OTLP/HTTP export to the host's OpenTelemetry pipeline and registers the
    /// non-owning <see cref="ILangfuseClient"/> facade plus all specialized client interfaces.
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
            var client = new DisabledLangfuseClient();
            RegisterDisabledClient(services, client);
            return services;
        }

        options.TraceExport.Validate();
        var endpoints = LangfuseEndpoints.Resolve(options);
        var existingClient = services.LastOrDefault(
            descriptor =>
                descriptor.ServiceType == typeof(ILangfuseClient)
                && descriptor.ServiceKey is null);
        var usesBuiltInClient = existingClient is null;
        if (usesBuiltInClient)
        {
            ValidateSpecializedClientLifetimes(services);
            services.AddHttpClient(HttpClientName);
            services.TryAddSingleton(sp => new LangfuseHttpTransport(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName)));
            services.TryAddSingleton(
                new LangfusePublicationHealth(isEnabled: true));
            services.TryAddSingleton<ILangfuseResourceLockProvider>(
                options.ResourceLockProvider);
            services.TryAddSingleton(sp => new LangfuseClientComposition(
                sp.GetRequiredService<LangfuseHttpTransport>(),
                endpoints,
                options,
                sp.GetRequiredService<ILangfuseResourceLockProvider>(),
                sp.GetRequiredService<LangfusePublicationHealth>()));
            RegisterSpecializedClients(services);
            services.TryAddSingleton(sp =>
            {
                _ = sp.GetRequiredService<TracerProvider>();
                return new LangfuseClient(
                    sp.GetRequiredService<LangfuseClientComposition>(),
                    sp.GetRequiredService<ILangfuseScoreClient>(),
                    sp.GetRequiredService<ILangfuseDatasetClient>(),
                    sp.GetRequiredService<ILangfuseScoreConfigClient>(),
                    sp.GetRequiredService<ILangfuseMetricsClient>(),
                    sp.GetRequiredService<ILangfuseModelClient>(),
                    sp.GetRequiredService<ILangfusePromptClient>());
            });
            services.TryAddSingleton<ILangfuseClient>(
                sp => sp.GetRequiredService<LangfuseClient>());
        }
        else
        {
            ValidateSingleton(existingClient!, typeof(ILangfuseClient));
            if (existingClient!.ImplementationInstance is not ILangfuseClient client)
            {
                throw new InvalidOperationException(
                    $"{nameof(ILangfuseClient)} overrides must be registered as singleton instances.");
            }

            services.RemoveAll<LangfusePublicationHealth>();
            services.AddSingleton(client.PublicationHealth);
            RegisterSpecializedClientsFromFacade(services, client);
        }

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
                .AddProcessor(new LangfuseTraceAttributeProcessor(options.Environment, options.Release)));
        services.ConfigureOpenTelemetryTracerProvider((serviceProvider, tracing) =>
        {
            if (usesBuiltInClient)
            {
                _ = serviceProvider.GetRequiredService<LangfuseHttpTransport>();
            }

            tracing.AddProcessor(LangfuseTraceExport.CreateProcessor(
                endpoints,
                options.TraceExport,
                serviceProvider.GetRequiredService<LangfusePublicationHealth>()));
        });

        if (options.IncludeMetrics)
        {
            otelBuilder.WithMetrics(metrics => metrics
                .AddMeter(options.AgentMeterName)
                .AddMeter(options.GenAiMeterName)
                .AddMeter([.. options.AdditionalMeters])
                .AddOtlpExporter(otlp => ConfigureOtlp(otlp, endpoints.MetricsEndpoint, endpoints.Headers)));
            if (usesBuiltInClient)
            {
                services.ConfigureOpenTelemetryMeterProvider((serviceProvider, metrics) =>
                {
                    _ = serviceProvider.GetRequiredService<LangfuseHttpTransport>();
                });
            }
        }

        return services;
    }

    private static void RegisterDisabledClient(
        IServiceCollection services,
        DisabledLangfuseClient client)
    {
        services.RemoveAll<ILangfuseClient>();
        services.RemoveAll<ILangfuseScoreClient>();
        services.RemoveAll<ILangfuseDatasetClient>();
        services.RemoveAll<ILangfuseScoreConfigClient>();
        services.RemoveAll<ILangfuseMetricsClient>();
        services.RemoveAll<ILangfuseModelClient>();
        services.RemoveAll<ILangfusePromptClient>();
        services.RemoveAll<LangfusePublicationHealth>();

        services.AddSingleton<ILangfuseClient>(client);
        services.AddSingleton(client.PublicationHealth);
        services.AddSingleton<ILangfuseScoreClient>(client.Scores);
        services.AddSingleton<ILangfuseDatasetClient>(client.Datasets);
        services.AddSingleton<ILangfuseScoreConfigClient>(client.ScoreConfigs);
        services.AddSingleton<ILangfuseMetricsClient>(client.Metrics);
        services.AddSingleton<ILangfuseModelClient>(client.Models);
        services.AddSingleton<ILangfusePromptClient>(client.Prompts);
    }

    private static void RegisterSpecializedClientsFromFacade(
        IServiceCollection services,
        ILangfuseClient client)
    {
        services.RemoveAll<ILangfuseScoreClient>();
        services.RemoveAll<ILangfuseDatasetClient>();
        services.RemoveAll<ILangfuseScoreConfigClient>();
        services.RemoveAll<ILangfuseMetricsClient>();
        services.RemoveAll<ILangfuseModelClient>();
        services.RemoveAll<ILangfusePromptClient>();

        services.AddSingleton(client.Scores);
        services.AddSingleton(client.Datasets);
        services.AddSingleton(client.ScoreConfigs);
        services.AddSingleton(client.Metrics);
        services.AddSingleton(client.Models);
        services.AddSingleton(client.Prompts);
    }

    private static void RegisterSpecializedClients(IServiceCollection services)
    {
        services.TryAddSingleton<ILangfuseScoreClient>(
            sp => sp.GetRequiredService<LangfuseClientComposition>().Scores);
        services.TryAddSingleton<ILangfuseDatasetClient>(
            sp => sp.GetRequiredService<LangfuseClientComposition>().Datasets);
        services.TryAddSingleton<ILangfuseScoreConfigClient>(
            sp => sp.GetRequiredService<LangfuseClientComposition>().ScoreConfigs);
        services.TryAddSingleton<ILangfuseMetricsClient>(
            sp => sp.GetRequiredService<LangfuseClientComposition>().Metrics);
        services.TryAddSingleton<ILangfuseModelClient>(
            sp => sp.GetRequiredService<LangfuseClientComposition>().Models);
        services.TryAddSingleton<ILangfusePromptClient>(
            sp => sp.GetRequiredService<LangfuseClientComposition>().Prompts);
    }

    private static void ValidateSpecializedClientLifetimes(IServiceCollection services)
    {
        ValidateSingletonOverride<ILangfuseScoreClient>(services);
        ValidateSingletonOverride<ILangfuseDatasetClient>(services);
        ValidateSingletonOverride<ILangfuseScoreConfigClient>(services);
        ValidateSingletonOverride<ILangfuseMetricsClient>(services);
        ValidateSingletonOverride<ILangfuseModelClient>(services);
        ValidateSingletonOverride<ILangfusePromptClient>(services);
    }

    private static void ValidateSingletonOverride<TService>(IServiceCollection services)
    {
        var descriptor = services.LastOrDefault(
            candidate =>
                candidate.ServiceType == typeof(TService)
                && candidate.ServiceKey is null);
        if (descriptor is not null)
        {
            ValidateSingleton(descriptor, typeof(TService));
        }
    }

    private static void ValidateSingleton(ServiceDescriptor descriptor, Type serviceType)
    {
        if (descriptor.Lifetime != ServiceLifetime.Singleton)
        {
            throw new InvalidOperationException(
                $"{serviceType.Name} must be registered as a singleton before AddNeedlrLangfuse.");
        }
    }

    private static void ConfigureOtlp(OtlpExporterOptions otlp, Uri endpoint, string headers)
    {
        otlp.Endpoint = endpoint;
        otlp.Protocol = OtlpExportProtocol.HttpProtobuf;
        otlp.Headers = headers;
    }
}
