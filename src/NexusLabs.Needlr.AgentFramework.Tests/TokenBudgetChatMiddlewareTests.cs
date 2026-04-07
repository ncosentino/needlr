using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Needlr.AgentFramework.Budget;
using NexusLabs.Needlr.AgentFramework.Workflows.Budget;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class TokenBudgetChatMiddlewareTests
{
    private static (TokenBudgetChatMiddleware middleware, TokenBudgetTracker tracker, Mock<IChatClient> inner) CreateMiddleware()
    {
        var tracker = new TokenBudgetTracker();
        var inner = new Mock<IChatClient>();
        var middleware = new TokenBudgetChatMiddleware(inner.Object, tracker);
        return (middleware, tracker, inner);
    }

    // -------------------------------------------------------------------------
    // Pre-call gate: budget already exhausted
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetResponseAsync_WhenBudgetAlreadyExhausted_ThrowsBeforeCallingInner()
    {
        var (middleware, tracker, inner) = CreateMiddleware();

        using var scope = tracker.BeginScope(100);
        tracker.Record(100); // exhaust budget

        await Assert.ThrowsAsync<TokenBudgetExceededException>(() =>
            middleware.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "hello")],
                options: null,
                CancellationToken.None));

        // Inner should never have been called
        inner.Verify(
            c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // -------------------------------------------------------------------------
    // Post-call: records tokens from response usage
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetResponseAsync_RecordsTokensFromUsage()
    {
        var (middleware, tracker, inner) = CreateMiddleware();

        var response = new ChatResponse([new ChatMessage(ChatRole.Assistant, "hi")])
        {
            Usage = new UsageDetails { TotalTokenCount = 50 }
        };

        inner
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        using var scope = tracker.BeginScope(1000);

        await middleware.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hello")],
            options: null,
            CancellationToken.None);

        Assert.Equal(50, tracker.CurrentTokens);
    }

    // -------------------------------------------------------------------------
    // Post-call: throws when response pushes over budget
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetResponseAsync_WhenResponseExceedsBudget_Throws()
    {
        var (middleware, tracker, inner) = CreateMiddleware();

        var response = new ChatResponse([new ChatMessage(ChatRole.Assistant, "hi")])
        {
            Usage = new UsageDetails { TotalTokenCount = 200 }
        };

        inner
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        using var scope = tracker.BeginScope(100);

        await Assert.ThrowsAsync<TokenBudgetExceededException>(() =>
            middleware.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "hello")],
                options: null,
                CancellationToken.None));
    }

    // -------------------------------------------------------------------------
    // No scope active — passes through without tracking
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetResponseAsync_WithoutScope_PassesThroughWithoutTracking()
    {
        var (middleware, tracker, inner) = CreateMiddleware();

        var response = new ChatResponse([new ChatMessage(ChatRole.Assistant, "hi")])
        {
            Usage = new UsageDetails { TotalTokenCount = 999 }
        };

        inner
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // No scope — should not throw even with high token count
        var result = await middleware.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hello")],
            options: null,
            CancellationToken.None);

        Assert.NotNull(result);
        // Tracker records the call but no scope means no budget check
        Assert.Equal(0, tracker.CurrentTokens); // no scope → Record is a no-op
    }

    // -------------------------------------------------------------------------
    // No usage in response — does not update tracker
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetResponseAsync_WhenUsageIsNull_DoesNotRecordTokens()
    {
        var (middleware, tracker, inner) = CreateMiddleware();

        var response = new ChatResponse([new ChatMessage(ChatRole.Assistant, "hi")]);
        // Usage is null by default

        inner
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        using var scope = tracker.BeginScope(1000);

        await middleware.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hello")],
            options: null,
            CancellationToken.None);

        Assert.Equal(0, tracker.CurrentTokens);
    }

    // -------------------------------------------------------------------------
    // Constructor validation
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithNullTracker_ThrowsArgumentNull()
    {
        var inner = new Mock<IChatClient>();

        Assert.Throws<ArgumentNullException>(() =>
            new TokenBudgetChatMiddleware(inner.Object, null!));
    }
}
