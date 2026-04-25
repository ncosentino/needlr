using RfcPipelineApp.Core.Validators;

namespace RfcPipelineApp.Tests;

public sealed class CompletenessValidatorTests
{
    private static string BuildCompleteRfc()
    {
        // Each section needs >= 30 words to pass the completeness check.
        var sectionBody = string.Join(" ", Enumerable.Repeat(
            "This section contains substantive content that demonstrates a thorough analysis of the topic at hand", 2));

        return $"""
            # RFC: Test Feature

            ## Problem Statement
            {sectionBody}

            ## Background
            {sectionBody}

            ## Proposed Solution
            {sectionBody}

            ## Technical Design
            {sectionBody}

            ## Alternatives Considered
            {sectionBody}

            ## Trade-offs
            {sectionBody}

            ## Migration Plan
            {sectionBody}

            ## Rollback Strategy
            {sectionBody}
            """;
    }

    [Fact]
    public void Validate_AllSectionsHaveContent_ReturnsNull()
    {
        var content = BuildCompleteRfc();

        var result = CompletenessValidator.Validate(content);

        Assert.Null(result);
    }

    [Fact]
    public void Validate_EmptySection_ReturnsError()
    {
        // Replace one section's body with nothing — the next header follows immediately.
        var content = BuildCompleteRfc();
        var marker = "## Proposed Solution";
        var nextMarker = "## Technical Design";
        var start = content.IndexOf(marker, StringComparison.Ordinal);
        var end = content.IndexOf(nextMarker, StringComparison.Ordinal);
        content = string.Concat(
            content.AsSpan(0, start),
            $"{marker}\n\n",
            content.AsSpan(end));

        var result = CompletenessValidator.Validate(content);

        Assert.NotNull(result);
        Assert.Contains("Proposed Solution", result);
    }

    [Fact]
    public void Validate_SectionWithOnlyWhitespace_ReturnsError()
    {
        var content = BuildCompleteRfc();
        var marker = "## Background";
        var nextMarker = "## Proposed Solution";
        var start = content.IndexOf(marker, StringComparison.Ordinal);
        var end = content.IndexOf(nextMarker, StringComparison.Ordinal);
        content = string.Concat(
            content.AsSpan(0, start),
            $"{marker}\n   \n   \n",
            content.AsSpan(end));

        var result = CompletenessValidator.Validate(content);

        Assert.NotNull(result);
        Assert.Contains("Background", result);
    }
}
