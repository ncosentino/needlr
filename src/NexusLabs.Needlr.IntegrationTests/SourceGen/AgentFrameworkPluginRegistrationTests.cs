using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Budget;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.SourceGen;

/// <summary>
/// Proves that <see cref="IServiceCollectionPlugin"/> implementations can register
/// the agent framework via the <see cref="ServiceCollectionAgentFrameworkExtensions"/>
/// <c>AddNeedlrAgentFramework</c> overloads and all infrastructure types resolve from
/// a real Syringe-built service provider. This is the end-to-end proof that the
/// plugin-based registration path works with real source generation, and that any
/// configuration supplied by the plugin's configure delegate flows through to the
/// constructed services.
/// </summary>
public sealed class AgentFrameworkPluginRegistrationTests
{
    [Fact]
    public void Plugin_RegistersAgentFramework_AllTypesResolve()
    {
        var provider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();

        Assert.NotNull(provider.GetService<IAgentFactory>());
        Assert.NotNull(provider.GetService<IWorkflowFactory>());
        Assert.NotNull(provider.GetService<ITokenBudgetTracker>());
        Assert.NotNull(provider.GetService<IAgentDiagnosticsAccessor>());
        Assert.NotNull(provider.GetService<IToolMetricsAccessor>());
    }

    /// <summary>
    /// Asserts that configuration supplied to <c>AddNeedlrAgentFramework(configure)</c>
    /// from inside an <see cref="IServiceCollectionPlugin"/> survives the full
    /// source-gen registration pipeline. Specifically, the configured meter name
    /// supplied by <see cref="AgentFrameworkRegistrationPlugin"/> via
    /// <c>ConfigureMetrics</c> must end up on the resolved <see cref="IAgentMetrics"/>
    /// activity source — proving the configure delegate ran, the
    /// <see cref="AgentFrameworkSyringe"/> it produced was used to build the
    /// <see cref="IAgentMetrics"/>, and the source-gen pipeline did not silently
    /// discard the user-supplied configuration when merging in generated
    /// function/group/agent types.
    /// </summary>
    [Fact]
    public void Plugin_RegistersAgentFrameworkWithConfigure_MetricsConfigurationFlowsThrough()
    {
        var provider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();

        var metrics = provider.GetRequiredService<IAgentMetrics>();
        Assert.Equal(
            AgentFrameworkRegistrationPlugin.ConfiguredMeterName,
            metrics.ActivitySource.Name);
    }
}

/// <summary>
/// Plugin that registers the Needlr Agent Framework via the configure overload of
/// <c>AddNeedlrAgentFramework</c>. Discovered automatically by Needlr's plugin
/// pipeline — no syringe builder call needed. Owns the metrics configuration so
/// the composition root never has to see it.
/// </summary>
internal sealed class AgentFrameworkRegistrationPlugin : IServiceCollectionPlugin
{
    internal const string ConfiguredMeterName = "IntegrationTests.PluginConfigured.Agents";

    public void Configure(ServiceCollectionPluginOptions options)
    {
        options.Services.AddNeedlrAgentFramework(af => af
            .ConfigureMetrics(o => o.MeterName = ConfiguredMeterName));
    }
}
