using NexusLabs.Needlr.Injection;

namespace NexusLabs.Needlr.Injection.Scrutor;

/// <summary>
/// Extension methods for configuring <see cref="ConfiguredSyringe"/> instances with Scrutor-specific functionality.
/// </summary>
/// <remarks>
/// Scrutor uses runtime reflection for assembly scanning. 
/// For AOT/trimming compatibility, use source-generated components instead.
/// </remarks>
/// <example>
/// Reflection-based usage with Scrutor:
/// <code>
/// var serviceProvider = new Syringe()
///     .UsingReflection()
///     .UsingScrutorTypeRegistrar()
///     .BuildServiceProvider();
/// </code>
/// </example>
public static class SyringeScrutorExtensions
{
    /// <summary>
    /// Configures the syringe to use the Scrutor type registrar.
    /// This enables automatic service registration using the Scrutor library for assembly scanning.
    /// </summary>
    /// <param name="syringe">The configured syringe to update.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// var syringe = new Syringe()
    ///     .UsingReflection()
    ///     .UsingScrutorTypeRegistrar();
    /// </code>
    /// </example>
    public static ConfiguredSyringe UsingScrutorTypeRegistrar(this ConfiguredSyringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.UsingTypeRegistrar(new ScrutorTypeRegistrar());
    }
}