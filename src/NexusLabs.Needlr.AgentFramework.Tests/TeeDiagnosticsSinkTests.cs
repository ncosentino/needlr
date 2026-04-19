using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class TeeDiagnosticsSinkTests
{
    [Fact]
    public void Constructor_NullSinks_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TeeDiagnosticsSink("Agent", null!));
    }

    [Fact]
    public void Constructor_EmptySinks_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new TeeDiagnosticsSink("Agent", []));
    }

    [Fact]
    public void AgentName_ReturnsConstructorValue()
    {
        var sink = new TeeDiagnosticsSink("TestAgent", [new FakeSink()]);
        Assert.Equal("TestAgent", sink.AgentName);
    }

    [Fact]
    public void NextChatCompletionSequence_IsMonotonicallyIncreasing()
    {
        var sink = new TeeDiagnosticsSink("Agent", [new FakeSink()]);
        Assert.Equal(0, sink.NextChatCompletionSequence());
        Assert.Equal(1, sink.NextChatCompletionSequence());
        Assert.Equal(2, sink.NextChatCompletionSequence());
    }

    [Fact]
    public void NextToolCallSequence_IsMonotonicallyIncreasing()
    {
        var sink = new TeeDiagnosticsSink("Agent", [new FakeSink()]);
        Assert.Equal(0, sink.NextToolCallSequence());
        Assert.Equal(1, sink.NextToolCallSequence());
        Assert.Equal(2, sink.NextToolCallSequence());
    }

    [Fact]
    public void AddChatCompletion_DispatchesToAllSinks()
    {
        var sink1 = new FakeSink();
        var sink2 = new FakeSink();
        var tee = new TeeDiagnosticsSink("Agent", [sink1, sink2]);

        var completion = MakeCompletion(0);
        tee.AddChatCompletion(completion);

        Assert.Single(sink1.ChatCompletions);
        Assert.Single(sink2.ChatCompletions);
        Assert.Same(completion, sink1.ChatCompletions[0]);
        Assert.Same(completion, sink2.ChatCompletions[0]);
    }

    [Fact]
    public void AddToolCall_DispatchesToAllSinks()
    {
        var sink1 = new FakeSink();
        var sink2 = new FakeSink();
        var tee = new TeeDiagnosticsSink("Agent", [sink1, sink2]);

        var toolCall = MakeToolCall(0);
        tee.AddToolCall(toolCall);

        Assert.Single(sink1.ToolCalls);
        Assert.Single(sink2.ToolCalls);
        Assert.Same(toolCall, sink1.ToolCalls[0]);
        Assert.Same(toolCall, sink2.ToolCalls[0]);
    }

    [Fact]
    public void AddChatCompletion_OneSinkThrows_OtherSinksStillReceive()
    {
        var throwingSink = new ThrowingSink();
        var goodSink = new FakeSink();
        var tee = new TeeDiagnosticsSink("Agent", [throwingSink, goodSink]);

        var completion = MakeCompletion(0);
        tee.AddChatCompletion(completion);

        Assert.Single(goodSink.ChatCompletions);
        Assert.Same(completion, goodSink.ChatCompletions[0]);
    }

    [Fact]
    public void AddToolCall_OneSinkThrows_OtherSinksStillReceive()
    {
        var throwingSink = new ThrowingSink();
        var goodSink = new FakeSink();
        var tee = new TeeDiagnosticsSink("Agent", [throwingSink, goodSink]);

        var toolCall = MakeToolCall(0);
        tee.AddToolCall(toolCall);

        Assert.Single(goodSink.ToolCalls);
        Assert.Same(toolCall, goodSink.ToolCalls[0]);
    }

    [Fact]
    public void AddChatCompletion_OneSinkThrows_DoesNotThrowToCaller()
    {
        var throwingSink = new ThrowingSink();
        var tee = new TeeDiagnosticsSink("Agent", [throwingSink]);

        var exception = Record.Exception(() => tee.AddChatCompletion(MakeCompletion(0)));

        Assert.Null(exception);
    }

    [Fact]
    public void AddToolCall_OneSinkThrows_DoesNotThrowToCaller()
    {
        var throwingSink = new ThrowingSink();
        var tee = new TeeDiagnosticsSink("Agent", [throwingSink]);

        var exception = Record.Exception(() => tee.AddToolCall(MakeToolCall(0)));

        Assert.Null(exception);
    }

    [Fact]
    public void SequenceCounters_AreIndependentOfInnerSinks()
    {
        var inner = new FakeSink();
        var tee = new TeeDiagnosticsSink("Agent", [inner]);

        // Reserve sequences on the inner sink directly
        inner.NextChatCompletionSequence();
        inner.NextToolCallSequence();

        // Tee's counters should start at 0, independent of inner
        Assert.Equal(0, tee.NextChatCompletionSequence());
        Assert.Equal(0, tee.NextToolCallSequence());
    }

    [Fact]
    public void ConcurrentNextSequence_ProducesUniqueValues()
    {
        var tee = new TeeDiagnosticsSink("Agent", [new FakeSink()]);
        var sequences = new int[100];

        Parallel.For(0, 100, i =>
        {
            sequences[i] = tee.NextToolCallSequence();
        });

        Assert.Equal(100, sequences.Distinct().Count());
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ChatCompletionDiagnostics MakeCompletion(int sequence) =>
        new(Sequence: sequence,
            Model: "test-model",
            Tokens: new TokenUsage(0, 0, 0, 0, 0),
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
        private int _chatSeq;
        private int _toolSeq;

        public List<ChatCompletionDiagnostics> ChatCompletions { get; } = [];
        public List<ToolCallDiagnostics> ToolCalls { get; } = [];
        public string? AgentName => "Fake";

        public int NextChatCompletionSequence() =>
            Interlocked.Increment(ref _chatSeq) - 1;

        public int NextToolCallSequence() =>
            Interlocked.Increment(ref _toolSeq) - 1;

        public void AddChatCompletion(ChatCompletionDiagnostics diagnostics) =>
            ChatCompletions.Add(diagnostics);

        public void AddToolCall(ToolCallDiagnostics diagnostics) =>
            ToolCalls.Add(diagnostics);
    }

    private sealed class ThrowingSink : IDiagnosticsSink
    {
        public string? AgentName => "Throwing";
        public int NextChatCompletionSequence() => 0;
        public int NextToolCallSequence() => 0;
        public void AddChatCompletion(ChatCompletionDiagnostics _) => throw new InvalidOperationException("Sink failure");
        public void AddToolCall(ToolCallDiagnostics _) => throw new InvalidOperationException("Sink failure");
    }
}
