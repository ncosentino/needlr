using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Progress;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Tests for the streaming capture path of <see cref="DiagnosticsChatClientMiddleware"/>.
/// </summary>
public sealed class DiagnosticsChatClientMiddlewareStreamingTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task HandleStreamingAsync_SuccessfulStream_YieldsAllUpdatesInOrder()
    {
        var (middleware, mockInner, _) = CreateMiddleware();

        var updates = new[]
        {
            new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent("hello ")] },
            new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent("world")] },
        };

        mockInner
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable(updates));

        var messages = new List<ChatMessage> { new(ChatRole.User, "hi") };

        var received = new List<ChatResponseUpdate>();
        await foreach (var u in middleware.HandleStreamingAsync(messages, options: null, mockInner.Object, _ct))
        {
            received.Add(u);
        }

        Assert.Equal(2, received.Count);
        Assert.Same(updates[0], received[0]);
        Assert.Same(updates[1], received[1]);
    }

    [Fact]
    public async Task HandleStreamingAsync_SuccessfulStream_CapturesDiagnosticsWithAggregatedResponse()
    {
        var (middleware, mockInner, _) = CreateMiddleware();

        mockInner
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable(new[]
            {
                new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent("hello ")], ModelId = "m-1" },
                new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent("world")], ModelId = "m-1" },
            }));

        var messages = new List<ChatMessage> { new(ChatRole.User, "hi") };

        await foreach (var _ in middleware.HandleStreamingAsync(messages, options: null, mockInner.Object, _ct))
        {
        }

        var drained = middleware.DrainCompletions();
        Assert.Single(drained);
        var diagnostics = drained[0];
        Assert.True(diagnostics.Succeeded, "Expected streaming diagnostics to record success");
        Assert.Null(diagnostics.ErrorMessage);
        Assert.Equal("m-1", diagnostics.Model);
        Assert.NotNull(diagnostics.Response);
        var aggregatedText = diagnostics.Response!.Text;
        Assert.Equal("hello world", aggregatedText);
        Assert.NotNull(diagnostics.RequestMessages);
        Assert.Single(diagnostics.RequestMessages!);
        Assert.Equal(1, diagnostics.InputMessageCount);
    }

    [Fact]
    public async Task HandleStreamingAsync_ErrorMidStream_CapturesPartialResponseAndRethrows()
    {
        var (middleware, mockInner, _) = CreateMiddleware();

        mockInner
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(ThrowingStream(
                new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent("partial ")] },
                new InvalidOperationException("stream blew up")));

        var messages = new List<ChatMessage> { new(ChatRole.User, "hi") };

        var received = new List<ChatResponseUpdate>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var u in middleware.HandleStreamingAsync(messages, options: null, mockInner.Object, _ct))
            {
                received.Add(u);
            }
        });

        Assert.Equal("stream blew up", ex.Message);
        Assert.Single(received);

        var drained = middleware.DrainCompletions();
        Assert.Single(drained);
        var diagnostics = drained[0];
        Assert.False(diagnostics.Succeeded, "Expected streaming diagnostics to record failure on mid-stream error");
        Assert.Equal("stream blew up", diagnostics.ErrorMessage);
        Assert.NotNull(diagnostics.Response);
        Assert.Contains("partial", diagnostics.Response!.Text);
        Assert.NotNull(diagnostics.RequestMessages);
        Assert.Single(diagnostics.RequestMessages!);
    }

    [Fact]
    public async Task HandleStreamingAsync_EmitsStartedAndCompletedProgressEvents()
    {
        var events = new List<IProgressEvent>();
        var (middleware, mockInner, _) = CreateMiddleware(events);

        mockInner
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable(new[]
            {
                new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent("ok")], ModelId = "m" },
            }));

        var messages = new List<ChatMessage> { new(ChatRole.User, "hi") };

        await foreach (var _ in middleware.HandleStreamingAsync(messages, options: null, mockInner.Object, _ct))
        {
        }

        Assert.Equal(2, events.Count);
        Assert.IsType<LlmCallStartedEvent>(events[0]);
        Assert.IsType<LlmCallCompletedEvent>(events[1]);
    }

    [Fact]
    public async Task HandleStreamingAsync_PopulatesCachedAndReasoningTokens_FromUsageContentFirstClassProperties()
    {
        var (middleware, mockInner, _) = CreateMiddleware();

        mockInner
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable(new[]
            {
                new ChatResponseUpdate
                {
                    Role = ChatRole.Assistant,
                    Contents = [new TextContent("answer")],
                    ModelId = "azure-gpt-4.1",
                },
                new ChatResponseUpdate
                {
                    Role = ChatRole.Assistant,
                    Contents = [new UsageContent(new UsageDetails
                    {
                        InputTokenCount = 5000,
                        OutputTokenCount = 200,
                        TotalTokenCount = 5200,
                        CachedInputTokenCount = 3000,
                        ReasoningTokenCount = 50,
                    })],
                    ModelId = "azure-gpt-4.1",
                },
            }));

        using var builder = AgentRunDiagnosticsBuilder.StartNew("Agent");
        var messages = new List<ChatMessage> { new(ChatRole.User, "hi") };
        await foreach (var _ in middleware.HandleStreamingAsync(messages, options: null, mockInner.Object, _ct))
        {
        }

        var diag = builder.Build();
        var completion = Assert.Single(diag.ChatCompletions);
        Assert.NotNull(completion.Response);
        Assert.NotNull(completion.Response!.Usage);
        Assert.Equal(3000, completion.Response.Usage!.CachedInputTokenCount);
        Assert.Equal(50, completion.Response.Usage!.ReasoningTokenCount);
        Assert.Equal(5000, completion.Tokens.InputTokens);
        Assert.Equal(200, completion.Tokens.OutputTokens);
        Assert.Equal(5200, completion.Tokens.TotalTokens);
        Assert.Equal(3000, completion.Tokens.CachedInputTokens);
        Assert.Equal(50, completion.Tokens.ReasoningTokens);
    }

    [Fact]
    public async Task HandleStreamingAsync_FallsBackToAdditionalCounts_WhenFirstClassPropertiesAreNull()
    {
        var (middleware, mockInner, _) = CreateMiddleware();

        mockInner
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable(new[]
            {
                new ChatResponseUpdate
                {
                    Role = ChatRole.Assistant,
                    Contents = [new TextContent("answer")],
                    ModelId = "custom",
                },
                new ChatResponseUpdate
                {
                    Role = ChatRole.Assistant,
                    Contents = [new UsageContent(new UsageDetails
                    {
                        InputTokenCount = 100,
                        OutputTokenCount = 50,
                        TotalTokenCount = 150,
                        AdditionalCounts = new AdditionalPropertiesDictionary<long>
                        {
                            ["CachedInputTokens"] = 42,
                            ["ReasoningTokens"] = 7,
                        },
                    })],
                    ModelId = "custom",
                },
            }));

        using var builder = AgentRunDiagnosticsBuilder.StartNew("Agent");
        var messages = new List<ChatMessage> { new(ChatRole.User, "hi") };
        await foreach (var _ in middleware.HandleStreamingAsync(messages, options: null, mockInner.Object, _ct))
        {
        }

        var diag = builder.Build();
        var completion = Assert.Single(diag.ChatCompletions);
        Assert.Equal(42, completion.Tokens.CachedInputTokens);
        Assert.Equal(7, completion.Tokens.ReasoningTokens);
    }

    [Fact]
    public async Task HandleStreamingAsync_StreamFailureAfterUsageChunk_PreservesPartialTokenUsage()
    {
        var (middleware, mockInner, _) = CreateMiddleware();

        var updates = new[]
        {
            new ChatResponseUpdate
            {
                Role = ChatRole.Assistant,
                Contents = [new TextContent("partial answer")],
                ModelId = "azure-gpt-4.1",
            },
            new ChatResponseUpdate
            {
                Role = ChatRole.Assistant,
                Contents = [new UsageContent(new UsageDetails
                {
                    InputTokenCount = 5000,
                    OutputTokenCount = 200,
                    TotalTokenCount = 5200,
                    CachedInputTokenCount = 3000,
                    ReasoningTokenCount = 50,
                })],
                ModelId = "azure-gpt-4.1",
            },
        };

        mockInner
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(ThrowingStreamMany(updates, new InvalidOperationException("late stream failure")));

        using var builder = AgentRunDiagnosticsBuilder.StartNew("Agent");
        var messages = new List<ChatMessage> { new(ChatRole.User, "hi") };

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in middleware.HandleStreamingAsync(messages, options: null, mockInner.Object, _ct))
            {
            }
        });

        var diag = builder.Build();
        var completion = Assert.Single(diag.ChatCompletions);
        Assert.False(completion.Succeeded);
        Assert.Equal("late stream failure", completion.ErrorMessage);
        Assert.Equal(5000, completion.Tokens.InputTokens);
        Assert.Equal(200, completion.Tokens.OutputTokens);
        Assert.Equal(5200, completion.Tokens.TotalTokens);
        Assert.Equal(3000, completion.Tokens.CachedInputTokens);
        Assert.Equal(50, completion.Tokens.ReasoningTokens);
        Assert.Equal(1, completion.InputMessageCount);
    }

    [Fact]
    public async Task HandleStreamingAsync_ErrorMidStream_EmitsFailedProgressEvent()
    {
        var events = new List<IProgressEvent>();
        var (middleware, mockInner, _) = CreateMiddleware(events);

        mockInner
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(ThrowingStream(
                new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent("x")] },
                new InvalidOperationException("boom")));

        var messages = new List<ChatMessage> { new(ChatRole.User, "hi") };

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in middleware.HandleStreamingAsync(messages, options: null, mockInner.Object, _ct))
            {
            }
        });

        Assert.Equal(2, events.Count);
        Assert.IsType<LlmCallStartedEvent>(events[0]);
        var failed = Assert.IsType<LlmCallFailedEvent>(events[1]);
        Assert.Equal("boom", failed.ErrorMessage);
    }

    private static (DiagnosticsChatClientMiddleware middleware, Mock<IChatClient> mockInner, Mock<IProgressReporter> mockReporter)
        CreateMiddleware(List<IProgressEvent>? events = null)
    {
        var mockInner = new Mock<IChatClient>();

        var mockReporter = new Mock<IProgressReporter>();
        mockReporter.Setup(r => r.WorkflowId).Returns("test-wf");
        mockReporter.Setup(r => r.AgentId).Returns("test-agent");
        mockReporter.Setup(r => r.Depth).Returns(0);
        mockReporter.Setup(r => r.NextSequence()).Returns(() => 1);
        if (events is not null)
        {
            mockReporter.Setup(r => r.Report(It.IsAny<IProgressEvent>()))
                .Callback<IProgressEvent>(events.Add);
        }

        var mockAccessor = new Mock<IProgressReporterAccessor>();
        mockAccessor.Setup(a => a.Current).Returns(mockReporter.Object);

        var middleware = new DiagnosticsChatClientMiddleware(new AgentMetrics(), mockAccessor.Object);
        return (middleware, mockInner, mockReporter);
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

    private static async IAsyncEnumerable<ChatResponseUpdate> ThrowingStreamMany(
        IEnumerable<ChatResponseUpdate> updates,
        Exception toThrow)
    {
        foreach (var update in updates)
        {
            yield return update;
        }

        await Task.CompletedTask;
        throw toThrow;
    }
}
