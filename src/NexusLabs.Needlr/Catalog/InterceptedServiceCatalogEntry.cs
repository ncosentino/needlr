namespace NexusLabs.Needlr.Catalog;

/// <summary>
/// Represents an intercepted service discovered at compile time.
/// </summary>
/// <param name="TypeName">The fully qualified name of the intercepted service type.</param>
/// <param name="ShortTypeName">The short name of the intercepted service type (without namespace).</param>
/// <param name="AssemblyName">The assembly name where this type is defined.</param>
/// <param name="Lifetime">The service lifetime (Singleton, Scoped, Transient).</param>
/// <param name="Interfaces">The interfaces this type is registered as.</param>
/// <param name="InterceptorTypeNames">The interceptor type names applied to this service, in order.</param>
/// <param name="SourceFilePath">Source file path where the type is defined, if available.</param>
public sealed record InterceptedServiceCatalogEntry(
    string TypeName,
    string ShortTypeName,
    string AssemblyName,
    ServiceCatalogLifetime Lifetime,
    IReadOnlyList<string> Interfaces,
    IReadOnlyList<string> InterceptorTypeNames,
    string? SourceFilePath = null);
