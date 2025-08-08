using NexusLabs.Needlr.Injection;

namespace NexusLabs.Needlr.Injection.Scrutor;

/// <summary>
/// Extension methods for configuring <see cref="Syringe"/> instances with Scrutor-specific functionality.
/// </summary>
/// <example>
/// Basic usage with Scrutor:
/// <code>
/// var serviceProvider = new Syringe()
///     .UsingScrutorTypeRegistrar()
///     .UsingDefaultTypeFilterer()
///     .BuildServiceProvider();
/// </code>
/// 
/// Combined with other configuration:
/// <code>
/// var syringe = new Syringe()
///     .UsingScrutorTypeRegistrar()
///     .UsingTypeFilterer(customFilterer)
///     .UsingAssemblyProvider(builder => builder
///         .MatchingAssemblies(x => x.Contains("MyApp"))
///         .Build());
/// </code>
/// </example>
public static class SyringeScrutorExtensions
{
    /// <summary>
    /// Configures the syringe to use the Scrutor type registrar.
    /// This enables automatic service registration using the Scrutor library for assembly scanning.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// var syringe = new Syringe()
    ///     .UsingScrutorTypeRegistrar()
    ///     .UsingDefaultTypeFilterer();
    /// 
    /// // Use in a web application
    /// var webApp = syringe
    ///     .ForWebApplication()
    ///     .BuildWebApplication();
    /// </code>
    /// </example>
    public static Syringe UsingScrutorTypeRegistrar(this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.UsingTypeRegistrar(new ScrutorTypeRegistrar());
    }
}