using Microsoft.Extensions.DependencyInjection;

namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> that register the Needlr Agent Framework
/// infrastructure directly, without requiring the <see cref="Injection.ConfiguredSyringe"/> fluent builder.
/// </summary>
/// <remarks>
/// <para>
/// Use this when registering the agent framework from an <see cref="IServiceCollectionPlugin"/>
/// so that feature projects can self-register without modifying the composition root:
/// </para>
/// <code>
/// public sealed class AgentFrameworkPlugin : IServiceCollectionPlugin
/// {
///     public void Configure(ServiceCollectionPluginOptions options)
///     {
///         options.Services.AddNeedlrAgentFramework();
///     }
/// }
/// </code>
/// <para>
/// This calls the same code path as
/// <see cref="SyringeExtensionsForAgentFramework.UsingAgentFramework(Injection.ConfiguredSyringe)"/>
/// — zero duplication, zero drift between the two entry points.
/// </para>
/// </remarks>
public static class ServiceCollectionAgentFrameworkExtensions
{
    /// <summary>
    /// Registers the full Needlr Agent Framework infrastructure on the service collection.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddNeedlrAgentFramework(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        SyringeExtensionsForAgentFramework.RegisterAgentFrameworkCore(services);

        return services;
    }
}
