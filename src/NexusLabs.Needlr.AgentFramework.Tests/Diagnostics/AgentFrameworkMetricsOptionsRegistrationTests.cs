using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Tests.Diagnostics;

/// <summary>
/// Regression coverage for the latent bug where
/// <see cref="AgentFrameworkMetricsOptions"/> was never DI-registered, despite
/// being read via <c>opts.ServiceProvider.GetService&lt;AgentFrameworkMetricsOptions&gt;()</c>
/// in <see cref="DiagnosticsExtensions.UsingDiagnostics"/>. The resolution always
/// returned <see langword="null"/>, so the <c>?? ChatCompletionActivityMode.Always</c>
/// fallback silently kicked in and consumers setting
/// <c>ChatCompletionActivityMode = EnrichParent</c> via <c>ConfigureMetrics(...)</c>
/// were ignored.
/// </summary>
public sealed class AgentFrameworkMetricsOptionsRegistrationTests
{
    [Fact]
    public void ConfigureMetrics_AgentFrameworkMetricsOptions_IsResolvableFromDi()
    {
        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .ConfigureMetrics(o => o.ChatCompletionActivityMode = ChatCompletionActivityMode.EnrichParent))
            .BuildServiceProvider(new ConfigurationBuilder().Build());

        var options = sp.GetService<AgentFrameworkMetricsOptions>();
        Assert.NotNull(options);
        Assert.Equal(ChatCompletionActivityMode.EnrichParent, options.ChatCompletionActivityMode);
    }

    [Fact]
    public void DefaultRegistration_AgentFrameworkMetricsOptions_ReturnsDefaultInstance()
    {
        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework()
            .BuildServiceProvider(new ConfigurationBuilder().Build());

        var options = sp.GetService<AgentFrameworkMetricsOptions>();
        Assert.NotNull(options);
        Assert.Equal("NexusLabs.Needlr.AgentFramework", options.MeterName);
        Assert.Equal("Experimental.Microsoft.Extensions.AI", options.GenAiMeterName);
        Assert.Equal(ChatCompletionActivityMode.Always, options.ChatCompletionActivityMode);
    }

    [Fact]
    public void ConfigureMetrics_AllPropertiesFlowToDiResolvedInstance()
    {
        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .ConfigureMetrics(o =>
                {
                    o.MeterName = "MyApp.Agents";
                    o.ActivitySourceName = "MyApp.Agents.Tracing";
                    o.GenAiMeterName = "MyApp.GenAI";
                    o.ChatCompletionActivityMode = ChatCompletionActivityMode.EnrichParent;
                }))
            .BuildServiceProvider(new ConfigurationBuilder().Build());

        var options = sp.GetRequiredService<AgentFrameworkMetricsOptions>();
        Assert.Equal("MyApp.Agents", options.MeterName);
        Assert.Equal("MyApp.Agents.Tracing", options.ActivitySourceName);
        Assert.Equal("MyApp.GenAI", options.GenAiMeterName);
        Assert.Equal(ChatCompletionActivityMode.EnrichParent, options.ChatCompletionActivityMode);
    }

    [Fact]
    public void Resolution_IsSingleton_SameInstanceAcrossResolutions()
    {
        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .ConfigureMetrics(o => o.MeterName = "X"))
            .BuildServiceProvider(new ConfigurationBuilder().Build());

        var first = sp.GetRequiredService<AgentFrameworkMetricsOptions>();
        var second = sp.GetRequiredService<AgentFrameworkMetricsOptions>();
        Assert.Same(first, second);
    }
}
