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


    [Fact]
    public void ShouldTerminate_NullContext_ThrowsArgumentNullException()
    {
        var condition = new KeywordTerminationCondition("APPROVED");
        Assert.Throws<ArgumentNullException>(() => condition.ShouldTerminate(null!));
    }


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


    [Fact]
    public void ShouldTerminate_AgentFilter_MatchingAgent_ReturnsTrue()
    {
        var condition = new KeywordTerminationCondition("APPROVED", "ApprovalAgent");
        var ctx = MakeContext("ApprovalAgent", "APPROVED");

        Assert.True(condition.ShouldTerminate(ctx));
    }

    [Fact]
    public void ShouldTerminate_AgentFilter_MatchingAgentWithHashSuffix_ReturnsTrue()
    {
        var condition = new KeywordTerminationCondition("APPROVED", "ApprovalAgent");
        var ctx = MakeContext("ApprovalAgent_a1b2c3d4e5f64a5b84a8b1850a83e94c", "APPROVED");

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
