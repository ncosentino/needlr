using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Needlr.AgentFramework.Progress;
using NexusLabs.Needlr.AgentFramework.Workflows.Budget;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Tests for <see cref="ContextWindowGuardMiddleware"/>.
/// </summary>
public sealed class ContextWindowGuardMiddlewareTests
{
    [Fact]
    public async Task GetResponseAsync_UnderThreshold_NoEventEmitted()
    {
        var events = new List<IProgressEvent>();
        var (middleware, _) = CreateMiddleware(maxTokens: 10000, events: events);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "Short system prompt"),
            new(ChatRole.User, "Hello"),
        };

        await middleware.GetResponseAsync(messages, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(events);
    }

    [Fact]
    public async Task GetResponseAsync_AtWarningThreshold_EmitsBudgetUpdatedEvent()
    {
        var events = new List<IProgressEvent>();
        // 200 token limit, 80% threshold = 160 tokens
        // "x" * 680 chars / 4 chars per token = 170 estimated tokens → between 160 and 200
        var (middleware, _) = CreateMiddleware(maxTokens: 200, events: events);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, new string('x', 680)),
        };

        await middleware.GetResponseAsync(messages, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(events);
        Assert.IsType<BudgetUpdatedEvent>(events[0]);
    }

    [Fact]
    public async Task GetResponseAsync_OverLimit_EmitsBudgetExceededEvent()
    {
        var events = new List<IProgressEvent>();
        var (middleware, _) = CreateMiddleware(maxTokens: 50, events: events);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, new string('x', 400)),
        };

        await middleware.GetResponseAsync(messages, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(events);
        var exceeded = Assert.IsType<BudgetExceededEvent>(events[0]);
        Assert.Equal("context_window", exceeded.LimitType);
        Assert.True(exceeded.CurrentValue > exceeded.MaxValue);
    }

    [Fact]
    public async Task GetResponseAsync_PruneOnOverflow_RemovesOldMessages()
    {
        var events = new List<IProgressEvent>();
        var (middleware, _) = CreateMiddleware(maxTokens: 20, events: events, pruneOnOverflow: true);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "System"),
            new(ChatRole.User, new string('a', 100)),
            new(ChatRole.Assistant, new string('b', 100)),
            new(ChatRole.User, "Latest question"),
        };

        var originalCount = messages.Count;
        await middleware.GetResponseAsync(messages, cancellationToken: TestContext.Current.CancellationToken);

        // Pruning should have removed some messages
        Assert.True(messages.Count < originalCount, $"Expected pruning to reduce from {originalCount} messages");
        // System message should be preserved
        Assert.Equal(ChatRole.System, messages[0].Role);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_OverLimit_EmitsBudgetExceededEvent()
    {
        var events = new List<IProgressEvent>();
        var (middleware, mockInner) = CreateMiddleware(maxTokens: 50, events: events);

        // Set up streaming response
        mockInner
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable([
                new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent("hi")] },
            ]));

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, new string('x', 400)),
        };

        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in middleware.GetStreamingResponseAsync(
            messages, cancellationToken: TestContext.Current.CancellationToken))
        {
            updates.Add(update);
        }

        Assert.Single(events);
        Assert.IsType<BudgetExceededEvent>(events[0]);
        Assert.Single(updates);
    }

    [Fact]
    public async Task GetResponseAsync_FunctionCallContent_IncludedInEstimate()
    {
        var events = new List<IProgressEvent>();
        // Very tight limit
        var (middleware, _) = CreateMiddleware(maxTokens: 10, events: events);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.Assistant,
            [
                new FunctionCallContent("c1", "web_search",
                    new Dictionary<string, object?> { ["query"] = new string('q', 200) }),
            ]),
        };

        await middleware.GetResponseAsync(messages, cancellationToken: TestContext.Current.CancellationToken);

        // Should trigger an event due to function call content being counted
        Assert.NotEmpty(events);
    }

    #region Helpers

    private static (ContextWindowGuardMiddleware middleware, Mock<IChatClient> mockInner) CreateMiddleware(
        int maxTokens,
        List<IProgressEvent> events,
        double warningThreshold = 0.8,
        bool pruneOnOverflow = false)
    {
        var mockInner = new Mock<IChatClient>();
        mockInner
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "ok")]));

        var mockReporter = new Mock<IProgressReporter>();
        mockReporter.Setup(r => r.Report(It.IsAny<IProgressEvent>()))
            .Callback<IProgressEvent>(e => events.Add(e));
        mockReporter.Setup(r => r.WorkflowId).Returns("test");
        mockReporter.Setup(r => r.NextSequence()).Returns(() => 1);

        var mockAccessor = new Mock<IProgressReporterAccessor>();
        mockAccessor.Setup(a => a.Current).Returns(mockReporter.Object);

        return (new ContextWindowGuardMiddleware(
            mockInner.Object, maxTokens, mockAccessor.Object, warningThreshold, pruneOnOverflow),
            mockInner);
    }

    private static async IAsyncEnumerable<T> AsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }

    #endregion
}
