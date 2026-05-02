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
/// the agent framework via <see cref="ServiceCollectionAgentFrameworkExtensions.AddNeedlrAgentFramework"/>
/// and all infrastructure types resolve from a real Syringe-built service provider.
/// This is the end-to-end proof that the plugin-based registration path works with
/// real source generation.
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
}

/// <summary>
/// Plugin that registers the Needlr Agent Framework via
/// <see cref="ServiceCollectionAgentFrameworkExtensions.AddNeedlrAgentFramework"/>.
/// Discovered automatically by Needlr's plugin pipeline — no syringe builder
/// call needed.
/// </summary>
internal sealed class AgentFrameworkRegistrationPlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        options.Services.AddNeedlrAgentFramework();
    }
}
