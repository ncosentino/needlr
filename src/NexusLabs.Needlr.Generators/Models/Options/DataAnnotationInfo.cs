namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Information about a DataAnnotation attribute on an options property.
/// </summary>
internal readonly struct DataAnnotationInfo
{
    public DataAnnotationInfo(
        DataAnnotationKind kind,
        string? errorMessage = null,
        object? minimum = null,
        object? maximum = null,
        string? pattern = null,
        int? minimumLength = null)
    {
        Kind = kind;
        ErrorMessage = errorMessage;
        Minimum = minimum;
        Maximum = maximum;
        Pattern = pattern;
        MinimumLength = minimumLength;
    }

    /// <summary>The kind of DataAnnotation attribute.</summary>
    public DataAnnotationKind Kind { get; }

    /// <summary>Custom error message if specified.</summary>
    public string? ErrorMessage { get; }

    /// <summary>Minimum value for Range attribute.</summary>
    public object? Minimum { get; }

    /// <summary>Maximum value for Range/StringLength/MaxLength attributes.</summary>
    public object? Maximum { get; }

    /// <summary>Pattern for RegularExpression attribute.</summary>
    public string? Pattern { get; }

    /// <summary>Minimum length for StringLength/MinLength attributes.</summary>
    public int? MinimumLength { get; }
}
