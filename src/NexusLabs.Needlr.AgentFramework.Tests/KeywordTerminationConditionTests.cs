using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Workflows;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class KeywordTerminationConditionTests
{
    private static TerminationContext MakeContext(string agentId, string response) =>
        new()
        {
            AgentId = agentId,
            ResponseText = response,
            TurnCount = 1,
            ConversationHistory = [],
        };

    // ----- constructor guard -----

    [Fact]
    public void Ctor_NullKeyword_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new KeywordTerminationCondition(null!));
    }

    [Fact]
    public void Ctor_WhitespaceKeyword_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new KeywordTerminationCondition("   "));
    }

    // ----- ShouldTerminate guard -----

    [Fact]
    public void ShouldTerminate_NullContext_ThrowsArgumentNullException()
    {
        var condition = new KeywordTerminationCondition("APPROVED");
        Assert.Throws<ArgumentNullException>(() => condition.ShouldTerminate(null!));
    }

    // ----- keyword matching -----

    [Theory]
    [InlineData("Task APPROVED by reviewer")]
    [InlineData("APPROVED")]
    [InlineData("approved")]           // case-insensitive
    [InlineData("The result is Approved.")]
    public void ShouldTerminate_ResponseContainsKeyword_ReturnsTrue(string response)
    {
        var condition = new KeywordTerminationCondition("APPROVED");
        var ctx = MakeContext("ReviewAgent", response);

        Assert.True(condition.ShouldTerminate(ctx));
    }

    [Fact]
    public void ShouldTerminate_ResponseLacksKeyword_ReturnsFalse()
    {
        var condition = new KeywordTerminationCondition("APPROVED");
        var ctx = MakeContext("ReviewAgent", "Looks good, keep going.");

        Assert.False(condition.ShouldTerminate(ctx));
    }

    // ----- agent filter -----

    [Fact]
    public void ShouldTerminate_AgentFilter_MatchingAgent_ReturnsTrue()
    {
        var condition = new KeywordTerminationCondition("APPROVED", "ApprovalAgent");
        var ctx = MakeContext("ApprovalAgent", "APPROVED");

        Assert.True(condition.ShouldTerminate(ctx));
    }

    [Fact]
    public void ShouldTerminate_AgentFilter_WrongAgent_ReturnsFalse()
    {
        var condition = new KeywordTerminationCondition("APPROVED", "ApprovalAgent");
        var ctx = MakeContext("OtherAgent", "APPROVED");

        Assert.False(condition.ShouldTerminate(ctx));
    }

    [Fact]
    public void ShouldTerminate_AgentFilter_NullAgentId_MatchesAnyAgent()
    {
        var condition = new KeywordTerminationCondition("APPROVED", agentId: null);
        var ctx = MakeContext("AnyAgent", "APPROVED");

        Assert.True(condition.ShouldTerminate(ctx));
    }

    // ----- custom comparison -----

    [Fact]
    public void ShouldTerminate_CaseSensitiveComparison_DoesNotMatchWrongCase()
    {
        var condition = new KeywordTerminationCondition(
            "APPROVED", agentId: null, StringComparison.Ordinal);
        var ctx = MakeContext("A", "approved");

        Assert.False(condition.ShouldTerminate(ctx));
    }

    [Fact]
    public void ShouldTerminate_CaseSensitiveComparison_MatchesExactCase()
    {
        var condition = new KeywordTerminationCondition(
            "APPROVED", agentId: null, StringComparison.Ordinal);
        var ctx = MakeContext("A", "APPROVED");

        Assert.True(condition.ShouldTerminate(ctx));
    }
}
