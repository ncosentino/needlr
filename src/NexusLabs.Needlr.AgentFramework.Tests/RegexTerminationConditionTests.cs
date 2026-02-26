using System.Text.RegularExpressions;

using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Workflows;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class RegexTerminationConditionTests
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
    public void Ctor_NullPattern_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RegexTerminationCondition(null!));
    }

    [Fact]
    public void Ctor_WhitespacePattern_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new RegexTerminationCondition("   "));
    }

    // ----- ShouldTerminate guard -----

    [Fact]
    public void ShouldTerminate_NullContext_ThrowsArgumentNullException()
    {
        var condition = new RegexTerminationCondition(@"\bLGTM\b");
        Assert.Throws<ArgumentNullException>(() => condition.ShouldTerminate(null!));
    }

    // ----- pattern matching -----

    [Theory]
    [InlineData(@"\bLGTM\b", "The PR looks great. LGTM.")]
    [InlineData(@"\bLGTM\b", "lgtm")]                   // IgnoreCase default
    [InlineData(@"^DONE$", "DONE")]
    [InlineData(@"\d{3}", "Error code 404 encountered")]
    public void ShouldTerminate_ResponseMatchesPattern_ReturnsTrue(string pattern, string response)
    {
        var condition = new RegexTerminationCondition(pattern);
        var ctx = MakeContext("Agent", response);

        Assert.True(condition.ShouldTerminate(ctx));
    }

    [Theory]
    [InlineData(@"\bLGTM\b", "Needs more work")]
    [InlineData(@"^DONE$", "NOT DONE")]
    public void ShouldTerminate_ResponseDoesNotMatchPattern_ReturnsFalse(string pattern, string response)
    {
        var condition = new RegexTerminationCondition(pattern);
        var ctx = MakeContext("Agent", response);

        Assert.False(condition.ShouldTerminate(ctx));
    }

    // ----- agent filter -----

    [Fact]
    public void ShouldTerminate_AgentFilter_MatchingAgent_ReturnsTrue()
    {
        var condition = new RegexTerminationCondition(@"\bLGTM\b", "ApprovalAgent");
        var ctx = MakeContext("ApprovalAgent", "LGTM");

        Assert.True(condition.ShouldTerminate(ctx));
    }

    [Fact]
    public void ShouldTerminate_AgentFilter_WrongAgent_ReturnsFalse()
    {
        var condition = new RegexTerminationCondition(@"\bLGTM\b", "ApprovalAgent");
        var ctx = MakeContext("OtherAgent", "LGTM");

        Assert.False(condition.ShouldTerminate(ctx));
    }

    // ----- custom options -----

    [Fact]
    public void ShouldTerminate_CaseSensitiveOptions_DoesNotMatchWrongCase()
    {
        var condition = new RegexTerminationCondition(
            "LGTM", agentId: null, RegexOptions.None);
        var ctx = MakeContext("A", "lgtm");

        Assert.False(condition.ShouldTerminate(ctx));
    }
}
