using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Workflows;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class ToolCallTerminationConditionTests
{
    // -------------------------------------------------------------------------
    // Constructor guards
    // -------------------------------------------------------------------------

    [Fact]
    public void Ctor_NullToolName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ToolCallTerminationCondition(null!));
    }

    [Fact]
    public void Ctor_WhitespaceToolName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new ToolCallTerminationCondition("   "));
    }

    // -------------------------------------------------------------------------
    // Matching: tool name only (no agent filter)
    // -------------------------------------------------------------------------

    [Fact]
    public void ShouldTerminate_ToolCalled_ReturnsTrue()
    {
        var condition = new ToolCallTerminationCondition("ApproveArticle");
        var ctx = MakeContext("Reviewer", toolCallNames: ["ApproveArticle"]);

        Assert.True(condition.ShouldTerminate(ctx));
    }

    [Fact]
    public void ShouldTerminate_DifferentToolCalled_ReturnsFalse()
    {
        var condition = new ToolCallTerminationCondition("ApproveArticle");
        var ctx = MakeContext("Reviewer", toolCallNames: ["RecordReviewIssue"]);

        Assert.False(condition.ShouldTerminate(ctx));
    }

    [Fact]
    public void ShouldTerminate_NoToolsCalled_ReturnsFalse()
    {
        var condition = new ToolCallTerminationCondition("ApproveArticle");
        var ctx = MakeContext("Reviewer", toolCallNames: []);

        Assert.False(condition.ShouldTerminate(ctx));
    }

    [Fact]
    public void ShouldTerminate_MultipleToolsCalled_MatchesIfTargetIsAmongThem()
    {
        var condition = new ToolCallTerminationCondition("ApproveArticle");
        var ctx = MakeContext("Reviewer", toolCallNames: ["ReadFile", "ApproveArticle", "RecordIssue"]);

        Assert.True(condition.ShouldTerminate(ctx));
    }

    [Fact]
    public void ShouldTerminate_CaseInsensitive_ReturnsFalse()
    {
        // Tool name matching is exact (case-sensitive) by default
        var condition = new ToolCallTerminationCondition("ApproveArticle");
        var ctx = MakeContext("Reviewer", toolCallNames: ["approvearticle"]);

        Assert.False(condition.ShouldTerminate(ctx));
    }

    // -------------------------------------------------------------------------
    // Agent filtering
    // -------------------------------------------------------------------------

    [Fact]
    public void ShouldTerminate_CorrectAgent_ReturnsTrue()
    {
        var condition = new ToolCallTerminationCondition("ApproveArticle", "Reviewer");
        var ctx = MakeContext("Reviewer", toolCallNames: ["ApproveArticle"]);

        Assert.True(condition.ShouldTerminate(ctx));
    }

    [Fact]
    public void ShouldTerminate_WrongAgent_ReturnsFalse()
    {
        var condition = new ToolCallTerminationCondition("ApproveArticle", "Reviewer");
        var ctx = MakeContext("Writer", toolCallNames: ["ApproveArticle"]);

        Assert.False(condition.ShouldTerminate(ctx));
    }

    [Fact]
    public void ShouldTerminate_AgentWithGuidSuffix_MatchesPrefix()
    {
        var condition = new ToolCallTerminationCondition("ApproveArticle", "Reviewer");
        var ctx = MakeContext("Reviewer_abc123", toolCallNames: ["ApproveArticle"]);

        Assert.True(condition.ShouldTerminate(ctx));
    }

    // -------------------------------------------------------------------------
    // Does NOT match on response text (the whole point)
    // -------------------------------------------------------------------------

    [Fact]
    public void ShouldTerminate_ToolNameInTextButNotInToolCalls_ReturnsFalse()
    {
        var condition = new ToolCallTerminationCondition("ApproveArticle");
        var ctx = MakeContext("Reviewer",
            responseText: "I called ApproveArticle and the article is approved",
            toolCallNames: []);

        Assert.False(condition.ShouldTerminate(ctx));
    }

    // -------------------------------------------------------------------------
    // Null context guard
    // -------------------------------------------------------------------------

    [Fact]
    public void ShouldTerminate_NullContext_ThrowsArgumentNullException()
    {
        var condition = new ToolCallTerminationCondition("ApproveArticle");
        Assert.Throws<ArgumentNullException>(() => condition.ShouldTerminate(null!));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static TerminationContext MakeContext(
        string agentId,
        IReadOnlyList<string>? toolCallNames = null,
        string responseText = "") =>
        new()
        {
            AgentId = agentId,
            LastMessage = new ChatMessage(ChatRole.Assistant, responseText),
            TurnCount = 1,
            ConversationHistory = [],
            ToolCallNames = toolCallNames ?? [],
        };
}
