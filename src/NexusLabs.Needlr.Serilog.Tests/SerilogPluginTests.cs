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

        Assert.NotNull(sp.GetService<ILogger<SerilogPluginTests>>());
    }

    [Fact]
    public void Reflection_LoggerWritesToSerilogPipeline()
    {
        var sink = new CapturingSink();
        var sp = BuildReflectionProviderWithSink(EmptyConfig, sink);

        sp.GetRequiredService<ILogger<SerilogPluginTests>>()
            .LogInformation("test message from reflection");

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

        Assert.NotNull(sp.GetService<ILogger<SerilogPluginTests>>());
    }

    [Fact]
    public void SourceGen_LoggerWritesToSerilogPipeline()
    {
        var sink = new CapturingSink();
        var sp = BuildSourceGenProviderWithSink(EmptyConfig, sink);

        sp.GetRequiredService<ILogger<SerilogPluginTests>>()
            .LogInformation("test message from source-gen");

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

        Assert.NotNull(reflectionSp.GetService<ILoggerFactory>());
        Assert.NotNull(sourceGenSp.GetService<ILoggerFactory>());
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
    // Override: consumer replaces the plugin's Serilog configuration
    // -------------------------------------------------------------------------

    [Fact]
    public void Reflection_Override_ConsumerCanReconfigureSerilogAfterPlugin()
    {
        var overrideSink = new CapturingSink();
        var sp = BuildReflectionProviderWithSink(EmptyConfig, overrideSink);

        sp.GetRequiredService<ILogger<SerilogPluginTests>>()
            .LogInformation("override message");

        Assert.NotEmpty(overrideSink.Events);
        Assert.Contains(overrideSink.Events, e => e.RenderMessage().Contains("override message"));
    }

    [Fact]
    public void SourceGen_Override_ConsumerCanReconfigureSerilogAfterPlugin()
    {
        var overrideSink = new CapturingSink();
        var sp = BuildSourceGenProviderWithSink(EmptyConfig, overrideSink);

        sp.GetRequiredService<ILogger<SerilogPluginTests>>()
            .LogInformation("override message");

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

        var sp = BuildReflectionProviderWithSink(config, sink);

        var logger = sp.GetRequiredService<ILogger<SerilogPluginTests>>();
        logger.LogInformation("should be filtered out");
        logger.LogWarning("should appear");

        Assert.Single(sink.Events);
        Assert.Contains(sink.Events, e => e.RenderMessage().Contains("should appear"));
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
