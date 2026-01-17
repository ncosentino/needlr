using System;
using System.Collections.Generic;

namespace NexusLabs.Needlr.Generators;

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
    {
        Type = type;
        Interfaces = interfaces;
    }

    /// <summary>
    /// Gets the concrete implementation type.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Gets the interfaces implemented by the type that should be registered.
    /// </summary>
    public IReadOnlyList<Type> Interfaces { get; }
}
