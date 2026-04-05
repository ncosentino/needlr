namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Identifies the kind of DataAnnotation attribute for source-generated validation.
/// </summary>
internal enum DataAnnotationKind
{
    Required,
    Range,
    StringLength,
    MinLength,
    MaxLength,
    RegularExpression,
    EmailAddress,
    Phone,
    Url,
    Unsupported
}
