using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Testing;
using NexusLabs.Needlr.AgentFramework.Workflows.Sequential;
using NexusLabs.Needlr.AgentFramework.Workspace;

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

    private static StageExecutionContext CreateTestContext()
    {
        var diagAccessor = new Mock<IAgentDiagnosticsAccessor>();
        diagAccessor.Setup(x => x.BeginCapture()).Returns(Mock.Of<IDisposable>());

        return new StageExecutionContext(
            new InMemoryWorkspace(),
            diagAccessor.Object,
            ProgressReporter: null,
            StageIndex: 0,
            TotalStages: 1,
            StageName: "Test");
    }

    [Fact]
    public void ResearchPrompts_ContainsWorkspaceInstructions()
    {
        var ctx = CreateTestContext();
        var prompt = ResearchPrompts.BuildResearch(TestAssignment, ctx);

        PromptAssert.Contains(prompt, "research");
        PromptAssert.Contains(prompt, TestAssignment.FeatureTitle);
    }

    [Fact]
    public void DraftPrompts_ContainsStructureGuidance()
    {
        var ctx = CreateTestContext();
        var prompt = DraftPrompts.BuildOutline(TestAssignment, ctx);

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
        var ctx = CreateTestContext();
        var prompt = ReviewPrompts.BuildTechnicalReview(TestAssignment, ctx);

        PromptAssert.Contains(prompt, "Logical Consistency");
        PromptAssert.Contains(prompt, "Technical Feasibility");
        PromptAssert.Contains(prompt, "Completeness");
        PromptAssert.Contains(prompt, "review");
    }

    [Fact]
    public void ColdReaderPrompts_ContainsEvaluationCriteria()
    {
        var ctx = CreateTestContext();
        var prompt = ColdReaderPrompts.BuildCritic(TestAssignment, ctx);

        PromptAssert.Contains(prompt, "Self-contained");
        PromptAssert.Contains(prompt, "Actionable");
        PromptAssert.Contains(prompt, "Persuasive");
        PromptAssert.Contains(prompt, "APPROVED");
    }
}
