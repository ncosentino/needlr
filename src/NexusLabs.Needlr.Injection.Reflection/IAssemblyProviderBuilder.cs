using NexusLabs.Needlr.Injection.AssemblyOrdering;

namespace NexusLabs.Needlr.Injection.Reflection;

/// <summary>
/// Defines a builder for constructing <see cref="IAssemblyProvider"/> instances with custom loaders and ordering.
/// Use this to configure how assemblies are discovered and ordered for dependency injection.
/// </summary>
[DoNotAutoRegister]
public interface IAssemblyProviderBuilder
{
    IAssemblyProvider Build();
    AssemblyProviderBuilder UseLoader(IAssemblyLoader loader);
    
    /// <summary>
    /// Configures assembly ordering using expression-based rules.
    /// Assemblies are sorted into tiers based on the first matching rule.
    /// Unmatched assemblies are placed last.
    /// </summary>
    /// <param name="configure">Action to configure the ordering rules.</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.OrderAssemblies(order => order
    ///     .By(a => a.Location.EndsWith(".exe"))
    ///     .ThenBy(a => a.Name.StartsWith("MyApp"))
    ///     .ThenBy(a => !a.Name.Contains("Tests")));
    /// </code>
    /// </example>
    AssemblyProviderBuilder OrderAssemblies(Action<AssemblyOrderBuilder> configure);
}