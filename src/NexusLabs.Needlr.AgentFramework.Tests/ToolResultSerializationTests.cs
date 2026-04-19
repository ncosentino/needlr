using System.Text.Json;

using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Verifies that tool results are JSON-serialized before being sent to the
/// LLM, not passed through via <c>ToString()</c>.
/// </summary>
public sealed class ToolResultSerializationTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task ToolReturnsComplexObject_LlmReceivesJson()
    {
        var toolResult = new SearchResult("Node.js", "https://nodejs.org");
        var captured = await RunToolAndCaptureFunctionResult("search", () => toolResult, _ct);

        var json = Assert.IsType<string>(captured.Result);
        Assert.Contains("Node.js", json);
        Assert.Contains("https://nodejs.org", json);

        // Should be valid JSON, not a C# ToString() like "SearchResult { ... }"
        Assert.DoesNotContain("SearchResult {", json);
        var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("Title", out _) ||
                    doc.RootElement.TryGetProperty("title", out _),
                    "Expected JSON to contain a Title property");
    }

    [Fact]
    public async Task ToolReturnsArrayOfRecords_LlmReceivesJsonArray()
    {
        var toolResult = new[]
        {
            new SearchResult("A", "https://a.com"),
            new SearchResult("B", "https://b.com"),
        };
        var captured = await RunToolAndCaptureFunctionResult("search", () => toolResult, _ct);

        var json = Assert.IsType<string>(captured.Result);
        Assert.StartsWith("[", json);
        Assert.Contains("https://a.com", json);
        Assert.Contains("https://b.com", json);

        var deserialized = JsonSerializer.Deserialize<SearchResult[]>(json);
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.Length);
    }

    [Fact]
    public async Task ToolReturnsString_LlmReceivesStringContent()
    {
        var captured = await RunToolAndCaptureFunctionResult(
            "echo", () => "hello world", _ct);

        var resultStr = Assert.IsType<string>(captured.Result);
        // MEAI may JSON-encode the string (adding quotes). Either way,
        // the actual content must be present.
        Assert.Contains("hello world", resultStr);
    }

    [Fact]
    public async Task ToolReturnsNull_LlmReceivesNullOrEmpty()
    {
        var captured = await RunToolAndCaptureFunctionResult(
            "void_tool", () => (object?)null, _ct);

        var resultStr = captured.Result?.ToString() ?? "";
        // MEAI serializes null returns as a JsonElement with value "null".
        // Either "" or "null" is acceptable — NOT a type name or exception.
        Assert.True(
            resultStr is "" or "null",
            $"Expected empty or 'null' for void tool, got: '{resultStr}'");
    }

    [Fact]
    public async Task ToolReturnsInteger_LlmReceivesSerializedValue()
    {
        var captured = await RunToolAndCaptureFunctionResult(
            "count", () => (object)42, _ct);

        var resultStr = Assert.IsType<string>(captured.Result);
        Assert.Equal("42", resultStr);
    }

    [Fact]
    public async Task ToolReturnsBoolean_LlmReceivesSerializedValue()
    {
        var captured = await RunToolAndCaptureFunctionResult(
            "check", () => (object)true, _ct);

        var resultStr = Assert.IsType<string>(captured.Result);
        Assert.Equal("true", resultStr);
    }

    [Fact]
    public async Task ToolReturnsNestedObject_LlmReceivesFullJson()
    {
        var toolResult = new BatchResult("dependency injection",
        [
            new SearchResult("DI Guide", "https://learn.microsoft.com/di"),
            new SearchResult("Autofac", "https://autofac.org"),
        ]);
        var captured = await RunToolAndCaptureFunctionResult("batch", () => toolResult, _ct);

        var json = Assert.IsType<string>(captured.Result);
        Assert.Contains("dependency injection", json);
        Assert.Contains("https://learn.microsoft.com/di", json);
        Assert.Contains("https://autofac.org", json);
        Assert.DoesNotContain("BatchResult", json);
    }

    [Fact]
    public async Task ToolReturnsObjectWithToString_LlmReceivesJsonNotToString()
    {
        var toolResult = new TypeWithCustomToString(42, "test");
        var captured = await RunToolAndCaptureFunctionResult("custom", () => toolResult, _ct);

        var json = Assert.IsType<string>(captured.Result);
        Assert.DoesNotContain("CUSTOM_TO_STRING", json);
        Assert.Contains("42", json);
        Assert.Contains("test", json);
    }

    /// <summary>
    /// Runs a tool call through the iterative loop in OneRoundTrip mode and
    /// captures the <see cref="FunctionResultContent"/> that gets sent back to
    /// the LLM as the tool result message.
    /// </summary>
    private static async Task<FunctionResultContent> RunToolAndCaptureFunctionResult(
        string toolName,
        Func<object?> toolExecute,
        CancellationToken cancellationToken)
    {
        var tool = AIFunctionFactory.Create(
            () => toolExecute(),
            new AIFunctionFactoryOptions { Name = toolName });

        List<ChatMessage>? capturedMessages = null;
        var callCount = 0;

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ChatMessage> msgs, ChatOptions? _, CancellationToken _) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // First call: LLM requests tool call
                    return new ChatResponse([new ChatMessage(ChatRole.Assistant,
                        [new FunctionCallContent("call-1", toolName, null)])]);
                }

                // Second call: LLM receives tool result — capture the messages
                capturedMessages = msgs.ToList();
                return new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, "Done.")]);
            });

        var accessor = new Mock<IChatClientAccessor>();
        accessor.Setup(a => a.ChatClient).Returns(mockChat.Object);

        var loop = new IterativeAgentLoop(accessor.Object, null, null);

        var options = new IterativeLoopOptions
        {
            Instructions = "Test",
            PromptFactory = _ => "Go",
            Tools = [tool],
            MaxIterations = 1,
            ToolResultMode = ToolResultMode.OneRoundTrip,
            LoopName = "test",
        };

        await loop.RunAsync(options, new IterativeContext
        {
            Workspace = new InMemoryWorkspace(),
        }, cancellationToken);

        Assert.NotNull(capturedMessages);
        Assert.True(capturedMessages!.Count >= 4,
            $"Expected at least 4 messages (system + user + assistant + tool), got {capturedMessages.Count}");

        // Find the tool result message
        var toolMessage = capturedMessages.FirstOrDefault(m => m.Role == ChatRole.Tool);
        Assert.NotNull(toolMessage);

        var functionResult = toolMessage!.Contents.OfType<FunctionResultContent>().Single();
        Assert.Equal("call-1", functionResult.CallId);

        return functionResult;
    }

    public sealed record SearchResult(string Title, string Url);

    public sealed record BatchResult(string Query, SearchResult[] Results);

    public sealed class TypeWithCustomToString
    {
        public int Value { get; }
        public string Name { get; }

        public TypeWithCustomToString(int value, string name)
        {
            Value = value;
            Name = name;
        }

        public override string ToString() => "CUSTOM_TO_STRING_OUTPUT";
    }
}
