using System.Reflection;

namespace NexusLabs.Needlr;

/// <summary>
/// Defines a factory for discovering and creating plugin instances from assemblies.
/// </summary>
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

    /// <summary>
    /// Creates instances of plugins from the provided assemblies that are decorated with the specified attribute.
    /// </summary>
    /// <typeparam name="TAttribute">The attribute type to search for in the type hierarchy.</typeparam>
    /// <param name="assemblies">A collection of assemblies to scan for plugin types.</param>
    /// <returns>An enumerable of instantiated plugins decorated with <typeparamref name="TAttribute"/>.</returns>
    IEnumerable<object> CreatePluginsWithAttributeFromAssemblies<TAttribute>(
        IEnumerable<Assembly> assemblies) 
        where TAttribute : Attribute;

    /// <summary>
    /// Creates instances of plugins of type <typeparamref name="TPlugin"/> from the provided assemblies 
    /// that are also decorated with the specified attribute.
    /// </summary>
    /// <typeparam name="TPlugin">The plugin interface or base type to search for.</typeparam>
    /// <typeparam name="TAttribute">The attribute type to search for in the type hierarchy.</typeparam>
    /// <param name="assemblies">A collection of assemblies to scan for plugin types.</param>
    /// <returns>
    /// An enumerable of instantiated plugins implementing <typeparamref name="TPlugin"/> and 
    /// decorated with <typeparamref name="TAttribute"/>.
    /// </returns>
    IEnumerable<TPlugin> CreatePluginsFromAssemblies<TPlugin, TAttribute>(
        IEnumerable<Assembly> assemblies) 
        where TPlugin : class
        where TAttribute : Attribute;
}
