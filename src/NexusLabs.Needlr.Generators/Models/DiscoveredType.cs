using System;
using System.Linq;

namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Information about a discovered injectable type.
/// </summary>
internal readonly struct DiscoveredType
{
    public DiscoveredType(string typeName, string[] interfaceNames, string assemblyName, GeneratorLifetime lifetime, TypeDiscoveryHelper.ConstructorParameterInfo[] constructorParameters, string[] serviceKeys, string? sourceFilePath = null, int sourceLine = 0, bool isDisposable = false, InterfaceInfo[]? interfaceInfos = null)
    {
        TypeName = typeName;
        InterfaceNames = interfaceNames;
        AssemblyName = assemblyName;
        Lifetime = lifetime;
        ConstructorParameters = constructorParameters;
        ServiceKeys = serviceKeys;
        SourceFilePath = sourceFilePath;
        SourceLine = sourceLine;
        IsDisposable = isDisposable;
        InterfaceInfos = interfaceInfos ?? Array.Empty<InterfaceInfo>();
    }

    public string TypeName { get; }
    public string[] InterfaceNames { get; }
    /// <summary>
    /// Detailed interface information including source locations.
    /// </summary>
    public InterfaceInfo[] InterfaceInfos { get; }
    public string AssemblyName { get; }
    public GeneratorLifetime Lifetime { get; }
    public TypeDiscoveryHelper.ConstructorParameterInfo[] ConstructorParameters { get; }
    /// <summary>
    /// Service keys from [Keyed] attributes on this type.
    /// </summary>
    public string[] ServiceKeys { get; }
    public string? SourceFilePath { get; }
    /// <summary>
    /// The 1-based line number where this type is declared.
    /// </summary>
    public int SourceLine { get; }
    /// <summary>
    /// True if this type implements IDisposable or IAsyncDisposable.
    /// </summary>
    public bool IsDisposable { get; }

    /// <summary>
    /// Gets the constructor parameter types (for backward compatibility with existing code paths).
    /// </summary>
    public string[] ConstructorParameterTypes => ConstructorParameters.Select(p => p.TypeName).ToArray();

    /// <summary>
    /// True if any constructor parameters are keyed services.
    /// </summary>
    public bool HasKeyedParameters => ConstructorParameters.Any(p => p.IsKeyed);

    /// <summary>
    /// True if this type has [Keyed] attributes for keyed registration.
    /// </summary>
    public bool IsKeyed => ServiceKeys.Length > 0;
}
