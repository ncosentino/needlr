using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Tests for the streaming capture path of <c>DiagnosticsAgentRunMiddleware</c>.
/// Mirrors the non-streaming <see cref="DiagnosticsAgentRunMiddleware.HandleAsync"/>
/// so consumers get replay-grade diagnostics from streaming agent runs too.
/// </summary>
public sealed class DiagnosticsAgentRunMiddlewareStreamingTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task HandleStreamingAsync_SuccessfulStream_RecordsDiagnosticsWithCounts()
    {
        var captured = new List<IAgentRunDiagnostics>();
        var (middleware, innerAgent, _) = CreateMiddleware(
            captured,
            new[]
            {
                new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent("hel")], MessageId = "m-1" },
                new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent("lo")], MessageId = "m-1" },
            });

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "hi"),
            new(ChatRole.User, "there"),
        };

        var received = new List<AgentResponseUpdate>();
        await foreach (var u in middleware.HandleStreamingAsync(messages, session: null, options: null, innerAgent, _ct))
        {
            received.Add(u);
        }

        Assert.NotEmpty(received);
        Assert.Single(captured);
        var diag = captured[0];
        Assert.True(diag.Succeeded, "Expected successful streaming run to be marked succeeded");
        Assert.Null(diag.ErrorMessage);
        Assert.Equal(2, diag.TotalInputMessages);
        Assert.Equal(1, diag.TotalOutputMessages);
    }

    [Fact]
    public async Task HandleStreamingAsync_MidStreamException_RecordsFailureAndRethrows()
    {
        var captured = new List<IAgentRunDiagnostics>();
        var (middleware, innerAgent, _) = CreateMiddleware(
            captured,
            ThrowingStream(
                new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent("partial")], MessageId = "m-1" },
                new InvalidOperationException("stream boom")));

        var messages = new List<ChatMessage> { new(ChatRole.User, "hi") };

        var received = new List<AgentResponseUpdate>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var u in middleware.HandleStreamingAsync(messages, session: null, options: null, innerAgent, _ct))
            {
                received.Add(u);
            }
        });

        Assert.Equal("stream boom", ex.Message);
        Assert.NotEmpty(received);
        Assert.Single(captured);
        var diag = captured[0];
        Assert.False(diag.Succeeded, "Expected failed streaming run to be marked not succeeded");
        Assert.Equal("stream boom", diag.ErrorMessage);
        Assert.Equal(1, diag.TotalInputMessages);
    }

    [Fact]
    public async Task HandleStreamingAsync_YieldsUpdatesInOrderInRealTime()
    {
        var captured = new List<IAgentRunDiagnostics>();
        var (middleware, innerAgent, _) = CreateMiddleware(
            captured,
            new[]
            {
                new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent("a")], MessageId = "m-1" },
                new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent("b")], MessageId = "m-1" },
                new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent("c")], MessageId = "m-1" },
            });

        var messages = new List<ChatMessage> { new(ChatRole.User, "hi") };
        var texts = new List<string>();

        await foreach (var u in middleware.HandleStreamingAsync(messages, session: null, options: null, innerAgent, _ct))
        {
            texts.Add(u.Text ?? string.Empty);
        }

        var combined = string.Concat(texts);
        Assert.Equal("abc", combined);
    }

    [Fact]
    public async Task HandleStreamingAsync_UsesInnerAgentNameWhenPresent()
    {
        var captured = new List<IAgentRunDiagnostics>();
        var (middleware, innerAgent, _) = CreateMiddleware(
            captured,
            new[]
            {
                new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent("ok")], MessageId = "m-1" },
            },
            innerAgentName: "resolved-name",
            fallbackName: "fallback-name");

        var messages = new List<ChatMessage> { new(ChatRole.User, "hi") };

        await foreach (var _u in middleware.HandleStreamingAsync(messages, session: null, options: null, innerAgent, _ct))
        {
        }

        Assert.Single(captured);
        Assert.Equal("resolved-name", captured[0].AgentName);
    }

    [Fact]
    public async Task HandleStreamingAsync_RecordsInputMessagesLosslessly()
    {
        var captured = new List<IAgentRunDiagnostics>();
        var (middleware, innerAgent, _) = CreateMiddleware(
            captured,
            new[]
            {
                new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent("ok")], MessageId = "m-1" },
            });

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "first"),
            new(ChatRole.User, "second"),
        };

        await foreach (var _u in middleware.HandleStreamingAsync(messages, session: null, options: null, innerAgent, _ct))
        {
        }

        Assert.Single(captured);
        var diag = captured[0];
        Assert.Equal(2, diag.InputMessages.Count);
        Assert.Equal("first", diag.InputMessages[0].Text);
        Assert.Equal("second", diag.InputMessages[1].Text);
        Assert.Equal(ChatRole.User, diag.InputMessages[0].Role);
    }

    [Fact]
    public async Task HandleStreamingAsync_AggregatesOutputResponseByMessageId()
    {
        var captured = new List<IAgentRunDiagnostics>();
        var (middleware, innerAgent, _) = CreateMiddleware(
            captured,
            new[]
            {
                new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent("hel")], MessageId = "m-1" },
                new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent("lo")], MessageId = "m-1" },
                new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent(" world")], MessageId = "m-2" },
            });

        var messages = new List<ChatMessage> { new(ChatRole.User, "hi") };

        await foreach (var _u in middleware.HandleStreamingAsync(messages, session: null, options: null, innerAgent, _ct))
        {
        }

        Assert.Single(captured);
        var diag = captured[0];
        Assert.NotNull(diag.OutputResponse);
        Assert.Equal(2, diag.OutputResponse!.Messages.Count);
        Assert.Equal("hello", diag.OutputResponse.Messages[0].Text);
        Assert.Equal(" world", diag.OutputResponse.Messages[1].Text);
    }

    [Fact]
    public async Task HandleStreamingAsync_MidStreamFailure_RecordsPartialOutputResponse()
    {
        var captured = new List<IAgentRunDiagnostics>();
        var (middleware, innerAgent, _) = CreateMiddleware(
            captured,
            ThrowingStream(
                new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent("partial")], MessageId = "m-1" },
                new InvalidOperationException("stream boom")));

        var messages = new List<ChatMessage> { new(ChatRole.User, "hi") };

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _u in middleware.HandleStreamingAsync(messages, session: null, options: null, innerAgent, _ct))
            {
            }
        });

        Assert.Single(captured);
        var diag = captured[0];
        Assert.False(diag.Succeeded, "Expected failed streaming run to be marked not succeeded");
        Assert.NotNull(diag.OutputResponse);
        Assert.Single(diag.OutputResponse!.Messages);
        Assert.Equal("partial", diag.OutputResponse.Messages[0].Text);
        Assert.Single(diag.InputMessages);
        Assert.Equal("hi", diag.InputMessages[0].Text);
    }

    private static (DiagnosticsAgentRunMiddleware middleware, AIAgent innerAgent, Mock<IChatClient> mockChat)
        CreateMiddleware(
            List<IAgentRunDiagnostics> captured,
            IEnumerable<ChatResponseUpdate> updates,
            string innerAgentName = "inner-agent",
            string fallbackName = "Agent")
        => CreateMiddleware(captured, AsyncEnumerable(updates), innerAgentName, fallbackName);

    private static (DiagnosticsAgentRunMiddleware middleware, AIAgent innerAgent, Mock<IChatClient> mockChat)
        CreateMiddleware(
            List<IAgentRunDiagnostics> captured,
            IAsyncEnumerable<ChatResponseUpdate> updates,
            string innerAgentName = "inner-agent",
            string fallbackName = "Agent")
    {
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(updates);

        var innerAgent = mockChat.Object.AsAIAgent(name: innerAgentName, instructions: "test-instructions");

        var writer = new Mock<IAgentDiagnosticsWriter>();
        writer
            .Setup(w => w.Set(It.IsAny<IAgentRunDiagnostics>()))
            .Callback<IAgentRunDiagnostics>(captured.Add);

        var middleware = new DiagnosticsAgentRunMiddleware(
            fallbackName,
            writer.Object,
            new AgentMetrics());

        return (middleware, innerAgent, mockChat);
    }

    private static async IAsyncEnumerable<T> AsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> ThrowingStream(
        ChatResponseUpdate firstUpdate,
        Exception toThrow)
    {
        yield return firstUpdate;
        await Task.CompletedTask;
        throw toThrow;
    }
}
