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
        : this(type, interfaces, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="InjectableTypeInfo"/>.
    /// </summary>
    /// <param name="type">The concrete implementation type.</param>
    /// <param name="interfaces">The interfaces implemented by the type that should be registered.</param>
    /// <param name="lifetime">The pre-computed service lifetime, or null if not determined.</param>
    public InjectableTypeInfo(Type type, IReadOnlyList<Type> interfaces, InjectableLifetime? lifetime)
    {
        Type = type;
        Interfaces = interfaces;
        Lifetime = lifetime;
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
    {
        PluginType = pluginType;
        PluginInterfaces = pluginInterfaces;
        Factory = factory;
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
}
