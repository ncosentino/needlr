using NexusLabs.Needlr.AgentFramework.Diagnostics;

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

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ChatCompletionDiagnostics MakeCompletion(int sequence) =>
        new(Sequence: sequence,
            Model: "test-model",
            Tokens: new TokenUsage(10, 20, 30, 0, 0),
            InputMessageCount: 1,
            Duration: TimeSpan.FromMilliseconds(10),
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow);

    private static ToolCallDiagnostics MakeToolCall(int sequence) =>
        new(Sequence: sequence,
            ToolName: "test-tool",
            Duration: TimeSpan.FromMilliseconds(5),
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow,
            CustomMetrics: null);

    private sealed class FakeSink : IDiagnosticsSink
    {
        public List<ChatCompletionDiagnostics> ChatCompletions { get; } = [];
        public List<ToolCallDiagnostics> ToolCalls { get; } = [];
        public string? AgentName => "Fake";
        public int NextChatCompletionSequence() => 0;
        public int NextToolCallSequence() => 0;
        public void AddChatCompletion(ChatCompletionDiagnostics d) => ChatCompletions.Add(d);
        public void AddToolCall(ToolCallDiagnostics d) => ToolCalls.Add(d);
    }

    private sealed class ThrowingSink : IDiagnosticsSink
    {
        public string? AgentName => "Throwing";
        public int NextChatCompletionSequence() => 0;
        public int NextToolCallSequence() => 0;
        public void AddChatCompletion(ChatCompletionDiagnostics _) => throw new InvalidOperationException("fail");
        public void AddToolCall(ToolCallDiagnostics _) => throw new InvalidOperationException("fail");
    }
}
