using System.Diagnostics.Metrics;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Tests.Diagnostics;

/// <summary>
/// Tests that <see cref="IGenAiTokenMetrics"/> is registered correctly by the agent
/// framework DI extensions: always-real implementation by default (mirroring
/// <see cref="IAgentMetrics"/>), respects <c>UsingDiagnostics()</c>, and the
/// <see cref="AgentFrameworkMetricsOptions.GenAiMeterName"/> value flows from
/// <c>ConfigureMetrics</c> through to the underlying <see cref="Meter"/>.
/// </summary>
public sealed class GenAiTokenMetricsRegistrationTests
{
    [Fact]
    public void DefaultRegistration_IsRealGenAiTokenMetrics_NotNoOp()
    {
        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework()
            .BuildServiceProvider(new ConfigurationBuilder().Build());

        var resolved = sp.GetService<IGenAiTokenMetrics>();
        Assert.NotNull(resolved);
        Assert.IsType<GenAiTokenMetrics>(resolved);
    }

    [Fact]
    public void WithUsingDiagnostics_RegistrationIsStillReal()
    {
        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af.UsingDiagnostics())
            .BuildServiceProvider(new ConfigurationBuilder().Build());

        var resolved = sp.GetService<IGenAiTokenMetrics>();
        Assert.NotNull(resolved);
        Assert.IsType<GenAiTokenMetrics>(resolved);
    }

    [Fact]
    public void DefaultGenAiMeterName_IsExperimentalMicrosoftExtensionsAI()
    {
        var listener = new MeterListener();
        var meterNamesSeen = new List<string>();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "gen_ai.client.token.usage")
                meterNamesSeen.Add(instrument.Meter.Name);
        };
        listener.Start();

        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework()
            .BuildServiceProvider(new ConfigurationBuilder().Build());

        sp.GetRequiredService<IGenAiTokenMetrics>();

        Assert.Contains("Experimental.Microsoft.Extensions.AI", meterNamesSeen);
    }

    [Fact]
    public void ConfigureMetrics_GenAiMeterName_FlowsToInstrument()
    {
        var customName = $"MyApp.GenAI.{Guid.NewGuid():N}";

        var listener = new MeterListener();
        var meterNamesSeen = new List<string>();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "gen_ai.client.token.usage")
                meterNamesSeen.Add(instrument.Meter.Name);
        };
        listener.Start();

        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af.ConfigureMetrics(o => o.GenAiMeterName = customName))
            .BuildServiceProvider(new ConfigurationBuilder().Build());

        sp.GetRequiredService<IGenAiTokenMetrics>();

        Assert.Contains(customName, meterNamesSeen);
    }

    [Fact]
    public void IGenAiTokenMetrics_IsSingleton_SameInstanceAcrossResolutions()
    {
        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework()
            .BuildServiceProvider(new ConfigurationBuilder().Build());

        var first = sp.GetRequiredService<IGenAiTokenMetrics>();
        var second = sp.GetRequiredService<IGenAiTokenMetrics>();

        Assert.Same(first, second);
    }
}
