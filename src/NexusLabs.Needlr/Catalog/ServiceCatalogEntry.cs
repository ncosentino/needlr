namespace NexusLabs.Needlr.Catalog;

/// <summary>
/// Represents an interface with its source location information.
/// </summary>
/// <param name="FullName">The fully qualified name of the interface.</param>
/// <param name="SourceFilePath">Source file path where the interface is defined, if available.</param>
/// <param name="SourceLine">1-based line number where the interface is defined, or 0 if unknown.</param>
public sealed record InterfaceEntry(
    string FullName,
    string? SourceFilePath = null,
    int SourceLine = 0);

/// <summary>
/// Represents a service registration discovered at compile time.
/// </summary>
/// <param name="TypeName">The fully qualified name of the implementation type.</param>
/// <param name="ShortTypeName">The short name of the implementation type (without namespace).</param>
/// <param name="AssemblyName">The assembly name where this type is defined.</param>
/// <param name="Lifetime">The service lifetime (Singleton, Scoped, Transient).</param>
/// <param name="Interfaces">The interfaces this type is registered as.</param>
/// <param name="ConstructorParameters">Constructor parameter type names.</param>
/// <param name="ServiceKeys">Service keys if registered as keyed service.</param>
/// <param name="SourceFilePath">Source file path where the type is defined, if available.</param>
/// <param name="SourceLine">1-based line number where the type is defined, or 0 if unknown.</param>
/// <param name="InterfaceEntries">Detailed interface information including source locations.</param>
public sealed record ServiceCatalogEntry(
    string TypeName,
    string ShortTypeName,
    string AssemblyName,
    ServiceCatalogLifetime Lifetime,
    IReadOnlyList<string> Interfaces,
    IReadOnlyList<ConstructorParameterEntry> ConstructorParameters,
    IReadOnlyList<string> ServiceKeys,
    string? SourceFilePath = null,
    int SourceLine = 0,
    IReadOnlyList<InterfaceEntry>? InterfaceEntries = null);

/// <summary>
/// Represents a constructor parameter.
/// </summary>
/// <param name="Name">The parameter name.</param>
/// <param name="TypeName">The fully qualified type name of the parameter.</param>
/// <param name="IsKeyed">True if this is a keyed service parameter.</param>
/// <param name="ServiceKey">The service key if this is a keyed parameter.</param>
public sealed record ConstructorParameterEntry(
    string Name,
    string TypeName,
    bool IsKeyed = false,
    string? ServiceKey = null);

/// <summary>
/// Service lifetime as discovered at compile time.
/// </summary>
public enum ServiceCatalogLifetime
{
    Singleton,
    Scoped,
    Transient
}
