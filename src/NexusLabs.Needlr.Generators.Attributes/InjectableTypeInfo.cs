using System;
using System.Collections.Generic;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Specifies the lifetime of a service in the dependency injection container.
/// </summary>
/// <remarks>
/// This enum mirrors <c>Microsoft.Extensions.DependencyInjection.ServiceLifetime</c>
/// to avoid adding a dependency on that package in the attributes assembly.
/// </remarks>
public enum InjectableLifetime
{
    /// <summary>
    /// A single instance is created and shared across all requests.
    /// </summary>
    Singleton = 0,

    /// <summary>
    /// A new instance is created for each scope (e.g., each HTTP request).
    /// </summary>
    Scoped = 1,

    /// <summary>
    /// A new instance is created each time the service is requested.
    /// </summary>
    Transient = 2
}

/// <summary>
/// Represents metadata about an injectable type discovered at compile time.
/// </summary>
/// <remarks>
/// This struct is used by the generated TypeRegistry to provide type
/// information without requiring reflection at runtime.
/// </remarks>
public readonly struct InjectableTypeInfo
{
    /// <summary>
    /// Initializes a new instance of <see cref="InjectableTypeInfo"/>.
    /// </summary>
    /// <param name="type">The concrete implementation type.</param>
    /// <param name="interfaces">The interfaces implemented by the type that should be registered.</param>
    public InjectableTypeInfo(Type type, IReadOnlyList<Type> interfaces)
        : this(type, interfaces, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="InjectableTypeInfo"/>.
    /// </summary>
    /// <param name="type">The concrete implementation type.</param>
    /// <param name="interfaces">The interfaces implemented by the type that should be registered.</param>
    /// <param name="lifetime">The pre-computed service lifetime, or null if not determined.</param>
    public InjectableTypeInfo(Type type, IReadOnlyList<Type> interfaces, InjectableLifetime? lifetime)
        : this(type, interfaces, lifetime, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="InjectableTypeInfo"/>.
    /// </summary>
    /// <param name="type">The concrete implementation type.</param>
    /// <param name="interfaces">The interfaces implemented by the type that should be registered.</param>
    /// <param name="lifetime">The pre-computed service lifetime, or null if not determined.</param>
    /// <param name="factory">
    /// A factory delegate that creates an instance of the type using the service provider.
    /// When provided, enables AOT-compatible instantiation without runtime reflection.
    /// </param>
    public InjectableTypeInfo(Type type, IReadOnlyList<Type> interfaces, InjectableLifetime? lifetime, Func<IServiceProvider, object>? factory)
    {
        Type = type;
        Interfaces = interfaces;
        Lifetime = lifetime;
        Factory = factory;
    }

    /// <summary>
    /// Gets the concrete implementation type.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Gets the interfaces implemented by the type that should be registered.
    /// </summary>
    public IReadOnlyList<Type> Interfaces { get; }

    /// <summary>
    /// Gets the pre-computed service lifetime for this type, or null if
    /// the lifetime should be determined at runtime by an <c>ITypeFilterer</c>.
    /// </summary>
    /// <remarks>
    /// When this value is set, the type registrar can skip runtime reflection
    /// for constructor analysis, improving startup performance and enabling
    /// AOT compilation scenarios.
    /// </remarks>
    public InjectableLifetime? Lifetime { get; }

    /// <summary>
    /// Gets a factory delegate that creates an instance of the type using the service provider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When this delegate is provided, the type registrar can use it to create instances
    /// without relying on <c>Activator.CreateInstance</c> or reflection-based constructor
    /// invocation. This is essential for NativeAOT scenarios where reflection may be disabled.
    /// </para>
    /// <para>
    /// The factory receives an <see cref="IServiceProvider"/> and should resolve all
    /// constructor dependencies from it before creating the instance.
    /// </para>
    /// </remarks>
    public Func<IServiceProvider, object>? Factory { get; }
}

/// <summary>
/// Represents metadata about a plugin type discovered at compile time.
/// </summary>
/// <remarks>
/// This struct is used by the generated TypeRegistry to provide plugin
/// factory methods without requiring reflection or Activator.CreateInstance at runtime.
/// </remarks>
public readonly struct PluginTypeInfo
{
    /// <summary>
    /// Initializes a new instance of <see cref="PluginTypeInfo"/>.
    /// </summary>
    /// <param name="pluginType">The concrete plugin type.</param>
    /// <param name="pluginInterfaces">The plugin interfaces implemented by the type.</param>
    /// <param name="factory">A factory delegate that creates an instance of the plugin.</param>
    public PluginTypeInfo(Type pluginType, IReadOnlyList<Type> pluginInterfaces, Func<object> factory)
        : this(pluginType, pluginInterfaces, factory, Array.Empty<Type>())
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="PluginTypeInfo"/>.
    /// </summary>
    /// <param name="pluginType">The concrete plugin type.</param>
    /// <param name="pluginInterfaces">The plugin interfaces implemented by the type.</param>
    /// <param name="factory">A factory delegate that creates an instance of the plugin.</param>
    /// <param name="attributes">The attribute types applied to the plugin type.</param>
    public PluginTypeInfo(Type pluginType, IReadOnlyList<Type> pluginInterfaces, Func<object> factory, IReadOnlyList<Type> attributes)
    {
        PluginType = pluginType;
        PluginInterfaces = pluginInterfaces;
        Factory = factory;
        Attributes = attributes;
    }

    /// <summary>
    /// Gets the concrete plugin type.
    /// </summary>
    public Type PluginType { get; }

    /// <summary>
    /// Gets the plugin interfaces implemented by the type.
    /// </summary>
    public IReadOnlyList<Type> PluginInterfaces { get; }

    /// <summary>
    /// Gets a factory delegate that creates an instance of the plugin
    /// without using Activator.CreateInstance.
    /// </summary>
    public Func<object> Factory { get; }

    /// <summary>
    /// Gets the attribute types applied to the plugin type.
    /// </summary>
    /// <remarks>
    /// This enables attribute-based plugin filtering without requiring
    /// reflection via <c>GetCustomAttribute</c> at runtime.
    /// </remarks>
    public IReadOnlyList<Type> Attributes { get; }

    /// <summary>
    /// Checks if the plugin has an attribute of the specified type.
    /// </summary>
    /// <typeparam name="TAttribute">The attribute type to check for.</typeparam>
    /// <returns>True if the plugin has the attribute; otherwise, false.</returns>
    public bool HasAttribute<TAttribute>() where TAttribute : Attribute
    {
        var attributeType = typeof(TAttribute);
        for (int i = 0; i < Attributes.Count; i++)
        {
            if (attributeType.IsAssignableFrom(Attributes[i]))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if the plugin has an attribute of the specified type.
    /// </summary>
    /// <param name="attributeType">The attribute type to check for.</param>
    /// <returns>True if the plugin has the attribute; otherwise, false.</returns>
    public bool HasAttribute(Type attributeType)
    {
        for (int i = 0; i < Attributes.Count; i++)
        {
            if (attributeType.IsAssignableFrom(Attributes[i]))
                return true;
        }
        return false;
    }
}
