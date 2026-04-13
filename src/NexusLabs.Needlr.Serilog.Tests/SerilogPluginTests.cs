using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Serilog;
using Serilog.Core;
using Serilog.Events;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.Serilog.Tests;

/// <summary>
/// Marker type so the Needlr source generator emits a TypeRegistry and
/// [ModuleInitializer] for this test assembly, enabling source-gen plugin
/// discovery of SerilogPlugin.
/// </summary>
internal sealed class TestAssemblyMarker;

/// <summary>
/// Tests for <see cref="SerilogPlugin"/> covering both reflection and source-gen
/// discovery paths, configuration override scenarios, and parity between modes.
/// </summary>
public sealed class SerilogPluginTests
{
    private static readonly IConfiguration SerilogConfig = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Serilog:Using:0"] = "Serilog.Sinks.Console",
            ["Serilog:MinimumLevel"] = "Information",
            ["Serilog:WriteTo:0:Name"] = "Console",
        })
        .Build();

    private static readonly IConfiguration EmptyConfig = new ConfigurationBuilder().Build();

    // -------------------------------------------------------------------------
    // Reflection path: plugin auto-discovery
    // -------------------------------------------------------------------------

    [Fact]
    public void Reflection_ILoggerFactory_IsRegistered()
    {
        var sp = BuildReflectionProvider(SerilogConfig);

        Assert.NotNull(sp.GetService<ILoggerFactory>());
    }

    [Fact]
    public void Reflection_ILoggerT_IsResolvable()
    {
        var sp = BuildReflectionProvider(SerilogConfig);

        var logger = sp.GetService<ILogger<SerilogPluginTests>>();

        Assert.NotNull(logger);
    }

    [Fact]
    public void Reflection_LoggerWritesToSerilogPipeline()
    {
        var sink = new CapturingSink();
        var sp = BuildReflectionProviderWithSink(EmptyConfig, sink);

        var logger = sp.GetRequiredService<ILogger<SerilogPluginTests>>();
        logger.LogInformation("test message from reflection");

        Assert.NotEmpty(sink.Events);
        Assert.Contains(sink.Events, e => e.RenderMessage().Contains("test message from reflection"));
    }

    // -------------------------------------------------------------------------
    // Source-gen path: plugin auto-discovery via [ModuleInitializer]
    // -------------------------------------------------------------------------

    [Fact]
    public void SourceGen_ILoggerFactory_IsRegistered()
    {
        var sp = BuildSourceGenProvider(SerilogConfig);

        Assert.NotNull(sp.GetService<ILoggerFactory>());
    }

    [Fact]
    public void SourceGen_ILoggerT_IsResolvable()
    {
        var sp = BuildSourceGenProvider(SerilogConfig);

        var logger = sp.GetService<ILogger<SerilogPluginTests>>();

        Assert.NotNull(logger);
    }

    [Fact]
    public void SourceGen_LoggerWritesToSerilogPipeline()
    {
        var sink = new CapturingSink();
        var sp = BuildSourceGenProviderWithSink(EmptyConfig, sink);

        var logger = sp.GetRequiredService<ILogger<SerilogPluginTests>>();
        logger.LogInformation("test message from source-gen");

        Assert.NotEmpty(sink.Events);
        Assert.Contains(sink.Events, e => e.RenderMessage().Contains("test message from source-gen"));
    }

    // -------------------------------------------------------------------------
    // Parity: both paths produce the same observable behavior
    // -------------------------------------------------------------------------

    [Fact]
    public void Parity_BothPaths_ResolveILoggerFactory()
    {
        var reflectionSp = BuildReflectionProvider(SerilogConfig);
        var sourceGenSp = BuildSourceGenProvider(SerilogConfig);

        var reflectionFactory = reflectionSp.GetService<ILoggerFactory>();
        var sourceGenFactory = sourceGenSp.GetService<ILoggerFactory>();

        Assert.NotNull(reflectionFactory);
        Assert.NotNull(sourceGenFactory);
    }

    [Fact]
    public void Parity_BothPaths_RouteLogsToConfiguredSinks()
    {
        var reflectionSink = new CapturingSink();
        var sourceGenSink = new CapturingSink();

        var reflectionSp = BuildReflectionProviderWithSink(EmptyConfig, reflectionSink);
        var sourceGenSp = BuildSourceGenProviderWithSink(EmptyConfig, sourceGenSink);

        reflectionSp.GetRequiredService<ILogger<SerilogPluginTests>>()
            .LogWarning("parity check");
        sourceGenSp.GetRequiredService<ILogger<SerilogPluginTests>>()
            .LogWarning("parity check");

        Assert.Single(reflectionSink.Events);
        Assert.Single(sourceGenSink.Events);
        Assert.Equal(
            reflectionSink.Events[0].RenderMessage(),
            sourceGenSink.Events[0].RenderMessage());
    }

    // -------------------------------------------------------------------------
    // Override: plugin provides the open generic ILogger<T>, consumer overrides
    // the Serilog configuration after the plugin runs
    // -------------------------------------------------------------------------

    [Fact]
    public void Reflection_Override_ConsumerCanReconfigureSerilogAfterPlugin()
    {
        var overrideSink = new CapturingSink();
        var sp = BuildReflectionProviderWithSink(EmptyConfig, overrideSink);

        var logger = sp.GetRequiredService<ILogger<SerilogPluginTests>>();
        logger.LogInformation("override message");

        Assert.NotEmpty(overrideSink.Events);
        Assert.Contains(overrideSink.Events, e => e.RenderMessage().Contains("override message"));
    }

    [Fact]
    public void SourceGen_Override_ConsumerCanReconfigureSerilogAfterPlugin()
    {
        var overrideSink = new CapturingSink();
        var sp = BuildSourceGenProviderWithSink(EmptyConfig, overrideSink);

        var logger = sp.GetRequiredService<ILogger<SerilogPluginTests>>();
        logger.LogInformation("override message");

        Assert.NotEmpty(overrideSink.Events);
        Assert.Contains(overrideSink.Events, e => e.RenderMessage().Contains("override message"));
    }

    [Fact]
    public void Override_MinimumLevelFromConfig_IsRespected()
    {
        var sink = new CapturingSink();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Serilog:MinimumLevel"] = "Warning",
            })
            .Build();

        // Build with a sink that captures events — the plugin reads MinimumLevel from config
        var sp = BuildReflectionProviderWithSink(config, sink);

        var logger = sp.GetRequiredService<ILogger<SerilogPluginTests>>();
        logger.LogInformation("should be filtered out");
        logger.LogWarning("should appear");

        Assert.Single(sink.Events);
        Assert.Contains(sink.Events, e => e.RenderMessage().Contains("should appear"));
    }

    // -------------------------------------------------------------------------
    // Bootstrapper path: scenario coverage
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Bootstrapper_DefaultConfig_InvokesCallbackWithLogger()
    {
        Microsoft.Extensions.Logging.ILogger? capturedLogger = null;

        await new NeedlrSerilogBootstrapper()
            .Configure(cfg => cfg.WriteTo.Sink(new CapturingSink()))
            .RunAsync((ctx, ct) =>
            {
                capturedLogger = ctx.Logger;
                return Task.CompletedTask;
            }, TestContext.Current.CancellationToken);

        Assert.NotNull(capturedLogger);
    }

    [Fact]
    public async Task Bootstrapper_CustomSink_ReceivesLogEvents()
    {
        var sink = new CapturingSink();

        await new NeedlrSerilogBootstrapper()
            .Configure(cfg => cfg.WriteTo.Sink(sink))
            .RunAsync((ctx, ct) =>
            {
                ctx.Logger.LogInformation("bootstrapper event");
                return Task.CompletedTask;
            }, TestContext.Current.CancellationToken);

        Assert.NotEmpty(sink.Events);
        Assert.Contains(sink.Events, e => e.RenderMessage().Contains("bootstrapper event"));
    }

    [Fact]
    public async Task Bootstrapper_ExceptionInCallback_DoesNotPropagate()
    {
        await new NeedlrSerilogBootstrapper()
            .Configure(cfg => cfg.WriteTo.Sink(new CapturingSink()))
            .RunAsync(
                (ctx, ct) => throw new InvalidOperationException("expected failure"),
                TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Bootstrapper_CancellationToken_IsForwardedToCallback()
    {
        var token = TestContext.Current.CancellationToken;
        CancellationToken received = default;

        await new NeedlrSerilogBootstrapper()
            .Configure(cfg => cfg.WriteTo.Sink(new CapturingSink()))
            .RunAsync((ctx, ct) =>
            {
                received = ct;
                return Task.CompletedTask;
            }, token);

        Assert.Equal(token, received);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static IServiceProvider BuildReflectionProvider(IConfiguration config) =>
        new Syringe()
            .UsingReflection()
            .UsingAdditionalAssemblies([typeof(SerilogPlugin).Assembly])
            .BuildServiceProvider(config);

    private static IServiceProvider BuildSourceGenProvider(IConfiguration config) =>
        new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider(config);

    private static IServiceProvider BuildReflectionProviderWithSink(
        IConfiguration config, ILogEventSink sink) =>
        new Syringe()
            .UsingReflection()
            .UsingAdditionalAssemblies([typeof(SerilogPlugin).Assembly])
            .UsingPostPluginRegistrationCallback(OverrideSerilogWith(config, sink))
            .BuildServiceProvider(config);

    private static IServiceProvider BuildSourceGenProviderWithSink(
        IConfiguration config, ILogEventSink sink) =>
        new Syringe()
            .UsingSourceGen()
            .UsingPostPluginRegistrationCallback(OverrideSerilogWith(config, sink))
            .BuildServiceProvider(config);

    /// <summary>
    /// Produces the post-plugin callback that replaces the plugin's config-driven
    /// Serilog logger with one that writes to a test sink. This is the consumer
    /// override pattern: the plugin provides the <c>ILoggerFactory</c> /
    /// <c>ILogger&lt;T&gt;</c> open-generic registration; the consumer swaps
    /// the underlying Serilog pipeline.
    /// </summary>
    private static Action<IServiceCollection> OverrideSerilogWith(
        IConfiguration config, ILogEventSink sink) =>
        services => services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(
                new LoggerConfiguration()
                    .ReadFrom.Configuration(config)
                    .WriteTo.Sink(sink)
                    .CreateLogger(),
                dispose: true);
        });
}
