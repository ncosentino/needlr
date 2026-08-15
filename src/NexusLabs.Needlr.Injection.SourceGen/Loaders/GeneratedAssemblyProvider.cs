using System.Reflection;

using NexusLabs.Needlr.Generators;

namespace NexusLabs.Needlr.Injection.SourceGen.Loaders;

/// <summary>
/// An assembly provider that derives assemblies from the generated TypeRegistry.
/// </summary>
/// <remarks>
/// <para>
/// When using source generation, the TypeRegistry contains all injectable types
/// and plugins discovered at compile time. Generated bootstraps also provide a
/// registry participant type so assemblies with no injectable types or plugins
/// remain available without runtime assembly scanning.
/// </para>
/// <para>
/// This provider should be used with <see cref="PluginFactories.GeneratedPluginFactory"/>
/// to ensure that all generated registry participants are included in plugin discovery
/// when runtime assembly metadata is available.
/// </para>
/// <para>
/// For assembly ordering, use <c>SyringeExtensions.OrderAssemblies</c> after configuring the Syringe.
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
    /// <remarks>
    /// This compatibility overload can derive assemblies only from injectable and plugin metadata.
    /// Use the three-argument overload when generated registry participant types are available.
    /// </remarks>
    public GeneratedAssemblyProvider(
        Func<IReadOnlyList<InjectableTypeInfo>> injectableTypesProvider,
        Func<IReadOnlyList<PluginTypeInfo>> pluginTypesProvider)
        : this(
            injectableTypesProvider,
            pluginTypesProvider,
            Array.Empty<Type>())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GeneratedAssemblyProvider"/> class.
    /// </summary>
    /// <param name="injectableTypesProvider">A function that returns the injectable types.</param>
    /// <param name="pluginTypesProvider">A function that returns the plugin types.</param>
    /// <param name="registryParticipantTypes">
    /// Generated types whose assemblies identify every TypeRegistry participant.
    /// </param>
    /// <remarks>
    /// Assemblies represented by injectable and plugin metadata preserve their existing order.
    /// Marker-only participant assemblies are appended in registration order.
    /// </remarks>
    public GeneratedAssemblyProvider(
        Func<IReadOnlyList<InjectableTypeInfo>> injectableTypesProvider,
        Func<IReadOnlyList<PluginTypeInfo>> pluginTypesProvider,
        IReadOnlyList<Type> registryParticipantTypes)
    {
        ArgumentNullException.ThrowIfNull(injectableTypesProvider);
        ArgumentNullException.ThrowIfNull(pluginTypesProvider);
        ArgumentNullException.ThrowIfNull(registryParticipantTypes);

        var registryParticipantTypeSnapshot = registryParticipantTypes.ToArray();
        _lazyAssemblies = new(() =>
        {
            // In NativeAOT with reflection disabled, Type.Assembly can throw.
            // Candidate assemblies are only a hint for reflection-based discovery.
            // For source generation, the plugin/type registries already define the universe.
            try
            {
                var assemblies = new List<Assembly>();
                var seenAssemblies = new HashSet<Assembly>();

                foreach (var info in injectableTypesProvider())
                {
                    AddAssembly(info.Type.Assembly);
                }

                foreach (var info in pluginTypesProvider())
                {
                    AddAssembly(info.PluginType.Assembly);
                }

                foreach (var registryParticipantType in registryParticipantTypeSnapshot)
                {
                    AddAssembly(registryParticipantType.Assembly);
                }

                return assemblies;

                void AddAssembly(Assembly assembly)
                {
                    if (seenAssemblies.Add(assembly))
                    {
                        assemblies.Add(assembly);
                    }
                }
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
