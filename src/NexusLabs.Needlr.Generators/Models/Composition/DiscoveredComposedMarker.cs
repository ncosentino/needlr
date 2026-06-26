using Microsoft.CodeAnalysis;

namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// A discovered <c>[RegisterClosedOverImplementationsOf]</c> marker on an open generic composition type,
/// captured during type collection for later expansion into closed registrations.
/// </summary>
internal readonly struct DiscoveredComposedMarker
{
    public DiscoveredComposedMarker(
        INamedTypeSymbol compositionType,
        INamedTypeSymbol sourceOpenGenericInterface,
        INamedTypeSymbol? asServiceType,
        GeneratorLifetime lifetime,
        string assemblyName,
        string? sourceFilePath)
    {
        CompositionType = compositionType;
        SourceOpenGenericInterface = sourceOpenGenericInterface;
        AsServiceType = asServiceType;
        Lifetime = lifetime;
        AssemblyName = assemblyName;
        SourceFilePath = sourceFilePath;
    }

    /// <summary>The open generic composition type carrying the attribute (e.g., FooCore{TData}).</summary>
    public INamedTypeSymbol CompositionType { get; }

    /// <summary>The open generic interface whose closed implementations drive registration (e.g., IFooDefinition{}).</summary>
    public INamedTypeSymbol SourceOpenGenericInterface { get; }

    /// <summary>The facade service type each closed composition is registered as, or null if unspecified.</summary>
    public INamedTypeSymbol? AsServiceType { get; }

    /// <summary>The lifetime each closed registration is given.</summary>
    public GeneratorLifetime Lifetime { get; }

    /// <summary>The name of the assembly that declared the composition type.</summary>
    public string AssemblyName { get; }

    /// <summary>The source file path of the composition type, if available.</summary>
    public string? SourceFilePath { get; }
}
