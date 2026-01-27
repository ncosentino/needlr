namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Simplified type information for diagnostic output (DependencyGraph, LifetimeSummary, RegistrationIndex).
/// Used for cross-assembly type aggregation where we don't need full symbol information.
/// </summary>
internal readonly struct DiagnosticTypeInfo
{
    public DiagnosticTypeInfo(
        string fullName,
        string shortName,
        GeneratorLifetime lifetime,
        string[] interfaces,
        string[] dependencies,
        bool isDecorator,
        bool isPlugin,
        bool hasFactory,
        string? keyedValue,
        bool isInterceptor = false,
        bool hasInterceptorProxy = false)
    {
        FullName = fullName;
        ShortName = shortName;
        Lifetime = lifetime;
        Interfaces = interfaces;
        Dependencies = dependencies;
        IsDecorator = isDecorator;
        IsPlugin = isPlugin;
        HasFactory = hasFactory;
        KeyedValue = keyedValue;
        IsInterceptor = isInterceptor;
        HasInterceptorProxy = hasInterceptorProxy;
    }

    /// <summary>Fully qualified type name including namespace.</summary>
    public string FullName { get; }
    
    /// <summary>Short type name without namespace.</summary>
    public string ShortName { get; }
    
    /// <summary>Service lifetime (Singleton, Scoped, Transient).</summary>
    public GeneratorLifetime Lifetime { get; }
    
    /// <summary>Interfaces implemented by this type.</summary>
    public string[] Interfaces { get; }
    
    /// <summary>Constructor dependencies (type names).</summary>
    public string[] Dependencies { get; }
    
    /// <summary>True if this is a decorator type (has [DecoratorFor] or [OpenDecoratorFor]).</summary>
    public bool IsDecorator { get; }
    
    /// <summary>True if this is a Needlr plugin type.</summary>
    public bool IsPlugin { get; }
    
    /// <summary>True if this type has [GenerateFactory] attribute.</summary>
    public bool HasFactory { get; }
    
    /// <summary>Keyed service value if this type has [Keyed] attribute.</summary>
    public string? KeyedValue { get; }
    
    /// <summary>True if this is an interceptor type (has [Intercept] or is referenced by one).</summary>
    public bool IsInterceptor { get; }
    
    /// <summary>True if this type has an interceptor proxy generated for it.</summary>
    public bool HasInterceptorProxy { get; }
}
