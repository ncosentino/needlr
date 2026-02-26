using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class TerminationContextTests
{
    [Fact]
    public void TerminationContext_AllRequiredProperties_Initializes()
    {
        var history = new List<ChatMessage> { new(ChatRole.Assistant, "hello") };

        var ctx = new TerminationContext
        {
            AgentId = "agent-1",
            ResponseText = "hello",
            TurnCount = 1,
            ConversationHistory = history,
        };

        Assert.Equal("agent-1", ctx.AgentId);
        Assert.Equal("hello", ctx.ResponseText);
        Assert.Equal(1, ctx.TurnCount);
        Assert.Same(history, ctx.ConversationHistory);
        Assert.Null(ctx.Usage);
    }

    [Fact]
    public void TerminationContext_WithUsage_ExposesUsage()
    {
        var usage = new UsageDetails { TotalTokenCount = 42 };
        var ctx = new TerminationContext
        {
            AgentId = "a",
            ResponseText = "r",
            TurnCount = 1,
            ConversationHistory = [],
            Usage = usage,
        };

        Assert.Same(usage, ctx.Usage);
        Assert.Equal(42, ctx.Usage!.TotalTokenCount);
    }
}
