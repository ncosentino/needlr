using RfcPipelineApp.Core.Validators;

namespace RfcPipelineApp.Tests;

public sealed class StructureValidatorTests
{
    private static string BuildValidRfc()
    {
        // Build an RFC with all required sections and enough words to pass the 500-word minimum.
        var filler = string.Join(" ", Enumerable.Repeat("lorem ipsum dolor sit amet consectetur adipiscing elit", 15));
        return $"""
            # RFC: Test Feature

            ## Problem Statement
            {filler}

            ## Background
            {filler}

            ## Proposed Solution
            {filler}

            ## Technical Design
            {filler}

            ## Alternatives Considered
            {filler}

            ## Trade-offs
            {filler}

            ## Migration Plan
            {filler}

            ## Rollback Strategy
            {filler}
            """;
    }

    [Fact]
    public void Validate_AllRequiredSections_ReturnsNull()
    {
        var content = BuildValidRfc();

        var result = StructureValidator.Validate(content);

        Assert.Null(result);
    }

    [Fact]
    public void Validate_MissingProblemStatement_ReturnsError()
    {
        var content = BuildValidRfc().Replace(
            "## Problem Statement",
            "## Introduction");

        var result = StructureValidator.Validate(content);

        Assert.NotNull(result);
        Assert.Contains("Problem Statement", result);
    }

    [Fact]
    public void Validate_MissingProposedSolution_ReturnsError()
    {
        var content = BuildValidRfc().Replace(
            "## Proposed Solution",
            "## Our Approach");

        var result = StructureValidator.Validate(content);

        Assert.NotNull(result);
        Assert.Contains("Proposed Solution", result);
    }

    [Fact]
    public void Validate_EmptyContent_ReturnsError()
    {
        var result = StructureValidator.Validate(string.Empty);

        Assert.NotNull(result);
        Assert.Contains("empty", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_TooFewWords_ReturnsError()
    {
        // All sections present but total content far below 500 words.
        var content = """
            ## Problem Statement
            Short.
            ## Background
            Short.
            ## Proposed Solution
            Short.
            ## Technical Design
            Short.
            ## Alternatives Considered
            Short.
            ## Trade-offs
            Short.
            ## Migration Plan
            Short.
            ## Rollback Strategy
            Short.
            """;

        var result = StructureValidator.Validate(content);

        Assert.NotNull(result);
        Assert.Contains("words", result, StringComparison.OrdinalIgnoreCase);
    }
}
