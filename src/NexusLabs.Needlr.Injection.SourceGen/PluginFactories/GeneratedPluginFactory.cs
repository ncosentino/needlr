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
    private readonly Func<IReadOnlyList<PluginTypeInfo>> _pluginProvider;
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
        _pluginProvider = pluginProvider;
        _allowAllWhenAssembliesEmpty = allowAllWhenAssembliesEmpty;
    }

    /// <inheritdoc />
    public IEnumerable<TPlugin> CreatePluginsFromAssemblies<TPlugin>(
        IEnumerable<Assembly> assemblies)
        where TPlugin : class
    {
        var pluginType = typeof(TPlugin);

        var assemblyList = assemblies as IReadOnlyCollection<Assembly> ?? assemblies.ToArray();
        if (_allowAllWhenAssembliesEmpty && assemblyList.Count == 0)
        {
            foreach (var info in _pluginProvider())
            {
                if (!pluginType.IsAssignableFrom(info.PluginType))
                    continue;

                yield return (TPlugin)info.Factory();
            }

            yield break;
        }

        var assemblySet = assemblyList
            .Select(a => a.GetName().Name)
            .Where(n => n is not null)
            .ToHashSet(StringComparer.Ordinal)!;

        foreach (var info in _pluginProvider())
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

    /// <inheritdoc />
    public IEnumerable<object> CreatePluginsWithAttributeFromAssemblies<TAttribute>(
        IEnumerable<Assembly> assemblies)
        where TAttribute : Attribute
    {
        var assemblyList = assemblies as IReadOnlyCollection<Assembly> ?? assemblies.ToArray();
        if (_allowAllWhenAssembliesEmpty && assemblyList.Count == 0)
        {
            foreach (var info in _pluginProvider())
            {
                if (!info.HasAttribute<TAttribute>())
                    continue;

                yield return info.Factory();
            }

            yield break;
        }

        var assemblySet = assemblyList
            .Select(a => a.GetName().Name)
            .Where(n => n is not null)
            .ToHashSet(StringComparer.Ordinal)!;

        foreach (var info in _pluginProvider())
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

    /// <inheritdoc />
    public IEnumerable<TPlugin> CreatePluginsFromAssemblies<TPlugin, TAttribute>(
        IEnumerable<Assembly> assemblies)
        where TPlugin : class
        where TAttribute : Attribute
    {
        var pluginType = typeof(TPlugin);

        var assemblyList = assemblies as IReadOnlyCollection<Assembly> ?? assemblies.ToArray();
        if (_allowAllWhenAssembliesEmpty && assemblyList.Count == 0)
        {
            foreach (var info in _pluginProvider())
            {
                if (!pluginType.IsAssignableFrom(info.PluginType))
                    continue;

                if (!info.HasAttribute<TAttribute>())
                    continue;

                yield return (TPlugin)info.Factory();
            }

            yield break;
        }

        var assemblySet = assemblyList
            .Select(a => a.GetName().Name)
            .Where(n => n is not null)
            .ToHashSet(StringComparer.Ordinal)!;

        foreach (var info in _pluginProvider())
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
