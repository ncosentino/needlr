using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Needlr.AgentFramework.Budget;
using NexusLabs.Needlr.AgentFramework.Workflows.Budget;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Tests for the decoupled token tracking/recording/enforcement pipeline.
/// </summary>
public sealed class TokenTrackingTests
{
    [Fact]
    public async Task UsingTokenTracking_Alone_RecordsTokens_NoEnforcement()
    {
        var tracker = new TokenBudgetTracker();

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "hi")])
            {
                Usage = new UsageDetails
                {
                    InputTokenCount = 500,
                    OutputTokenCount = 200,
                    TotalTokenCount = 700,
                },
            });

        // Wire ONLY the recording middleware — no enforcement
        var client = new TokenUsageRecordingMiddleware(mockChat.Object, tracker);

        using (tracker.BeginTrackingScope())
        {
            // Make a call that would exceed any reasonable budget
            await client.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "hello")],
                cancellationToken: TestContext.Current.CancellationToken);

            // Tokens should be recorded
            Assert.Equal(700, tracker.CurrentTokens);
            Assert.Equal(500, tracker.CurrentInputTokens);
            Assert.Equal(200, tracker.CurrentOutputTokens);

            // No enforcement — no cancellation, no exception
            Assert.False(tracker.BudgetCancellationToken.IsCancellationRequested);
        }
    }

    [Fact]
    public void BeginTrackingScope_NeverEnforces()
    {
        var tracker = new TokenBudgetTracker();

        using (tracker.BeginTrackingScope())
        {
            // Record enormous usage
            tracker.Record(1_000_000, 500_000);

            Assert.Equal(1_500_000, tracker.CurrentTokens);
            Assert.Null(tracker.MaxTokens);
            Assert.Null(tracker.MaxInputTokens);
            Assert.Null(tracker.MaxOutputTokens);
            Assert.False(tracker.BudgetCancellationToken.IsCancellationRequested);
        }
    }

    [Fact]
    public void BeginTrackingScope_ChildWithLimits_ChildEnforces_ParentDoesNot()
    {
        var tracker = new TokenBudgetTracker();

        using (tracker.BeginTrackingScope())
        {
            var parentToken = tracker.BudgetCancellationToken;

            using (tracker.BeginChildScope("stage-1", 100))
            {
                tracker.Record(200);

                // Child should be cancelled
                Assert.True(tracker.BudgetCancellationToken.IsCancellationRequested);
            }

            // Parent should NOT be cancelled — it has no limits
            Assert.False(parentToken.IsCancellationRequested);

            // Parent should see the rolled-up usage
            Assert.Equal(200, tracker.CurrentTokens);
        }
    }

    [Fact]
    public async Task TotalTokenCountOnly_Provider_RecordsCorrectly()
    {
        var tracker = new TokenBudgetTracker();

        // Provider returns ONLY TotalTokenCount, no input/output split
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "hi")])
            {
                Usage = new UsageDetails { TotalTokenCount = 350 },
            });

        var client = new TokenUsageRecordingMiddleware(mockChat.Object, tracker);

        using (tracker.BeginTrackingScope())
        {
            await client.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "hello")],
                cancellationToken: TestContext.Current.CancellationToken);

            // TotalTokenCount fallback should record as total only
            Assert.Equal(350, tracker.CurrentTokens);
            Assert.Equal(0, tracker.CurrentInputTokens);
            Assert.Equal(0, tracker.CurrentOutputTokens);
        }
    }

    [Fact]
    public async Task RecordingMiddleware_NoUsage_DoesNotRecord()
    {
        var tracker = new TokenBudgetTracker();

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "hi")]));

        var client = new TokenUsageRecordingMiddleware(mockChat.Object, tracker);

        using (tracker.BeginTrackingScope())
        {
            await client.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "hello")],
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(0, tracker.CurrentTokens);
        }
    }

    [Fact]
    public async Task RecordingAndBudget_Together_RecordsAndEnforces()
    {
        var tracker = new TokenBudgetTracker();
        var progressAccessor = new Progress.ProgressReporterAccessor();

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "hi")])
            {
                Usage = new UsageDetails
                {
                    InputTokenCount = 150,
                    OutputTokenCount = 60,
                    TotalTokenCount = 210,
                },
            });

        // Wire both: recording (inner) → budget (outer)
        var recording = new TokenUsageRecordingMiddleware(mockChat.Object, tracker);
        var budget = new TokenBudgetChatMiddleware(recording, tracker, progressAccessor);

        using (tracker.BeginScope(200))
        {
            // Should throw because 210 > 200
            var ex = await Assert.ThrowsAsync<OperationCanceledException>(() =>
                budget.GetResponseAsync(
                    [new ChatMessage(ChatRole.User, "hello")],
                    options: null,
                    TestContext.Current.CancellationToken));

            Assert.IsType<TokenBudgetExceededException>(ex.InnerException);
            Assert.Equal(210, tracker.CurrentTokens);
        }
    }
}
