namespace NexusLabs.Needlr.Catalog;

/// <summary>
/// Represents a hosted service registration discovered at compile time.
/// </summary>
/// <param name="TypeName">The fully qualified name of the hosted service type.</param>
/// <param name="ShortTypeName">The short name of the hosted service type (without namespace).</param>
/// <param name="AssemblyName">The assembly name where this type is defined.</param>
/// <param name="ConstructorParameters">Constructor parameter type names.</param>
/// <param name="SourceFilePath">Source file path where the type is defined, if available.</param>
public sealed record HostedServiceCatalogEntry(
    string TypeName,
    string ShortTypeName,
    string AssemblyName,
    IReadOnlyList<ConstructorParameterEntry> ConstructorParameters,
    string? SourceFilePath = null);
