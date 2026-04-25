using RfcPipelineApp.Core;

namespace RfcPipelineApp.Tests;

public sealed class RfcAssignmentTests
{
    private static readonly RfcAssignment Assignment = new(
        FeatureTitle: "Caching Layer",
        Description: "Add a distributed caching layer.",
        Constraints: ["Must support Redis"],
        ExistingContext: ["RFC-042: Data access patterns"]);

    [Fact]
    public void DraftPath_ReturnsExpectedValue()
    {
        Assert.Equal("rfc-draft.md", Assignment.DraftPath);
    }

    [Fact]
    public void ResearchPath_ReturnsExpectedValue()
    {
        Assert.Equal("research-brief.md", Assignment.ResearchPath);
    }

    [Fact]
    public void OutlinePath_ReturnsExpectedValue()
    {
        Assert.Equal("outline.md", Assignment.OutlinePath);
    }
}
