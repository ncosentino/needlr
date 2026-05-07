using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Extension methods that register the small set of Needlr Agent Framework accessors
/// that tools and test harnesses depend on, without registering the full
/// <see cref="IAgentFactory"/> / <see cref="WorkflowFactory"/> / iterative-loop infrastructure.
/// </summary>
/// <remarks>
/// <para>
/// Use this when you need the Needlr accessors (<see cref="IAgentExecutionContextAccessor"/>,
/// <see cref="IAgentDiagnosticsAccessor"/>, <see cref="IAgentDiagnosticsWriter"/>) but do not
/// want the rest of the Agent Framework wiring — typically because you are constructing a
/// minimal service provider for a tool-level test.
/// </para>
/// <para>
/// The <c>UsingAgentFramework()</c> extension on <see cref="NexusLabs.Needlr.Injection.ConfiguredSyringe"/>
/// already registers these accessors as part of its broader infrastructure setup. Calling
/// <see cref="AddAgentFrameworkAccessors"/> after <c>UsingAgentFramework()</c> is a no-op because
/// every registration uses <see cref="ServiceCollectionDescriptorExtensions.TryAdd(IServiceCollection, ServiceDescriptor)"/>.
/// </para>
/// </remarks>
public static class AgentFrameworkAccessorServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Needlr Agent Framework accessor singletons:
    /// <see cref="IAgentExecutionContextAccessor"/>, <see cref="IAgentDiagnosticsAccessor"/>,
    /// and <see cref="IAgentDiagnosticsWriter"/>.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    /// <remarks>
    /// All registrations use <c>TryAddSingleton</c>, so calling this method is safe even if the
    /// accessors have already been registered by <c>UsingAgentFramework()</c> or another path.
    /// </remarks>
    public static IServiceCollection AddAgentFrameworkAccessors(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IAgentExecutionContextAccessor, AgentExecutionContextAccessor>();
        services.TryAddSingleton<AgentDiagnosticsAccessor>(sp =>
            new AgentDiagnosticsAccessor(
                sp.GetService<ChatCompletionCollectorHolder>(),
                sp.GetService<ToolCallCollectorHolder>()));
        services.TryAddSingleton<IAgentDiagnosticsAccessor>(sp =>
            sp.GetRequiredService<AgentDiagnosticsAccessor>());
        services.TryAddSingleton<IAgentDiagnosticsWriter>(sp =>
            sp.GetRequiredService<AgentDiagnosticsAccessor>());

        return services;
    }
}
