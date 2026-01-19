using System.Reflection;

using NexusLabs.Needlr.Generators;

namespace NexusLabs.Needlr.Injection.SourceGen.Loaders;

/// <summary>
/// An assembly provider that derives assemblies from the generated TypeRegistry.
/// </summary>
/// <remarks>
/// <para>
/// When using source generation, the TypeRegistry contains all injectable types
/// and plugins discovered at compile time. This provider extracts the unique
/// assemblies from those types, enabling cross-assembly plugin discovery without
/// runtime assembly scanning.
/// </para>
/// <para>
/// This provider should be used with <see cref="PluginFactories.GeneratedPluginFactory"/>
/// to ensure that all assemblies containing generated types are included in plugin discovery.
/// </para>
/// </remarks>
[DoNotAutoRegister]
public sealed class GeneratedAssemblyProvider : IAssemblyProvider
{
    private readonly Lazy<IReadOnlyList<Assembly>> _lazyAssemblies;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeneratedAssemblyProvider"/> class.
    /// </summary>
    /// <param name="injectableTypesProvider">A function that returns the injectable types.</param>
    /// <param name="pluginTypesProvider">A function that returns the plugin types.</param>
    public GeneratedAssemblyProvider(
        Func<IReadOnlyList<InjectableTypeInfo>> injectableTypesProvider,
        Func<IReadOnlyList<PluginTypeInfo>> pluginTypesProvider)
    {
        ArgumentNullException.ThrowIfNull(injectableTypesProvider);
        ArgumentNullException.ThrowIfNull(pluginTypesProvider);

        _lazyAssemblies = new(() =>
        {
            // In NativeAOT with reflection disabled, Type.Assembly can throw.
            // Candidate assemblies are only a hint for reflection-based discovery.
            // For source generation, the plugin/type registries already define the universe.
            try
            {
                var assemblies = new HashSet<Assembly>();

                // Extract assemblies from injectable types
                foreach (var info in injectableTypesProvider())
                {
                    assemblies.Add(info.Type.Assembly);
                }

                // Extract assemblies from plugin types
                foreach (var info in pluginTypesProvider())
                {
                    assemblies.Add(info.PluginType.Assembly);
                }

                return assemblies.ToList();
            }
            catch (NotSupportedException)
            {
                return Array.Empty<Assembly>();
            }
        });
    }

    /// <inheritdoc />
    public IReadOnlyList<Assembly> GetCandidateAssemblies() =>
        _lazyAssemblies.Value;
}
