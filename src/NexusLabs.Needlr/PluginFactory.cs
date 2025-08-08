using System.Reflection;

namespace NexusLabs.Needlr;

/// <summary>
/// Factory for creating plugin instances from assemblies.
/// </summary>
public sealed class PluginFactory : IPluginFactory
{
    /// <inheritdoc />
    /// <remarks>
    /// Only non-abstract, non-generic classes assignable to <typeparamref name="TPlugin"/> are instantiated.
    /// Types that cannot be loaded from an assembly are skipped.
    /// </remarks>
    public IEnumerable<TPlugin> CreatePluginsFromAssemblies<TPlugin>(
        IEnumerable<Assembly> assemblies)
        where TPlugin : class
    {
        foreach (var t in assemblies
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch
                {
                    return [];
                }
            })
            .Where(t =>
                t.IsClass &&
                !t.IsAbstract &&
                t.IsAssignableTo(typeof(TPlugin))))
        {
            var plugin = Activator.CreateInstance(t) as TPlugin;
            yield return plugin!;
        }
    }
}
