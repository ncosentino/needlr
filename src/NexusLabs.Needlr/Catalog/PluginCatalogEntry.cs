namespace NexusLabs.Needlr.Catalog;

/// <summary>
/// Represents a plugin discovered at compile time.
/// </summary>
/// <param name="TypeName">The fully qualified name of the plugin type.</param>
/// <param name="ShortTypeName">The short name of the plugin type (without namespace).</param>
/// <param name="Interfaces">The plugin interfaces this type implements.</param>
/// <param name="AssemblyName">The assembly name where this type is defined.</param>
/// <param name="Order">The execution order of this plugin.</param>
/// <param name="SourceFilePath">Source file path where the type is defined, if available.</param>
public sealed record PluginCatalogEntry(
    string TypeName,
    string ShortTypeName,
    IReadOnlyList<string> Interfaces,
    string AssemblyName,
    int Order = 0,
    string? SourceFilePath = null);
