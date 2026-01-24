using Microsoft.Extensions.Hosting;

using NexusLabs.Needlr.Injection;

namespace NexusLabs.Needlr.Hosting;

/// <summary>
/// Extension methods for configuring <see cref="ConfiguredSyringe"/> instances with generic host functionality.
/// </summary>
/// <example>
/// Source-gen first (recommended for AOT/trimming):
/// <code>
/// // With module initializer bootstrap (automatic):
/// var host = new Syringe()
///     .UsingSourceGen()
///     .ForHost()
///     .BuildHost();
/// 
/// await host.RunAsync();
/// </code>
/// 
/// Reflection-based (for dynamic scenarios):
/// <code>
/// var host = new Syringe()
///     .UsingReflection()
///     .ForHost()
///     .BuildHost();
/// 
/// await host.RunAsync();
/// </code>
/// </example>
public static class SyringeHostingExtensions
{
    /// <summary>
    /// Transitions the configured syringe to host mode, enabling host-specific configuration.
    /// </summary>
    /// <param name="syringe">The configured syringe to transition.</param>
    /// <returns>A new host syringe instance.</returns>
    /// <example>
    /// <code>
    /// var hostSyringe = new Syringe()
    ///     .UsingReflection()
    ///     .ForHost(); // Transition to host mode
    /// 
    /// // Now you can use host-specific methods
    /// var host = hostSyringe
    ///     .UsingOptions(() => CreateHostOptions.Default.UsingArgs(args))
    ///     .BuildHost();
    /// </code>
    /// </example>
    public static HostSyringe ForHost(this ConfiguredSyringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return new HostSyringe(syringe);
    }

    /// <summary>
    /// Builds a host with the configured settings using the default HostFactory.
    /// </summary>
    /// <param name="syringe">The configured syringe to build from.</param>
    /// <returns>The configured <see cref="IHost"/>.</returns>
    /// <example>
    /// <code>
    /// // Direct build without additional host configuration
    /// var host = new Syringe()
    ///     .UsingReflection()
    ///     .BuildHost();
    /// 
    /// await host.RunAsync();
    /// </code>
    /// </example>
    public static IHost BuildHost(this ConfiguredSyringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.ForHost().BuildHost();
    }
}
