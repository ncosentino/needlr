using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class DiagnosticsCharCountsTests
{
    [Fact]
    public void ChatMessagesLength_SumsAllTextContent()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a helpful assistant."),
            new(ChatRole.User, "Hi."),
        };

        var count = DiagnosticsCharCounter.ChatMessagesLength(messages);

        Assert.Equal("You are a helpful assistant.".Length + "Hi.".Length, count);
    }

    [Fact]
    public void ChatMessagesLength_NullReturnsZero()
    {
        var count = DiagnosticsCharCounter.ChatMessagesLength(null);

        Assert.Equal(0, count);
    }

    [Fact]
    public void ChatMessagesLength_EmptyReturnsZero()
    {
        var count = DiagnosticsCharCounter.ChatMessagesLength(Array.Empty<ChatMessage>());

        Assert.Equal(0, count);
    }

    [Fact]
    public void ChatResponseLength_SumsMessagesText()
    {
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Hello!"));

        var count = DiagnosticsCharCounter.ChatResponseLength(response);

        Assert.Equal("Hello!".Length, count);
    }

    [Fact]
    public void ChatResponseLength_NullReturnsZero()
    {
        var count = DiagnosticsCharCounter.ChatResponseLength(null);

        Assert.Equal(0, count);
    }

    [Fact]
    public void JsonLength_SerializesDictionary()
    {
        var value = new Dictionary<string, object?>
        {
            ["query"] = "weather",
            ["units"] = "metric",
        };

        var count = DiagnosticsCharCounter.JsonLength(value);

        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(value).Length, count);
    }

    [Fact]
    public void JsonLength_NullReturnsZero()
    {
        var count = DiagnosticsCharCounter.JsonLength(null);

        Assert.Equal(0, count);
    }

    [Fact]
    public void JsonLength_UnserializableReturnsZero()
    {
        var unserializable = new UnserializableThing();

        var count = DiagnosticsCharCounter.JsonLength(unserializable);

        Assert.Equal(0, count);
    }

    [Fact]
    public void ChatCompletionDiagnostics_ExposesCharCounts()
    {
        var diag = new ChatCompletionDiagnostics(
            Sequence: 0,
            Model: "m",
            Tokens: new TokenUsage(0, 0, 0, 0, 0),
            InputMessageCount: 0,
            Duration: TimeSpan.Zero,
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow)
        {
            RequestCharCount = 123,
            ResponseCharCount = 456,
        };

        Assert.Equal(123, diag.RequestCharCount);
        Assert.Equal(456, diag.ResponseCharCount);
    }

    [Fact]
    public void ChatCompletionDiagnostics_CharCountsDefaultToZero()
    {
        var diag = new ChatCompletionDiagnostics(
            Sequence: 0,
            Model: "m",
            Tokens: new TokenUsage(0, 0, 0, 0, 0),
            InputMessageCount: 0,
            Duration: TimeSpan.Zero,
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow);

        Assert.Equal(0, diag.RequestCharCount);
        Assert.Equal(0, diag.ResponseCharCount);
    }

    [Fact]
    public void ToolCallDiagnostics_ExposesCharCounts()
    {
        var diag = new ToolCallDiagnostics(
            Sequence: 0,
            ToolName: "t",
            Duration: TimeSpan.Zero,
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow,
            CustomMetrics: null)
        {
            ArgumentsCharCount = 12,
            ResultCharCount = 34,
        };

        Assert.Equal(12, diag.ArgumentsCharCount);
        Assert.Equal(34, diag.ResultCharCount);
    }

    [Fact]
    public void ToolCallDiagnostics_CharCountsDefaultToZero()
    {
        var diag = new ToolCallDiagnostics(
            Sequence: 0,
            ToolName: "t",
            Duration: TimeSpan.Zero,
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow,
            CustomMetrics: null);

        Assert.Equal(0, diag.ArgumentsCharCount);
        Assert.Equal(0, diag.ResultCharCount);
    }

    [Fact]
    public void ChatMessagesLength_CountsTextReasoningContent()
    {
        var message = new ChatMessage(ChatRole.Assistant,
            [new TextReasoningContent("Let me think about this step by step...")]);

        var count = DiagnosticsCharCounter.ChatMessagesLength(new[] { message });

        Assert.Equal("Let me think about this step by step...".Length, count);
    }

    [Fact]
    public void ChatMessagesLength_SumsMixedTextAndReasoningContent()
    {
        var message = new ChatMessage(ChatRole.Assistant,
        [
            new TextContent("Hello!"),
            new TextReasoningContent("Reasoning trace here"),
        ]);

        var count = DiagnosticsCharCounter.ChatMessagesLength(new[] { message });

        Assert.Equal("Hello!".Length + "Reasoning trace here".Length, count);
    }

    [Fact]
    public void ChatMessagesLength_NullReasoningTextReturnsZero()
    {
        var message = new ChatMessage(ChatRole.Assistant,
            [new TextReasoningContent(null!)]);

        var count = DiagnosticsCharCounter.ChatMessagesLength(new[] { message });

        Assert.Equal(0, count);
    }

    private sealed class UnserializableThing
    {
        public UnserializableThing Self => this;
    }
}
