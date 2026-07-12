using System.Diagnostics;

using OpenTelemetry;
using OpenTelemetry.Trace;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

[Collection("Langfuse activity")]
public sealed class LangfuseStandaloneCompositionTests
{
    [Fact]
    public void SessionContract_InheritsAbstractClientSurfaceWithoutRedeclaringFacadeMembers()
    {
        Assert.Contains(typeof(ILangfuseClient), typeof(ILangfuseSession).GetInterfaces());
        Assert.Empty(typeof(ILangfuseSession).GetProperties(
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.DeclaredOnly));
        Assert.Equal(
            ["Flush", "Shutdown"],
            typeof(ILangfuseSession)
                .GetMethods(
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.DeclaredOnly)
                .Select(method => method.Name)
                .OrderBy(name => name)
                .ToArray());
        Assert.All(
            typeof(ILangfuseClient)
                .GetMethods(
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.DeclaredOnly),
            method => Assert.True(
                method.IsAbstract,
                $"Expected {method.Name} to remain an abstract client contract member."));
    }

    [Fact]
    public void Session_ComposesSharedClientAndOwnsOnlyStandaloneLifecycleResources()
    {
        var options = new LangfuseOptions
        {
            PublicKey = "pk",
            SecretKey = "sk",
            Host = "https://lf.example",
            SamplingRatio = 0,
        };
        var handler = new TrackingHttpMessageHandler();
        var transport = new LangfuseHttpTransport(new HttpClient(handler, disposeHandler: true));
        var client = new LangfuseClient(transport, LangfuseEndpoints.Resolve(options), options);
        var exporter = new ControlledExporter<Activity>();
        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOffSampler())
            .AddSource(LangfuseActivitySource.Name)
            .AddProcessor(new SimpleActivityExportProcessor(exporter))
            .Build();
        var session = new LangfuseSession(
            tracerProvider,
            meterProvider: null,
            transport,
            client,
            options.ShutdownTimeout);

        Assert.IsAssignableFrom<ILangfuseClient>(session);
        Assert.Same(client.Scores, session.Scores);
        Assert.Same(client.Datasets, session.Datasets);
        Assert.Same(client.ScoreConfigs, session.ScoreConfigs);
        Assert.Same(client.Metrics, session.Metrics);
        Assert.Same(client.Models, session.Models);
        Assert.Same(client.Prompts, session.Prompts);

        using var scenario = session.BeginScenario("standalone-composition");

        Assert.NotNull(scenario.TraceId);
        Assert.Equal(0, handler.DisposeCalls);

        session.Dispose();

        Assert.Equal(1, exporter.ShutdownCalls);
        Assert.Equal(1, exporter.DisposeCalls);
        Assert.Equal(1, handler.DisposeCalls);
    }
}
