using NexusLabs.Needlr.AgentFramework.Testing;

using RfcPipelineApp.Core;
using RfcPipelineApp.Core.Prompts;

namespace RfcPipelineApp.Tests;

public sealed class PromptIntegrityTests
{
    private static readonly RfcAssignment TestAssignment = new(
        FeatureTitle: "Widget API v2",
        Description: "Redesign the widget API for better ergonomics.",
        Constraints: ["Must be backward compatible", "< 50ms p99 latency"],
        ExistingContext: ["RFC-001: Widget API v1", "Widget usage telemetry dashboard"]);

    [Fact]
    public void ResearchPrompts_ContainsWorkspaceInstructions()
    {
        var prompt = ResearchPrompts.BuildResearch(TestAssignment);

        PromptAssert.Contains(prompt, TestAssignment.ResearchPath);
        PromptAssert.Contains(prompt, "research");
        PromptAssert.Contains(prompt, TestAssignment.FeatureTitle);
    }

    [Fact]
    public void DraftPrompts_ContainsStructureGuidance()
    {
        var prompt = DraftPrompts.BuildOutline(TestAssignment);

        PromptAssert.Contains(prompt, "Problem Statement");
        PromptAssert.Contains(prompt, "Proposed Solution");
        PromptAssert.Contains(prompt, "Technical Design");
        PromptAssert.Contains(prompt, "Alternatives Considered");
        PromptAssert.Contains(prompt, "Migration Plan");
        PromptAssert.Contains(prompt, "Rollback Strategy");
    }

    [Fact]
    public void ReviewPrompts_ContainsTechnicalReviewCriteria()
    {
        var prompt = ReviewPrompts.BuildTechnicalReview(TestAssignment);

        PromptAssert.Contains(prompt, "Logical Consistency");
        PromptAssert.Contains(prompt, "Technical Feasibility");
        PromptAssert.Contains(prompt, "Completeness");
        PromptAssert.Contains(prompt, "review-findings.md");
    }

    [Fact]
    public void ColdReaderPrompts_ContainsEvaluationCriteria()
    {
        var prompt = ColdReaderPrompts.BuildCritic(TestAssignment);

        PromptAssert.Contains(prompt, "Self-contained");
        PromptAssert.Contains(prompt, "Actionable");
        PromptAssert.Contains(prompt, "Persuasive");
        PromptAssert.Contains(prompt, "APPROVED");
        PromptAssert.Contains(prompt, TestAssignment.DraftPath);
    }
}
