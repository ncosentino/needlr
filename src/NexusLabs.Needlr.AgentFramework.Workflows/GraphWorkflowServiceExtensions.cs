using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Iterative;

namespace NexusLabs.Needlr.AgentFramework.Workflows;

/// <summary>
/// Extension methods for registering graph workflow services in the DI container.
/// </summary>
public static class GraphWorkflowServiceExtensions
{
    /// <summary>
    /// Registers <see cref="IGraphWorkflowRunner"/> and its internal dependencies.
    /// Call after <c>UsingAgentFramework()</c>.
    /// </summary>
    public static IServiceCollection AddGraphWorkflowRunner(this IServiceCollection services)
    {
        services.TryAddSingleton<GraphTopologyProvider>();
        services.TryAddSingleton<GraphEdgeRouter>();
        services.TryAddSingleton<IGraphWorkflowRunner>(sp =>
            new GraphWorkflowRunner(
                sp.GetRequiredService<IWorkflowFactory>(),
                sp.GetRequiredService<IAgentFactory>(),
                sp.GetRequiredService<IChatClientAccessor>(),
                sp.GetRequiredService<GraphTopologyProvider>(),
                sp.GetRequiredService<GraphEdgeRouter>(),
                sp.GetService<IAgentDiagnosticsAccessor>()));
        return services;
    }
}
