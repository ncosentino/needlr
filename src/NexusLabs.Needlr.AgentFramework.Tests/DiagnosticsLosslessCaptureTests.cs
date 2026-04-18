using Microsoft.Extensions.AI;
using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class DiagnosticsLosslessCaptureTests
{
    [Fact]
    public void ChatCompletionDiagnostics_CapturesRequestMessagesAndResponse()
    {
        var requestMessages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a helpful assistant."),
            new(ChatRole.User, "Hello."),
        };
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Hi there!"));

        var diag = new ChatCompletionDiagnostics(
            Sequence: 1,
            Model: "gpt-4",
            Tokens: new TokenUsage(10, 5, 0, 0, 15),
            InputMessageCount: 2,
            Duration: TimeSpan.FromMilliseconds(100),
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow)
        {
            AgentName = "TestAgent",
            RequestMessages = requestMessages,
            Response = response,
        };

        using var builder = AgentRunDiagnosticsBuilder.StartNew("TestAgent");
        builder.AddChatCompletion(diag);
        var result = builder.Build();

        var captured = Assert.Single(result.ChatCompletions);
        Assert.NotNull(captured.RequestMessages);
        Assert.Equal(2, captured.RequestMessages.Count);
        Assert.Equal("You are a helpful assistant.", captured.RequestMessages[0].Text);
        Assert.Equal("Hello.", captured.RequestMessages[1].Text);
        Assert.NotNull(captured.Response);
        Assert.Equal("Hi there!", captured.Response.Text);
    }

    [Fact]
    public void ToolCallDiagnostics_CapturesArgumentsAndResult()
    {
        var arguments = new Dictionary<string, object?>
        {
            ["query"] = "weather",
            ["units"] = "metric",
        };
        var toolResult = new { Temperature = 22, Condition = "Sunny" };

        var diag = new ToolCallDiagnostics(
            Sequence: 1,
            ToolName: "GetWeather",
            Duration: TimeSpan.FromMilliseconds(50),
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow,
            CustomMetrics: null)
        {
            AgentName = "TestAgent",
            Arguments = arguments,
            Result = toolResult,
        };

        using var builder = AgentRunDiagnosticsBuilder.StartNew("TestAgent");
        builder.AddToolCall(diag);
        var result = builder.Build();

        var captured = Assert.Single(result.ToolCalls);
        Assert.NotNull(captured.Arguments);
        Assert.Equal(2, captured.Arguments.Count);
        Assert.Equal("weather", captured.Arguments["query"]);
        Assert.Equal("metric", captured.Arguments["units"]);
        Assert.NotNull(captured.Result);
        Assert.Same(toolResult, captured.Result);
    }

    [Fact]
    public void ChatCompletionDiagnostics_NullContent_IsAllowed()
    {
        var diag = new ChatCompletionDiagnostics(
            Sequence: 1,
            Model: "gpt-4",
            Tokens: new TokenUsage(0, 0, 0, 0, 0),
            InputMessageCount: 0,
            Duration: TimeSpan.FromMilliseconds(10),
            Succeeded: false,
            ErrorMessage: "boom",
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow);

        Assert.Null(diag.RequestMessages);
        Assert.Null(diag.Response);
    }

    [Fact]
    public void ToolCallDiagnostics_NullContent_IsAllowed()
    {
        var diag = new ToolCallDiagnostics(
            Sequence: 1,
            ToolName: "Foo",
            Duration: TimeSpan.FromMilliseconds(10),
            Succeeded: false,
            ErrorMessage: "boom",
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow,
            CustomMetrics: null);

        Assert.Null(diag.Arguments);
        Assert.Null(diag.Result);
    }
}
