using System.Reflection;

namespace NexusLabs.Needlr.Injection.Tests.Syringes;

/// <summary>
/// Plugin factory that never produces plugins.
/// </summary>
[DoNotAutoRegister]
public sealed class NoOpPluginFactory : IPluginFactory
{
    /// <inheritdoc />
    public IEnumerable<TPlugin> CreatePluginsFromAssemblies<TPlugin>(IEnumerable<Assembly> assemblies)
        where TPlugin : class
    {
        return [];
    }

    /// <inheritdoc />
    public IEnumerable<object> CreatePluginsWithAttributeFromAssemblies<TAttribute>(IEnumerable<Assembly> assemblies)
        where TAttribute : Attribute
    {
        return [];
    }

    /// <inheritdoc />
    public IEnumerable<TPlugin> CreatePluginsFromAssemblies<TPlugin, TAttribute>(IEnumerable<Assembly> assemblies)
        where TPlugin : class
        where TAttribute : Attribute
    {
        return [];
    }
}
