namespace NexusLabs.Needlr.Catalog;

/// <summary>
/// Represents an options/configuration binding discovered at compile time.
/// </summary>
/// <param name="TypeName">The fully qualified name of the options type.</param>
/// <param name="ShortTypeName">The short name of the options type (without namespace).</param>
/// <param name="SectionName">The configuration section name this options type binds to.</param>
/// <param name="AssemblyName">The assembly name where this type is defined.</param>
/// <param name="Name">The named options name, or null for default options.</param>
/// <param name="ValidateOnStart">True if validation is performed on application start.</param>
/// <param name="HasValidator">True if this options type has a validation method.</param>
/// <param name="HasDataAnnotations">True if this options type has DataAnnotation validation attributes.</param>
/// <param name="SourceFilePath">Source file path where the type is defined, if available.</param>
public sealed record OptionsCatalogEntry(
    string TypeName,
    string ShortTypeName,
    string SectionName,
    string AssemblyName,
    string? Name = null,
    bool ValidateOnStart = false,
    bool HasValidator = false,
    bool HasDataAnnotations = false,
    string? SourceFilePath = null);
