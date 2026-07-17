using System.Diagnostics;
using System.Net;

using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry;
using OpenTelemetry.Trace;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

[Collection("Langfuse activity")]
public sealed class LangfuseClientFacadeTests
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public void AddNeedlrLangfuse_Configured_RegistersCompleteNonOwningFacadeWithExactSpecializedIdentities()
    {
        var handler = new TrackingHttpMessageHandler();
        var services = CreateConfiguredServices(handler);

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ILangfuseClient>();
        var tracerProviders = provider.GetServices<TracerProvider>().ToArray();

        Assert.Single(tracerProviders);
        Assert.True(client.IsEnabled, "Expected configured Langfuse registration to enable the facade.");
        Assert.True(client.PublicationHealth.GetSnapshot().IsEnabled, "Expected configured publication health.");
        Assert.Equal(0, client.PublicationHealth.GetSnapshot().ScoreUploads.Failed);
        Assert.Null(provider.GetService<ILangfuseSession>());
        Assert.False(client is IDisposable, "Expected the hosted facade to expose no telemetry lifecycle ownership.");
        Assert.Same(client.Scores, provider.GetRequiredService<ILangfuseScoreClient>());
        Assert.Same(client.Datasets, provider.GetRequiredService<ILangfuseDatasetClient>());
        Assert.Same(client.ScoreConfigs, provider.GetRequiredService<ILangfuseScoreConfigClient>());
        Assert.Same(client.Metrics, provider.GetRequiredService<ILangfuseMetricsClient>());
        Assert.Same(client.Models, provider.GetRequiredService<ILangfuseModelClient>());
        Assert.Same(client.Prompts, provider.GetRequiredService<ILangfusePromptClient>());

        using var scenario = client.BeginScenario("hosted-scenario", sessionId: "session-1");

        Assert.NotNull(scenario.Activity);
        Assert.NotNull(scenario.TraceId);
        Assert.Equal(scenario.Activity!.TraceId.ToHexString(), scenario.TraceId);
    }

    [Fact]
    public void AddNeedlrLangfuse_Configured_PreservesHostSampler()
    {
        var services = new ServiceCollection();
        services
            .AddOpenTelemetry()
            .WithTracing(tracing => tracing.SetSampler(new AlwaysOffSampler()));
        services.AddNeedlrLangfuse(options =>
        {
            options.PublicKey = "pk";
            options.SecretKey = "sk";
            options.Host = "http://127.0.0.1:1";
            options.SamplingRatio = 1;
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ILangfuseClient>();

        using var scenario = client.BeginScenario("host-sampler");

        Assert.NotNull(scenario.Activity);
        Assert.Equal(ActivityTraceFlags.None, scenario.Activity.ActivityTraceFlags);
        Assert.Single(provider.GetServices<TracerProvider>());
    }

    [Fact]
    public async Task AddNeedlrLangfuse_Configured_ComposesFacadeFromPreRegisteredSpecializedClients()
    {
        using var scoreHttp = LangfuseHttpStub.Create(
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError),
            []);
        var scores = CreateScoreClient(scoreHttp);
        await scores.RecordScoreAsync(
            "trace",
            "failure",
            1.0,
            cancellationToken: _cancellationToken);
        var datasets = new DisabledLangfuseDatasetClient();
        var scoreConfigs = new DisabledLangfuseScoreConfigClient();
        var metrics = new DisabledLangfuseMetricsClient();
        var models = new DisabledLangfuseModelClient();
        var prompts = new DisabledLangfusePromptClient();
        var services = new ServiceCollection();
        services.AddSingleton<ILangfuseScoreClient>(scores);
        services.AddSingleton<ILangfuseDatasetClient>(datasets);
        services.AddSingleton<ILangfuseScoreConfigClient>(scoreConfigs);
        services.AddSingleton<ILangfuseMetricsClient>(metrics);
        services.AddSingleton<ILangfuseModelClient>(models);
        services.AddSingleton<ILangfusePromptClient>(prompts);
        services.AddNeedlrLangfuse(options =>
        {
            options.PublicKey = "pk";
            options.SecretKey = "sk";
            options.Host = "http://127.0.0.1:1";
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ILangfuseClient>();
        using var scenario = client.BeginScenario(
            "custom-score-client",
            sessionId: "custom-session");
        await scenario.RecordScoreAsync(
            "scenario-failure",
            1.0,
            cancellationToken: _cancellationToken);
        await scenario.RecordSessionScoreAsync(
            "session-failure",
            1.0,
            cancellationToken: _cancellationToken);

        Assert.Same(scores, client.Scores);
        Assert.Equal(3, scores.PublicationHealth.GetSnapshot().ScoreUploads.Failed);
        Assert.Equal(0, client.PublicationHealth.GetSnapshot().ScoreUploads.Failed);
        Assert.Same(datasets, client.Datasets);
        Assert.Same(scoreConfigs, client.ScoreConfigs);
        Assert.Same(metrics, client.Metrics);
        Assert.Same(models, client.Models);
        Assert.Same(prompts, client.Prompts);
    }

    [Fact]
    public void AddNeedlrLangfuse_Configured_PreRegisteredFacadeIsAuthoritativeForSpecializedClients()
    {
        var customFacade = new DisabledLangfuseClient();
        var services = new ServiceCollection();
        services.AddSingleton<ILangfuseClient>(customFacade);
        services.AddNeedlrLangfuse(options =>
        {
            options.PublicKey = "pk";
            options.SecretKey = "sk";
            options.Host = "http://127.0.0.1:1";
        });

        Assert.Same(
            customFacade.Scores,
            services.Last(descriptor =>
                descriptor.ServiceType == typeof(ILangfuseScoreClient)
                && descriptor.ServiceKey is null).ImplementationInstance);
        Assert.Same(
            customFacade.Datasets,
            services.Last(descriptor =>
                descriptor.ServiceType == typeof(ILangfuseDatasetClient)
                && descriptor.ServiceKey is null).ImplementationInstance);

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ILangfuseClient>();

        Assert.Same(customFacade, client);
        Assert.Same(client.Scores, provider.GetRequiredService<ILangfuseScoreClient>());
        Assert.Same(client.Datasets, provider.GetRequiredService<ILangfuseDatasetClient>());
        Assert.Same(client.ScoreConfigs, provider.GetRequiredService<ILangfuseScoreConfigClient>());
        Assert.Same(client.Metrics, provider.GetRequiredService<ILangfuseMetricsClient>());
        Assert.Same(client.Models, provider.GetRequiredService<ILangfuseModelClient>());
        Assert.Same(client.Prompts, provider.GetRequiredService<ILangfusePromptClient>());
    }

    [Fact]
    public void AddNeedlrLangfuse_Configured_PreRegisteredFacadeFactoryIsRejected()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILangfuseClient>(_ => new DisabledLangfuseClient());

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddNeedlrLangfuse(options =>
            {
                options.PublicKey = "pk";
                options.SecretKey = "sk";
                options.Host = "http://127.0.0.1:1";
            }));

        Assert.Contains(nameof(ILangfuseClient), exception.Message, StringComparison.Ordinal);
        Assert.Contains("instance", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddNeedlrLangfuse_Configured_KeyedFacadeDoesNotReplaceUnkeyedBuiltInFacade()
    {
        var keyedFacade = new DisabledLangfuseClient();
        var services = new ServiceCollection();
        services.AddKeyedSingleton<ILangfuseClient>("custom", keyedFacade);
        services.AddNeedlrLangfuse(options =>
        {
            options.PublicKey = "pk";
            options.SecretKey = "sk";
            options.Host = "http://127.0.0.1:1";
            options.SamplingRatio = 0;
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ILangfuseClient>();

        Assert.True(client.IsEnabled, "Expected a keyed facade not to suppress the default facade.");
        Assert.Same(keyedFacade, provider.GetRequiredKeyedService<ILangfuseClient>("custom"));
        Assert.Same(client.Scores, provider.GetRequiredService<ILangfuseScoreClient>());
    }

    [Fact]
    public void AddNeedlrLangfuse_Configured_KeyedScopedSpecializedClientDoesNotAffectDefaultFacade()
    {
        var services = new ServiceCollection();
        services.AddKeyedScoped<ILangfuseScoreClient, DisabledLangfuseScoreClient>("custom");

        services.AddNeedlrLangfuse(options =>
        {
            options.PublicKey = "pk";
            options.SecretKey = "sk";
            options.Host = "http://127.0.0.1:1";
            options.SamplingRatio = 0;
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var client = provider.GetRequiredService<ILangfuseClient>();

        Assert.True(client.Scores.IsEnabled, "Expected the unkeyed specialized client to remain enabled.");
        Assert.False(
            scope.ServiceProvider.GetRequiredKeyedService<ILangfuseScoreClient>("custom").IsEnabled,
            "Expected the keyed specialized registration to remain available independently.");
    }

    [Theory]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void AddNeedlrLangfuse_Configured_NonSingletonSpecializedOverrideIsRejected(
        ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();
        if (lifetime == ServiceLifetime.Scoped)
        {
            services.AddScoped<ILangfuseScoreClient, DisabledLangfuseScoreClient>();
        }
        else
        {
            services.AddTransient<ILangfuseScoreClient, DisabledLangfuseScoreClient>();
        }

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddNeedlrLangfuse(options =>
            {
                options.PublicKey = "pk";
                options.SecretKey = "sk";
                options.Host = "http://127.0.0.1:1";
            }));

        Assert.Contains(nameof(ILangfuseScoreClient), exception.Message, StringComparison.Ordinal);
        Assert.Contains("singleton", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddNeedlrLangfuse_Configured_FacadeBeginsAndLinksExperimentRun()
    {
        var handler = new TrackingHttpMessageHandler(request =>
            request.Uri.AbsolutePath.EndsWith("/dataset-run-items", StringComparison.Ordinal)
                ? LangfuseDatasetRunItemHttpStub.CreateResponse(
                    request,
                    "dataset-run-item-7",
                    "dataset-run-42")
                : new HttpResponseMessage(HttpStatusCode.OK));
        var services = CreateConfiguredServices(handler);

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ILangfuseClient>();
        var run = client.BeginExperimentRun(
            "dataset-a",
            "run-42",
            new LangfuseExperimentRunOptions
            {
                Description = "facade run",
            });

        var result = await run.RunItemAsync(
            "item-7",
            (scenario, _) => Task.FromResult(scenario.TraceId),
            new LangfuseExperimentItemOptions
            {
                ScenarioName = "facade-item",
            },
            cancellationToken: _cancellationToken);

        var request = Assert.Single(handler.CapturedRequests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/public/dataset-run-items", request.Uri.AbsolutePath);
        Assert.Contains("\"runName\":\"run-42\"", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"datasetItemId\":\"item-7\"", request.Body, StringComparison.Ordinal);
        Assert.Contains($"\"traceId\":\"{result.TraceId}\"", request.Body, StringComparison.Ordinal);
        Assert.Equal(result.TraceId, result.Value);
        Assert.Equal(LangfuseExperimentItemLinkStatus.Linked, result.Link.Status);
        Assert.Equal("dataset-run-42", result.Link.DatasetRunId);
    }

    [Fact]
    public async Task AddNeedlrLangfuse_Disabled_RegistersCoherentNoOpFacadeWithExactSpecializedIdentities()
    {
        var services = new ServiceCollection();
        services.AddNeedlrLangfuse(options => options.Enabled = false);

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ILangfuseClient>();

        Assert.Empty(provider.GetServices<TracerProvider>());
        Assert.False(client.IsEnabled, "Expected disabled Langfuse registration to provide an inert facade.");
        Assert.False(client.PublicationHealth.GetSnapshot().IsEnabled, "Expected disabled publication health.");
        Assert.Equal(LangfuseDrainStatus.Disabled, client.PublicationHealth.GetSnapshot().Drain.Status);
        Assert.Null(provider.GetService<ILangfuseSession>());
        Assert.False(client is IDisposable, "Expected the disabled facade to remain non-owning.");
        Assert.Same(client.Scores, provider.GetRequiredService<ILangfuseScoreClient>());
        Assert.Same(client.Datasets, provider.GetRequiredService<ILangfuseDatasetClient>());
        Assert.Same(client.ScoreConfigs, provider.GetRequiredService<ILangfuseScoreConfigClient>());
        Assert.Same(client.Metrics, provider.GetRequiredService<ILangfuseMetricsClient>());
        Assert.Same(client.Models, provider.GetRequiredService<ILangfuseModelClient>());
        Assert.Same(client.Prompts, provider.GetRequiredService<ILangfusePromptClient>());

        using var scenario = client.BeginScenario("disabled");
        var run = client.BeginExperimentRun("dataset", "run");
        var item = await run.RunItemAsync(
            "item",
            (_, _) => Task.FromResult("disabled"),
            options: null,
            cancellationToken: _cancellationToken);
        await client.Scores.RecordScoreAsync(
            "trace",
            "score",
            1.0,
            cancellationToken: _cancellationToken);
        await client.AddTraceCommentAsync("trace", "comment", _cancellationToken);

        Assert.Null(scenario.TraceId);
        Assert.Null(item.TraceId);
        Assert.Equal(LangfuseExperimentItemLinkStatus.Disabled, item.Link.Status);
        Assert.Equal("disabled", item.Value);
        Assert.Empty(provider.GetServices<TracerProvider>());
    }

    [Fact]
    public void AddNeedlrLangfuse_Disabled_ReplacesPreRegisteredSpecializedClientWithCoherentNoOp()
    {
        using var scoreHttp = LangfuseHttpStub.Create(
            _ => new HttpResponseMessage(HttpStatusCode.OK),
            []);
        var preRegisteredScores = CreateScoreClient(scoreHttp);
        var preRegisteredFacade = new DisabledLangfuseClient();
        var keyedScores = new DisabledLangfuseScoreClient();
        var services = new ServiceCollection();
        services.AddSingleton<ILangfuseClient>(preRegisteredFacade);
        services.AddSingleton<ILangfuseScoreClient>(preRegisteredScores);
        services.AddKeyedSingleton<ILangfuseScoreClient>("custom", keyedScores);
        services.AddNeedlrLangfuse(options => options.Enabled = false);

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ILangfuseClient>();
        var scores = provider.GetRequiredService<ILangfuseScoreClient>();

        Assert.Same(client.Scores, scores);
        Assert.NotSame(preRegisteredFacade, client);
        Assert.NotSame(preRegisteredScores, scores);
        Assert.False(scores.IsEnabled, "Expected disabled registration to replace specialized clients with no-ops.");
        Assert.Same(
            keyedScores,
            provider.GetRequiredKeyedService<ILangfuseScoreClient>("custom"));
    }

    [Fact]
    public void AddNeedlrLangfuse_ProviderDisposal_ShutsDownTracerBeforeDiOwnedHttpTransport()
    {
        var handler = new TrackingHttpMessageHandler();
        var exporter = new ControlledExporter<Activity>(() => handler.DisposeCalls);
        var services = new ServiceCollection();
        services
            .AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource("LangfuseClientFacadeTests.Disposal")
                .AddProcessor(new SimpleActivityExportProcessor(exporter)));
        services.AddSingleton(_ => new LangfuseHttpTransport(
            new HttpClient(handler, disposeHandler: true)));
        services.AddNeedlrLangfuse(options =>
        {
            options.PublicKey = "pk";
            options.SecretKey = "sk";
            options.Host = "http://127.0.0.1:1";
            options.SamplingRatio = 0;
        });

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ILangfuseClient>();
        _ = provider.GetRequiredService<TracerProvider>();

        Assert.False(client is IDisposable, "Expected callers to have no facade disposal path.");
        Assert.Equal(0, handler.DisposeCalls);

        provider.Dispose();

        Assert.Equal(0, exporter.DependencyDisposeCallsAtDispose);
        Assert.Equal(1, handler.DisposeCalls);
    }

    private static ServiceCollection CreateConfiguredServices(TrackingHttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_ => new LangfuseHttpTransport(
            new HttpClient(handler, disposeHandler: true)));
        services.AddNeedlrLangfuse(options =>
        {
            options.PublicKey = "pk";
            options.SecretKey = "sk";
            options.Host = "http://127.0.0.1:1";
            options.SamplingRatio = 0;
        });
        return services;
    }

    private static LangfuseScoreClient CreateScoreClient(HttpClient httpClient)
    {
        var failureSink = new LangfuseScoreFailureSink(
            LangfuseScoreFailureMode.NonFatal,
            callback: null);
        var apiClient = LangfuseTestFactory.CreateScoreApiClient(
            httpClient,
            authorizationHeaderValue: "Basic dGVzdA==");
        var recorder = new LangfuseScoreRecorder(
            apiClient,
            failureSink,
            normalizeNames: false);
        return new LangfuseScoreClient(recorder, failureSink);
    }
}
