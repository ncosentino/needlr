using System.Reflection;

using NexusLabs.Needlr.Generators;

namespace NexusLabs.Needlr.Injection.PluginFactories;

/// <summary>
/// A plugin factory that uses compile-time generated type information
/// instead of runtime reflection for plugin discovery and instantiation.
/// </summary>
/// <remarks>
/// <para>
/// This factory provides better performance and AOT compatibility by using
/// pre-computed plugin information and factory delegates generated at compile time.
/// </para>
/// <para>
/// To use this factory, your assembly must have:
/// <list type="bullet">
/// <item>A reference to <c>NexusLabs.Needlr.Generators</c></item>
/// <item>The <c>[assembly: GenerateTypeRegistry(...)]</c> attribute</item>
/// </list>
/// </para>
/// </remarks>
[DoNotAutoRegister]
public sealed class GeneratedPluginFactory : IPluginFactory
{
    private readonly Func<IReadOnlyList<PluginTypeInfo>> _pluginProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeneratedPluginFactory"/> class
    /// using the generated TypeRegistry.
    /// </summary>
    /// <remarks>
    /// Uses the generated <c>NexusLabs.Needlr.Generated.TypeRegistry.GetPluginTypes()</c> method.
    /// </remarks>
    public GeneratedPluginFactory()
    {
        // Use reflection to find the generated TypeRegistry
        _pluginProvider = FindGeneratedTypeRegistry();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GeneratedPluginFactory"/> class
    /// with a custom plugin provider.
    /// </summary>
    /// <param name="pluginProvider">A function that returns the plugin types.</param>
    public GeneratedPluginFactory(Func<IReadOnlyList<PluginTypeInfo>> pluginProvider)
    {
        ArgumentNullException.ThrowIfNull(pluginProvider);
        _pluginProvider = pluginProvider;
    }

    /// <inheritdoc />
    public IEnumerable<TPlugin> CreatePluginsFromAssemblies<TPlugin>(
        IEnumerable<Assembly> assemblies)
        where TPlugin : class
    {
        var pluginType = typeof(TPlugin);
        var assemblySet = assemblies.Select(a => a.GetName().Name).ToHashSet(StringComparer.Ordinal);

        foreach (var info in _pluginProvider())
        {
            // Check if this plugin is from one of the specified assemblies
            var pluginAssemblyName = info.PluginType.Assembly.GetName().Name;
            if (!assemblySet.Contains(pluginAssemblyName))
                continue;

            // Check if plugin implements the requested type
            if (!pluginType.IsAssignableFrom(info.PluginType))
                continue;

            // Use the pre-compiled factory delegate instead of Activator.CreateInstance
            yield return (TPlugin)info.Factory();
        }
    }

    /// <inheritdoc />
    public IEnumerable<object> CreatePluginsWithAttributeFromAssemblies<TAttribute>(
        IEnumerable<Assembly> assemblies)
        where TAttribute : Attribute
    {
        var assemblySet = assemblies.Select(a => a.GetName().Name).ToHashSet(StringComparer.Ordinal);

        foreach (var info in _pluginProvider())
        {
            var pluginAssemblyName = info.PluginType.Assembly.GetName().Name;
            if (!assemblySet.Contains(pluginAssemblyName))
                continue;

            // Check for attribute - this still requires reflection but happens rarely
            if (info.PluginType.GetCustomAttribute<TAttribute>(inherit: true) is null)
                continue;

            yield return info.Factory();
        }
    }

    /// <inheritdoc />
    public IEnumerable<TPlugin> CreatePluginsFromAssemblies<TPlugin, TAttribute>(
        IEnumerable<Assembly> assemblies)
        where TPlugin : class
        where TAttribute : Attribute
    {
        var pluginType = typeof(TPlugin);
        var assemblySet = assemblies.Select(a => a.GetName().Name).ToHashSet(StringComparer.Ordinal);

        foreach (var info in _pluginProvider())
        {
            var pluginAssemblyName = info.PluginType.Assembly.GetName().Name;
            if (!assemblySet.Contains(pluginAssemblyName))
                continue;

            if (!pluginType.IsAssignableFrom(info.PluginType))
                continue;

            // Check for attribute
            if (info.PluginType.GetCustomAttribute<TAttribute>(inherit: true) is null)
                continue;

            yield return (TPlugin)info.Factory();
        }
    }

    private static Func<IReadOnlyList<PluginTypeInfo>> FindGeneratedTypeRegistry()
    {
        // Search all loaded assemblies for the generated TypeRegistry
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var typeRegistryType = assembly.GetType("NexusLabs.Needlr.Generated.TypeRegistry");
            if (typeRegistryType is null)
                continue;

            var method = typeRegistryType.GetMethod(
                "GetPluginTypes",
                BindingFlags.Public | BindingFlags.Static);

            if (method is null)
                continue;

            return () => (IReadOnlyList<PluginTypeInfo>)method.Invoke(null, null)!;
        }

        // Return empty list if no generated registry found
        return static () => [];
    }
}
