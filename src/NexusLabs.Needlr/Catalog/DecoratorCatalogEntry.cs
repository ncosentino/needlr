namespace NexusLabs.Needlr.Catalog;

/// <summary>
/// Represents a decorator registration discovered at compile time.
/// </summary>
/// <param name="DecoratorTypeName">The fully qualified name of the decorator type.</param>
/// <param name="ShortDecoratorTypeName">The short name of the decorator type (without namespace).</param>
/// <param name="ServiceTypeName">The fully qualified name of the service type being decorated.</param>
/// <param name="Order">The order in which this decorator is applied (lower = closer to original service).</param>
/// <param name="AssemblyName">The assembly name where this decorator is defined.</param>
/// <param name="SourceFilePath">Source file path where the decorator is defined, if available.</param>
public sealed record DecoratorCatalogEntry(
    string DecoratorTypeName,
    string ShortDecoratorTypeName,
    string ServiceTypeName,
    int Order,
    string AssemblyName,
    string? SourceFilePath = null);
