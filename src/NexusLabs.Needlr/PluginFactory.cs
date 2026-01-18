using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NexusLabs.Needlr;

/// <summary>
/// The plugin factory provides methods to scan assemblies for types that implement specified interfaces,
/// are decorated with particular attributes, or meet both criteria. All discovered types are instantiated
/// using their parameterless constructors.
/// </summary>
/// <remarks>
/// This factory uses runtime reflection and is not compatible with NativeAOT or trimming.
/// For AOT scenarios, use the GeneratedPluginFactory from NexusLabs.Needlr.Injection
/// with the Needlr source generator instead.
/// </remarks>
[RequiresUnreferencedCode("PluginFactory uses reflection to discover and instantiate plugins. Use GeneratedPluginFactory for AOT scenarios.")]
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
        return GetTypesFromAssemblies(assemblies)
            .Where(t => IsValidPluginType(t) && t.IsAssignableTo(typeof(TPlugin)))
            .Select(CreateInstance<TPlugin>);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Only non-abstract, non-generic classes decorated with <typeparamref name="TAttribute"/> are instantiated.
    /// Types that cannot be loaded from an assembly are skipped. The attribute is searched in the type hierarchy
    /// using inheritance.
    /// </remarks>
    public IEnumerable<object> CreatePluginsWithAttributeFromAssemblies<TAttribute>(
        IEnumerable<Assembly> assemblies)
        where TAttribute : Attribute
    {
        return GetTypesFromAssemblies(assemblies)
            .Where(t => 
                IsValidPluginType(t) && 
                HasAttribute<TAttribute>(t))
            .Select(CreateInstance);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Only non-abstract, non-generic classes assignable to <typeparamref name="TPlugin"/> 
    /// and decorated with <typeparamref name="TAttribute"/> are instantiated.
    /// Types that cannot be loaded from an assembly are skipped. The attribute is 
    /// searched in the type hierarchy using inheritance.
    /// </remarks>
    public IEnumerable<TPlugin> CreatePluginsFromAssemblies<TPlugin, TAttribute>(
        IEnumerable<Assembly> assemblies)
        where TPlugin : class
        where TAttribute : Attribute
    {
        return GetTypesFromAssemblies(assemblies)
            .Where(t => 
                IsValidPluginType(t) && 
                t.IsAssignableTo(typeof(TPlugin)) && 
                HasAttribute<TAttribute>(t))
            .Select(CreateInstance<TPlugin>);
    }

    /// <summary>
    /// Gets all types from the provided assemblies, handling assembly loading exceptions gracefully.
    /// </summary>
    /// <param name="assemblies">The assemblies to extract types from.</param>
    /// <returns>An enumerable of types that were successfully loaded.</returns>
    private static IEnumerable<Type> GetTypesFromAssemblies(IEnumerable<Assembly> assemblies)
    {
        return assemblies
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    return ex.Types.OfType<Type>();
                }
                catch
                {
                    return [];
                }
            });
    }

    /// <summary>
    /// Determines if a type is a valid plugin type (non-abstract, non-generic class with parameterless constructor).
    /// </summary>
    /// <param name="type">The type to validate.</param>
    /// <returns>True if the type is a valid plugin type; otherwise, false.</returns>
    private static bool IsValidPluginType(Type type)
    {
        return type.IsClass && 
               !type.IsAbstract && 
               !type.IsGenericTypeDefinition &&
               HasParameterlessConstructor(type);
    }

    /// <summary>
    /// Determines if a type has a parameterless constructor.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type has a parameterless constructor; otherwise, false.</returns>
    private static bool HasParameterlessConstructor(Type type)
    {
        return type.GetConstructor(Type.EmptyTypes) is not null;
    }

    /// <summary>
    /// Determines if a type has the specified attribute in its type hierarchy.
    /// </summary>
    /// <typeparam name="TAttribute">The attribute type to search for.</typeparam>
    /// <param name="type">The type to check for the attribute.</param>
    /// <returns>True if the type has the attribute; otherwise, false.</returns>
    private static bool HasAttribute<TAttribute>(Type type) where TAttribute : Attribute
    {
        return type.GetCustomAttribute<TAttribute>(inherit: true) is not null;
    }

    /// <summary>
    /// Creates an instance of the specified type.
    /// </summary>
    /// <param name="type">The type to instantiate.</param>
    /// <returns>An instance of the type.</returns>
    private static object CreateInstance(Type type)
    {
        return Activator.CreateInstance(type)!;
    }

    /// <summary>
    /// Creates an instance of the specified type and casts it to the target type.
    /// </summary>
    /// <typeparam name="T">The target type to cast to.</typeparam>
    /// <param name="type">The type to instantiate.</param>
    /// <returns>An instance of the type cast to the target type.</returns>
    private static T CreateInstance<T>(Type type) where T : class
    {
        return (T)Activator.CreateInstance(type)!;
    }
}
