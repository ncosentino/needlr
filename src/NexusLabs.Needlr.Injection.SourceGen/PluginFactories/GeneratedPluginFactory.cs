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
        _lazyPlugins = new(() => pluginProvider.Invoke().ToArray());
        _allowAllWhenAssembliesEmpty = allowAllWhenAssembliesEmpty;
    }

    /// <summary>
    /// Creates instances of plugins of type <typeparamref name="TPlugin"/>.
    /// </summary>
    /// <typeparam name="TPlugin">The plugin interface or base type to search for.</typeparam>
    /// <returns>An enumerable of instantiated plugins implementing <typeparamref name="TPlugin"/>.</returns>
    public IEnumerable<TPlugin> CreatePlugins<TPlugin>()
        where TPlugin : class
    {
        var pluginType = typeof(TPlugin);
        foreach (var info in _lazyPlugins.Value)
        {
            if (!pluginType.IsAssignableFrom(info.PluginType))
                continue;

            yield return (TPlugin)info.Factory();
        }
    }

    /// <inheritdoc />
    public IEnumerable<TPlugin> CreatePluginsFromAssemblies<TPlugin>(
        IEnumerable<Assembly> assemblies)
        where TPlugin : class
    {
        var assemblyList = assemblies as IReadOnlyCollection<Assembly> ?? assemblies.ToArray();
        if (_allowAllWhenAssembliesEmpty && assemblyList.Count == 0)
        {
            foreach (var p in CreatePlugins<TPlugin>())
            {
                yield return p;
            }

            yield break;
        }

        var assemblySet = assemblyList
            .Select(a => a.GetName().Name)
            .Where(n => n is not null)
            .ToHashSet(StringComparer.Ordinal)!;

        var pluginType = typeof(TPlugin);
        foreach (var info in _lazyPlugins.Value)
        {
            // Check if this plugin is from one of the specified assemblies
            var pluginAssemblyName = info.PluginType.Assembly.GetName().Name;
            if (pluginAssemblyName is null || !assemblySet.Contains(pluginAssemblyName))
                continue;

            // Check if plugin implements the requested type
            if (!pluginType.IsAssignableFrom(info.PluginType))
                continue;

            // Use the pre-compiled factory delegate instead of Activator.CreateInstance
            yield return (TPlugin)info.Factory();
        }
    }

    /// <summary>
    /// Creates instances of plugins that are decorated with the specified attribute.
    /// </summary>
    /// <typeparam name="TAttribute">The attribute type to search for in the type hierarchy.</typeparam>
    /// <returns>An enumerable of instantiated plugins decorated with <typeparamref name="TAttribute"/>.</returns>
    public IEnumerable<object> CreatePluginsWithAttribute<TAttribute>()
        where TAttribute : Attribute
    {
        foreach (var info in _lazyPlugins.Value)
        {
            if (!info.HasAttribute<TAttribute>())
                continue;

            yield return info.Factory();
        }
    }

    /// <inheritdoc />
    public IEnumerable<object> CreatePluginsWithAttributeFromAssemblies<TAttribute>(
        IEnumerable<Assembly> assemblies)
        where TAttribute : Attribute
    {
        var assemblyList = assemblies as IReadOnlyCollection<Assembly> ?? assemblies.ToArray();
        if (_allowAllWhenAssembliesEmpty && assemblyList.Count == 0)
        {
            foreach (var p in CreatePluginsWithAttribute<TAttribute>())
            {
                yield return p;
            }

            yield break;
        }

        var assemblySet = assemblyList
            .Select(a => a.GetName().Name)
            .Where(n => n is not null)
            .ToHashSet(StringComparer.Ordinal)!;

        foreach (var info in _lazyPlugins.Value)
        {
            // Check if this plugin is from one of the specified assemblies
            var pluginAssemblyName = info.PluginType.Assembly.GetName().Name;
            if (pluginAssemblyName is null || !assemblySet.Contains(pluginAssemblyName))
                continue;

            // Use pre-computed attribute info - no reflection needed
            if (!info.HasAttribute<TAttribute>())
                continue;

            yield return info.Factory();
        }
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
    public IEnumerable<TPlugin> CreatePlugins<TPlugin, TAttribute>()
        where TPlugin : class
        where TAttribute : Attribute
    {
        var pluginType = typeof(TPlugin);

        foreach (var info in _lazyPlugins.Value)
        {
            if (!pluginType.IsAssignableFrom(info.PluginType))
                continue;

            if (!info.HasAttribute<TAttribute>())
                continue;

            yield return (TPlugin)info.Factory();
        }
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
            foreach (var p in CreatePlugins<TPlugin, TAttribute>())
            {
                yield return p;
            }

            yield break;
        }

        var assemblySet = assemblyList
            .Select(a => a.GetName().Name)
            .Where(n => n is not null)
            .ToHashSet(StringComparer.Ordinal)!;

        var pluginType = typeof(TPlugin);
        foreach (var info in _lazyPlugins.Value)
        {
            // Check if this plugin is from one of the specified assemblies
            var pluginAssemblyName = info.PluginType.Assembly.GetName().Name;
            if (pluginAssemblyName is null || !assemblySet.Contains(pluginAssemblyName))
                continue;

            if (!pluginType.IsAssignableFrom(info.PluginType))
                continue;

            // Use pre-computed attribute info - no reflection needed
            if (!info.HasAttribute<TAttribute>())
                continue;

            yield return (TPlugin)info.Factory();
        }
    }
}
