namespace RfcPipelineApp.Core.Validators;

/// <summary>
/// Validates that every required section in the RFC draft has substantive content.
/// </summary>
internal static class CompletenessValidator
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

    private const int MinSectionWordCount = 30;

    /// <summary>
    /// Validates that all required sections contain non-trivial content.
    /// </summary>
    /// <param name="content">The full text of the RFC draft.</param>
    /// <returns>
    /// <see langword="null"/> if all sections have substantive content;
    /// otherwise, a description of which sections are too thin.
    /// </returns>
    internal static string? Validate(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "RFC draft is empty.";
        }

        var thinSections = new List<string>();

        for (var i = 0; i < RequiredSections.Length; i++)
        {
            var section = RequiredSections[i];
            var sectionContent = ExtractSectionContent(content, section);

            if (sectionContent is null)
            {
                thinSections.Add($"{section} (missing)");
                continue;
            }

            var wordCount = sectionContent.Split(
                (char[])[' ', '\n', '\r', '\t'],
                StringSplitOptions.RemoveEmptyEntries).Length;

            if (wordCount < MinSectionWordCount)
            {
                thinSections.Add($"{section} ({wordCount} words, need {MinSectionWordCount}+)");
            }
        }

        return thinSections.Count > 0
            ? $"Sections with insufficient content: {string.Join("; ", thinSections)}"
            : null;
    }

    private static string? ExtractSectionContent(string content, string sectionName)
    {
        // Find "## Section Name" header (case-insensitive)
        var headerPattern = $"## {sectionName}";
        var headerIndex = content.IndexOf(headerPattern, StringComparison.OrdinalIgnoreCase);

        if (headerIndex < 0)
        {
            return null;
        }

        var contentStart = content.IndexOf('\n', headerIndex);
        if (contentStart < 0)
        {
            return string.Empty;
        }

        contentStart++;

        // Find the next H2 header or end of document
        var nextHeaderIndex = content.IndexOf("\n## ", contentStart, StringComparison.Ordinal);
        var sectionEnd = nextHeaderIndex >= 0 ? nextHeaderIndex : content.Length;

        return content[contentStart..sectionEnd].Trim();
    }
}
