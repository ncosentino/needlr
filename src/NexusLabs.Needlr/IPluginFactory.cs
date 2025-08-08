using System.Reflection;

namespace NexusLabs.Needlr;

public interface IPluginFactory
{
    /// <summary>
    /// Creates instances of plugins of type <typeparamref name="TPlugin"/> from the provided assemblies.
    /// </summary>
    /// <typeparam name="TPlugin">The plugin interface or base type to search for.</typeparam>
    /// <param name="assemblies">A collection of assemblies to scan for plugin types.</param>
    /// <returns>An enumerable of instantiated plugins implementing <typeparamref name="TPlugin"/>.</returns>
    IEnumerable<TPlugin> CreatePluginsFromAssemblies<TPlugin>(
        IEnumerable<Assembly> assemblies) 
        where TPlugin : class;
}
