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
/// The configure overloads let plugins own the full agent-framework configuration surface
/// (custom <see cref="Microsoft.Extensions.AI.IChatClient"/>, metrics meter / activity source
/// names, diagnostics, token budget tracking, etc.) without exfiltrating that configuration
/// to the composition root:
/// </para>
/// <code>
/// public sealed class AgentFrameworkPlugin : IServiceCollectionPlugin
/// {
///     public void Configure(ServiceCollectionPluginOptions options)
///     {
///         options.Services.AddNeedlrAgentFramework(af => af
///             .ConfigureMetrics(o =>
///             {
///                 o.MeterName = "MyApp.Agents";
///                 o.ActivitySourceName = "MyApp.Agents";
///             }));
///     }
/// }
/// </code>
/// <para>
/// All overloads call the same code path as
/// <see cref="SyringeExtensionsForAgentFramework.UsingAgentFramework(Injection.ConfiguredSyringe)"/>
/// — zero duplication, zero drift between the two entry points.
/// </para>
/// <para>
/// When both this entry point and <c>UsingAgentFramework</c> are used in the same application,
/// the first registration wins (<c>TryAddSingleton</c> semantics). Configuration delegates
/// supplied by later registrations are silently discarded.
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

    /// <summary>
    /// Registers the full Needlr Agent Framework infrastructure on the service collection
    /// with a configurable <see cref="AgentFrameworkSyringe"/>.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">
    /// A delegate that receives a pre-initialized <see cref="AgentFrameworkSyringe"/>
    /// (with its <see cref="AgentFrameworkSyringe.ServiceProvider"/> set) and
    /// returns the configured instance used to build the agent factory. Invoked
    /// lazily the first time <see cref="BuiltAgentFrameworkSyringe"/> is resolved.
    /// </param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddNeedlrAgentFramework(
        this IServiceCollection services,
        Func<AgentFrameworkSyringe, AgentFrameworkSyringe> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        SyringeExtensionsForAgentFramework.RegisterAgentFrameworkCore(services, configure);

        return services;
    }

    /// <summary>
    /// Registers the full Needlr Agent Framework infrastructure on the service collection
    /// using an <see cref="AgentFrameworkSyringe"/> created by the supplied factory.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">
    /// A factory that creates a fully-configured <see cref="AgentFrameworkSyringe"/>
    /// used to build the agent factory. Useful when configuration does not need the
    /// service provider. Routes through the
    /// <see cref="AddNeedlrAgentFramework(IServiceCollection, Func{AgentFrameworkSyringe, AgentFrameworkSyringe})"/>
    /// overload so both paths share the same <see cref="BuiltAgentFrameworkSyringe"/>
    /// construction and progress sink wiring.
    /// </param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddNeedlrAgentFramework(
        this IServiceCollection services,
        Func<AgentFrameworkSyringe> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        return services.AddNeedlrAgentFramework(_ => configure.Invoke());
    }
}
