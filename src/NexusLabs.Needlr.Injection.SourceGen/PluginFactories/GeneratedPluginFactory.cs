using System.Reflection;

using NexusLabs.Needlr.Generators;

namespace NexusLabs.Needlr.Injection.SourceGen.PluginFactories;

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
/// <para>
/// Plugins are sorted by their <see cref="PluginTypeInfo.Order"/> value (lower first),
/// then alphabetically by fully qualified type name for deterministic execution order.
/// Sorting is performed once during initialization.
/// </para>
/// </remarks>
[DoNotAutoRegister]
public sealed class GeneratedPluginFactory : IPluginFactory
{
    private readonly Lazy<IReadOnlyList<PluginTypeInfo>> _lazyPlugins;
    private readonly bool _allowAllWhenAssembliesEmpty;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeneratedPluginFactory"/> class
    /// with a custom plugin provider.
    /// </summary>
    /// <param name="pluginProvider">A function that returns the plugin types.</param>
    /// <param name="allowAllWhenAssembliesEmpty">
    /// When true, passing an empty assemblies collection to the factory methods will not filter plugins.
    /// This is useful for NativeAOT scenarios where producing an <see cref="Assembly"/> list without reflection is not possible.
    /// </param>
    public GeneratedPluginFactory(
        Func<IReadOnlyList<PluginTypeInfo>> pluginProvider,
        bool allowAllWhenAssembliesEmpty = false)
    {
        ArgumentNullException.ThrowIfNull(pluginProvider);
        // Sort once during initialization - filtering maintains relative order
        _lazyPlugins = new(() => pluginProvider.Invoke()
            .OrderBy(info => info.Order)
            .ThenBy(info => info.PluginType.FullName, StringComparer.Ordinal)
            .ToArray());
        _allowAllWhenAssembliesEmpty = allowAllWhenAssembliesEmpty;
    }

    /// <summary>
    /// Creates instances of plugins of type <typeparamref name="TPlugin"/>.
    /// </summary>
    /// <typeparam name="TPlugin">The plugin interface or base type to search for.</typeparam>
    /// <returns>An enumerable of instantiated plugins implementing <typeparamref name="TPlugin"/>.</returns>
    /// <remarks>
    /// Plugins are returned in order by their <see cref="PluginTypeInfo.Order"/> value (lower first),
    /// then alphabetically by fully qualified type name for deterministic execution order.
    /// </remarks>
    public IEnumerable<TPlugin> CreatePlugins<TPlugin>()
        where TPlugin : class
    {
        var pluginType = typeof(TPlugin);
        return _lazyPlugins.Value
            .Where(info => pluginType.IsAssignableFrom(info.PluginType))
            .Select(info => (TPlugin)info.Factory());
    }

    /// <inheritdoc />
    public IEnumerable<TPlugin> CreatePluginsFromAssemblies<TPlugin>(
        IEnumerable<Assembly> assemblies)
        where TPlugin : class
    {
        var assemblyList = assemblies as IReadOnlyCollection<Assembly> ?? assemblies.ToArray();
        if (_allowAllWhenAssembliesEmpty && assemblyList.Count == 0)
        {
            return CreatePlugins<TPlugin>();
        }

        var assemblySet = assemblyList
            .Select(a => a.GetName().Name)
            .Where(n => n is not null)
            .ToHashSet(StringComparer.Ordinal)!;

        var pluginType = typeof(TPlugin);
        return _lazyPlugins.Value
            .Where(info =>
            {
                // Check if this plugin is from one of the specified assemblies
                var pluginAssemblyName = info.PluginType.Assembly.GetName().Name;
                if (pluginAssemblyName is null || !assemblySet.Contains(pluginAssemblyName))
                    return false;

                // Check if plugin implements the requested type
                return pluginType.IsAssignableFrom(info.PluginType);
            })
            .Select(info => (TPlugin)info.Factory());
    }

    /// <summary>
    /// Creates instances of plugins that are decorated with the specified attribute.
    /// </summary>
    /// <typeparam name="TAttribute">The attribute type to search for in the type hierarchy.</typeparam>
    /// <returns>An enumerable of instantiated plugins decorated with <typeparamref name="TAttribute"/>.</returns>
    /// <remarks>
    /// Plugins are returned in order by their <see cref="PluginTypeInfo.Order"/> value (lower first),
    /// then alphabetically by fully qualified type name for deterministic execution order.
    /// </remarks>
    public IEnumerable<object> CreatePluginsWithAttribute<TAttribute>()
        where TAttribute : Attribute
    {
        return _lazyPlugins.Value
            .Where(info => info.HasAttribute<TAttribute>())
            .Select(info => info.Factory());
    }

    /// <inheritdoc />
    public IEnumerable<object> CreatePluginsWithAttributeFromAssemblies<TAttribute>(
        IEnumerable<Assembly> assemblies)
        where TAttribute : Attribute
    {
        var assemblyList = assemblies as IReadOnlyCollection<Assembly> ?? assemblies.ToArray();
        if (_allowAllWhenAssembliesEmpty && assemblyList.Count == 0)
        {
            return CreatePluginsWithAttribute<TAttribute>();
        }

        var assemblySet = assemblyList
            .Select(a => a.GetName().Name)
            .Where(n => n is not null)
            .ToHashSet(StringComparer.Ordinal)!;

        return _lazyPlugins.Value
            .Where(info =>
            {
                // Check if this plugin is from one of the specified assemblies
                var pluginAssemblyName = info.PluginType.Assembly.GetName().Name;
                if (pluginAssemblyName is null || !assemblySet.Contains(pluginAssemblyName))
                    return false;

                // Use pre-computed attribute info - no reflection needed
                return info.HasAttribute<TAttribute>();
            })
            .Select(info => info.Factory());
    }

    /// <summary>
    /// Creates instances of plugins of type <typeparamref name="TPlugin"/> that are 
    /// decorated with the specified attribute.
    /// </summary>
    /// <typeparam name="TPlugin">The plugin interface or base type to search for.</typeparam>
    /// <typeparam name="TAttribute">The attribute type to search for in the type hierarchy.</typeparam>
    /// <returns>
    /// An enumerable of instantiated plugins implementing <typeparamref name="TPlugin"/> and 
    /// decorated with <typeparamref name="TAttribute"/>.
    /// </returns>
    /// <remarks>
    /// Plugins are returned in order by their <see cref="PluginTypeInfo.Order"/> value (lower first),
    /// then alphabetically by fully qualified type name for deterministic execution order.
    /// </remarks>
    public IEnumerable<TPlugin> CreatePlugins<TPlugin, TAttribute>()
        where TPlugin : class
        where TAttribute : Attribute
    {
        var pluginType = typeof(TPlugin);
        return _lazyPlugins.Value
            .Where(info => pluginType.IsAssignableFrom(info.PluginType) && info.HasAttribute<TAttribute>())
            .Select(info => (TPlugin)info.Factory());
    }

    /// <inheritdoc />
    public IEnumerable<TPlugin> CreatePluginsFromAssemblies<TPlugin, TAttribute>(
        IEnumerable<Assembly> assemblies)
        where TPlugin : class
        where TAttribute : Attribute
    {
        var assemblyList = assemblies as IReadOnlyCollection<Assembly> ?? assemblies.ToArray();
        if (_allowAllWhenAssembliesEmpty && assemblyList.Count == 0)
        {
            return CreatePlugins<TPlugin, TAttribute>();
        }

        var assemblySet = assemblyList
            .Select(a => a.GetName().Name)
            .Where(n => n is not null)
            .ToHashSet(StringComparer.Ordinal)!;

        var pluginType = typeof(TPlugin);
        return _lazyPlugins.Value
            .Where(info =>
            {
                // Check if this plugin is from one of the specified assemblies
                var pluginAssemblyName = info.PluginType.Assembly.GetName().Name;
                if (pluginAssemblyName is null || !assemblySet.Contains(pluginAssemblyName))
                    return false;

                if (!pluginType.IsAssignableFrom(info.PluginType))
                    return false;

                // Use pre-computed attribute info - no reflection needed
                return info.HasAttribute<TAttribute>();
            })
            .Select(info => (TPlugin)info.Factory());
    }
}
