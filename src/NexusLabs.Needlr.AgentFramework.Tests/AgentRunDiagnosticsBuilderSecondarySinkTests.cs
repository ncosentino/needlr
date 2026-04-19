using NexusLabs.Needlr.AgentFramework.Diagnostics;

using static NexusLabs.Needlr.AgentFramework.Tests.AgentRunDiagnosticsBuilderSecondarySinkTestsHelpers;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class AgentRunDiagnosticsBuilderSecondarySinkTests
{
    [Fact]
    public void StartNew_WithSecondarySinks_ForwardsChatCompletion()
    {
        var secondary = new FakeSink();
        using var builder = AgentRunDiagnosticsBuilder.StartNew("Agent", [secondary]);

        var completion = MakeCompletion(0);
        builder.AddChatCompletion(completion);

        var result = builder.Build();
        Assert.Single(result.ChatCompletions);
        Assert.Single(secondary.ChatCompletions);
        Assert.Same(completion, secondary.ChatCompletions[0]);
    }

    [Fact]
    public void StartNew_WithSecondarySinks_ForwardsToolCall()
    {
        var secondary = new FakeSink();
        using var builder = AgentRunDiagnosticsBuilder.StartNew("Agent", [secondary]);

        var toolCall = MakeToolCall(0);
        builder.AddToolCall(toolCall);

        var result = builder.Build();
        Assert.Single(result.ToolCalls);
        Assert.Single(secondary.ToolCalls);
        Assert.Same(toolCall, secondary.ToolCalls[0]);
    }

    [Fact]
    public void StartNew_WithNullSecondarySinks_DoesNotThrow()
    {
        using var builder = AgentRunDiagnosticsBuilder.StartNew("Agent", secondarySinks: null);

        var completion = MakeCompletion(0);
        builder.AddChatCompletion(completion);

        var result = builder.Build();
        Assert.Single(result.ChatCompletions);
    }

    [Fact]
    public void StartNew_WithEmptySecondarySinks_DoesNotThrow()
    {
        using var builder = AgentRunDiagnosticsBuilder.StartNew("Agent", []);

        var completion = MakeCompletion(0);
        builder.AddChatCompletion(completion);

        var result = builder.Build();
        Assert.Single(result.ChatCompletions);
    }

    [Fact]
    public void SecondarySinkThrows_BuilderStillRecords()
    {
        var throwingSink = new ThrowingSink();
        using var builder = AgentRunDiagnosticsBuilder.StartNew("Agent", [throwingSink]);

        var completion = MakeCompletion(0);
        builder.AddChatCompletion(completion);

        var result = builder.Build();
        Assert.Single(result.ChatCompletions);
    }

    [Fact]
    public void SecondarySinkThrows_DoesNotThrowToCaller()
    {
        var throwingSink = new ThrowingSink();
        using var builder = AgentRunDiagnosticsBuilder.StartNew("Agent", [throwingSink]);

        var exception = Record.Exception(() => builder.AddChatCompletion(MakeCompletion(0)));
        Assert.Null(exception);

        exception = Record.Exception(() => builder.AddToolCall(MakeToolCall(0)));
        Assert.Null(exception);
    }

    [Fact]
    public void MultipleSecondarySinks_AllReceiveRecords()
    {
        var sink1 = new FakeSink();
        var sink2 = new FakeSink();
        using var builder = AgentRunDiagnosticsBuilder.StartNew("Agent", [sink1, sink2]);

        builder.AddChatCompletion(MakeCompletion(0));
        builder.AddToolCall(MakeToolCall(0));

        Assert.Single(sink1.ChatCompletions);
        Assert.Single(sink2.ChatCompletions);
        Assert.Single(sink1.ToolCalls);
        Assert.Single(sink2.ToolCalls);
    }

    [Fact]
    public void ThrowingSink_DoesNotPreventOtherSecondary_FromReceiving()
    {
        var throwingSink = new ThrowingSink();
        var goodSink = new FakeSink();
        using var builder = AgentRunDiagnosticsBuilder.StartNew("Agent", [throwingSink, goodSink]);

        builder.AddToolCall(MakeToolCall(0));

        Assert.Single(goodSink.ToolCalls);
    }

    [Fact]
    public void OriginalStartNew_StillWorksWithoutSecondary()
    {
        using var builder = AgentRunDiagnosticsBuilder.StartNew("Agent");

        builder.AddChatCompletion(MakeCompletion(0));
        builder.AddToolCall(MakeToolCall(0));

        var result = builder.Build();
        Assert.Single(result.ChatCompletions);
        Assert.Single(result.ToolCalls);
    }

}
