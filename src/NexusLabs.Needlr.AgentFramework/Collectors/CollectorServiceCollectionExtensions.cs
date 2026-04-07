using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NexusLabs.Needlr.AgentFramework.Collectors;

/// <summary>
/// Extension methods for registering <see cref="IAgentOutputCollectorAccessor{T}"/> in DI.
/// </summary>
/// <remarks>
/// Generic open types cannot be auto-registered by the framework. Call
/// <see cref="AddAgentOutputCollector{T}"/> once per record type at startup.
/// </remarks>
public static class CollectorServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IAgentOutputCollectorAccessor{T}"/> as a singleton for the given
    /// record type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The record type the collector accumulates.</typeparam>
    public static IServiceCollection AddAgentOutputCollector<T>(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IAgentOutputCollectorAccessor<T>, AgentOutputCollectorAccessor<T>>();
        return services;
    }
}
