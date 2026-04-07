using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class AgentRunDiagnosticsBuilderTests
{
    [Fact]
    public void StartNew_SetsAgentName()
    {
        var builder = AgentRunDiagnosticsBuilder.StartNew("TestAgent");

        var result = builder.Build();

        Assert.Equal("TestAgent", result.AgentName);
        AgentRunDiagnosticsBuilder.ClearCurrent();
    }

    [Fact]
    public void StartNew_MakesBuilderAccessibleViaCurrent()
    {
        var builder = AgentRunDiagnosticsBuilder.StartNew("Agent");

        Assert.Same(builder, AgentRunDiagnosticsBuilder.GetCurrent());
        AgentRunDiagnosticsBuilder.ClearCurrent();
    }

    [Fact]
    public void ClearCurrent_RemovesBuilder()
    {
        AgentRunDiagnosticsBuilder.StartNew("Agent");

        AgentRunDiagnosticsBuilder.ClearCurrent();

        Assert.Null(AgentRunDiagnosticsBuilder.GetCurrent());
    }

    [Fact]
    public void Build_Succeeded_DefaultsToTrue()
    {
        var builder = AgentRunDiagnosticsBuilder.StartNew("Agent");

        var result = builder.Build();

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorMessage);
        AgentRunDiagnosticsBuilder.ClearCurrent();
    }

    [Fact]
    public void RecordFailure_SetsSucceededFalse()
    {
        var builder = AgentRunDiagnosticsBuilder.StartNew("Agent");
        builder.RecordFailure("boom");

        var result = builder.Build();

        Assert.False(result.Succeeded);
        Assert.Equal("boom", result.ErrorMessage);
        AgentRunDiagnosticsBuilder.ClearCurrent();
    }

    [Fact]
    public void RecordInputOutputMessageCounts()
    {
        var builder = AgentRunDiagnosticsBuilder.StartNew("Agent");
        builder.RecordInputMessageCount(5);
        builder.RecordOutputMessageCount(3);

        var result = builder.Build();

        Assert.Equal(5, result.TotalInputMessages);
        Assert.Equal(3, result.TotalOutputMessages);
        AgentRunDiagnosticsBuilder.ClearCurrent();
    }

    [Fact]
    public void AddChatCompletion_AccumulatesTokens()
    {
        var builder = AgentRunDiagnosticsBuilder.StartNew("Agent");

        builder.AddChatCompletion(MakeCompletion(sequence: 0, input: 10, output: 20, total: 30));
        builder.AddChatCompletion(MakeCompletion(sequence: 1, input: 5, output: 15, total: 20));

        var result = builder.Build();

        Assert.Equal(2, result.ChatCompletions.Count);
        Assert.Equal(15, result.AggregateTokenUsage.InputTokens);
        Assert.Equal(35, result.AggregateTokenUsage.OutputTokens);
        Assert.Equal(50, result.AggregateTokenUsage.TotalTokens);
        AgentRunDiagnosticsBuilder.ClearCurrent();
    }

    [Fact]
    public void ChatCompletions_OrderedBySequence_NotInsertionOrder()
    {
        var builder = AgentRunDiagnosticsBuilder.StartNew("Agent");

        builder.AddChatCompletion(MakeCompletion(sequence: 2));
        builder.AddChatCompletion(MakeCompletion(sequence: 0));
        builder.AddChatCompletion(MakeCompletion(sequence: 1));

        var result = builder.Build();

        Assert.Equal(0, result.ChatCompletions[0].Sequence);
        Assert.Equal(1, result.ChatCompletions[1].Sequence);
        Assert.Equal(2, result.ChatCompletions[2].Sequence);
        AgentRunDiagnosticsBuilder.ClearCurrent();
    }

    [Fact]
    public void AddToolCall_AccumulatesAndOrders()
    {
        var builder = AgentRunDiagnosticsBuilder.StartNew("Agent");

        builder.AddToolCall(MakeToolCall(sequence: 1, name: "B"));
        builder.AddToolCall(MakeToolCall(sequence: 0, name: "A"));

        var result = builder.Build();

        Assert.Equal(2, result.ToolCalls.Count);
        Assert.Equal("A", result.ToolCalls[0].ToolName);
        Assert.Equal("B", result.ToolCalls[1].ToolName);
        AgentRunDiagnosticsBuilder.ClearCurrent();
    }

    [Fact]
    public void NextChatCompletionSequence_IsMonotonicallyIncreasing()
    {
        var builder = AgentRunDiagnosticsBuilder.StartNew("Agent");

        Assert.Equal(0, builder.NextChatCompletionSequence());
        Assert.Equal(1, builder.NextChatCompletionSequence());
        Assert.Equal(2, builder.NextChatCompletionSequence());
        AgentRunDiagnosticsBuilder.ClearCurrent();
    }

    [Fact]
    public void NextToolCallSequence_IsMonotonicallyIncreasing()
    {
        var builder = AgentRunDiagnosticsBuilder.StartNew("Agent");

        Assert.Equal(0, builder.NextToolCallSequence());
        Assert.Equal(1, builder.NextToolCallSequence());
        Assert.Equal(2, builder.NextToolCallSequence());
        AgentRunDiagnosticsBuilder.ClearCurrent();
    }

    [Fact]
    public void Build_Duration_IsNonZero()
    {
        var builder = AgentRunDiagnosticsBuilder.StartNew("Agent");
        Thread.Sleep(5); // ensure measurable duration

        var result = builder.Build();

        Assert.True(result.TotalDuration > TimeSpan.Zero);
        AgentRunDiagnosticsBuilder.ClearCurrent();
    }

    [Fact]
    public void Build_EmptyBuilder_HasZeroTokens()
    {
        var builder = AgentRunDiagnosticsBuilder.StartNew("Agent");

        var result = builder.Build();

        Assert.Equal(0, result.AggregateTokenUsage.InputTokens);
        Assert.Equal(0, result.AggregateTokenUsage.TotalTokens);
        Assert.Empty(result.ChatCompletions);
        Assert.Empty(result.ToolCalls);
        AgentRunDiagnosticsBuilder.ClearCurrent();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ChatCompletionDiagnostics MakeCompletion(
        int sequence, long input = 0, long output = 0, long total = 0) =>
        new(Sequence: sequence,
            Model: "test-model",
            Tokens: new TokenUsage(input, output, total, 0, 0),
            InputMessageCount: 1,
            Duration: TimeSpan.FromMilliseconds(10),
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow);

    private static ToolCallDiagnostics MakeToolCall(int sequence, string name = "tool") =>
        new(Sequence: sequence,
            ToolName: name,
            Duration: TimeSpan.FromMilliseconds(5),
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow,
            CustomMetrics: null);
}
