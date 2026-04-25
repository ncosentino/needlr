namespace RfcPipelineApp.Core.Validators;

/// <summary>
/// Validates that the RFC draft contains all required structural sections.
/// </summary>
internal static class StructureValidator
{
    private static readonly string[] RequiredSections =
    [
        "Problem Statement",
        "Background",
        "Proposed Solution",
        "Technical Design",
        "Alternatives Considered",
        "Trade-offs",
        "Migration Plan",
        "Rollback Strategy",
    ];

    private const int MinimumWordCount = 500;

    /// <summary>
    /// Validates the RFC draft content for structural completeness.
    /// </summary>
    /// <param name="content">The full text of the RFC draft.</param>
    /// <returns>
    /// <see langword="null"/> if the document passes validation;
    /// otherwise, a description of the structural deficiency.
    /// </returns>
    internal static string? Validate(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "RFC draft is empty.";
        }

        var missingSections = RequiredSections
            .Where(section => !content.Contains(section, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (missingSections.Count > 0)
        {
            return $"RFC is missing required sections: {string.Join(", ", missingSections)}";
        }

        var wordCount = content.Split(
            (char[])[' ', '\n', '\r', '\t'],
            StringSplitOptions.RemoveEmptyEntries).Length;

        if (wordCount < MinimumWordCount)
        {
            return $"RFC has only {wordCount} words; minimum is {MinimumWordCount}.";
        }

        return null;
    }
}
