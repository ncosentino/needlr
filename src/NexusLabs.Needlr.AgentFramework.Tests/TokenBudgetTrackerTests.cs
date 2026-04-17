using NexusLabs.Needlr.AgentFramework.Budget;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class TokenBudgetTrackerTests
{
    // -------------------------------------------------------------------------
    // BeginScope
    // -------------------------------------------------------------------------

    [Fact]
    public void BeginScope_SetsMaxTokens()
    {
        var tracker = new TokenBudgetTracker();

        using var scope = tracker.BeginScope(1000);

        Assert.Equal(1000, tracker.MaxTokens);
    }

    [Fact]
    public void BeginScope_InitializesCurrentTokensToZero()
    {
        var tracker = new TokenBudgetTracker();

        using var scope = tracker.BeginScope(1000);

        Assert.Equal(0, tracker.CurrentTokens);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void BeginScope_WithNonPositiveBudget_ThrowsArgumentOutOfRange(long maxTokens)
    {
        var tracker = new TokenBudgetTracker();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            tracker.BeginScope(maxTokens));
    }

    // -------------------------------------------------------------------------
    // Record
    // -------------------------------------------------------------------------

    [Fact]
    public void Record_AccumulatesTokens()
    {
        var tracker = new TokenBudgetTracker();
        using var scope = tracker.BeginScope(5000);

        tracker.Record(100);
        tracker.Record(200);
        tracker.Record(300);

        Assert.Equal(600, tracker.CurrentTokens);
    }

    [Fact]
    public void Record_WithoutActiveScope_DoesNotThrow()
    {
        var tracker = new TokenBudgetTracker();

        // No scope — Record is a no-op
        tracker.Record(100);

        Assert.Equal(0, tracker.CurrentTokens);
    }

    // -------------------------------------------------------------------------
    // Scope disposal
    // -------------------------------------------------------------------------

    [Fact]
    public void Dispose_ClearsScope()
    {
        var tracker = new TokenBudgetTracker();

        var scope = tracker.BeginScope(1000);
        tracker.Record(500);
        scope.Dispose();

        Assert.Null(tracker.MaxTokens);
        Assert.Equal(0, tracker.CurrentTokens);
    }

    // -------------------------------------------------------------------------
    // No active scope
    // -------------------------------------------------------------------------

    [Fact]
    public void CurrentTokens_WithoutScope_ReturnsZero()
    {
        var tracker = new TokenBudgetTracker();

        Assert.Equal(0, tracker.CurrentTokens);
    }

    [Fact]
    public void MaxTokens_WithoutScope_ReturnsNull()
    {
        var tracker = new TokenBudgetTracker();

        Assert.Null(tracker.MaxTokens);
    }

    // -------------------------------------------------------------------------
    // AsyncLocal isolation
    // -------------------------------------------------------------------------

    // -------------------------------------------------------------------------
    // Granular budgets: input, output, total
    // -------------------------------------------------------------------------

    [Fact]
    public void BeginScope_WithInputBudget_SetsMaxInputTokens()
    {
        var tracker = new TokenBudgetTracker();

        using var scope = tracker.BeginScope(maxInputTokens: 500);

        Assert.Equal(500, tracker.MaxInputTokens);
        Assert.Null(tracker.MaxOutputTokens);
        Assert.Null(tracker.MaxTokens);
    }

    [Fact]
    public void BeginScope_WithOutputBudget_SetsMaxOutputTokens()
    {
        var tracker = new TokenBudgetTracker();

        using var scope = tracker.BeginScope(maxOutputTokens: 1000);

        Assert.Null(tracker.MaxInputTokens);
        Assert.Equal(1000, tracker.MaxOutputTokens);
        Assert.Null(tracker.MaxTokens);
    }

    [Fact]
    public void BeginScope_WithAllBudgets_SetsAll()
    {
        var tracker = new TokenBudgetTracker();

        using var scope = tracker.BeginScope(
            maxInputTokens: 200, maxOutputTokens: 800, maxTotalTokens: 900);

        Assert.Equal(200, tracker.MaxInputTokens);
        Assert.Equal(800, tracker.MaxOutputTokens);
        Assert.Equal(900, tracker.MaxTokens);
    }

    [Fact]
    public void BeginScope_WithNoBudgets_CreatesTrackingOnlyScope()
    {
        var tracker = new TokenBudgetTracker();

        using (tracker.BeginScope(maxInputTokens: null, maxOutputTokens: null, maxTotalTokens: null))
        {
            tracker.Record(500, 200);
            Assert.Equal(700, tracker.CurrentTokens);
            Assert.Equal(500, tracker.CurrentInputTokens);
            Assert.Equal(200, tracker.CurrentOutputTokens);
            Assert.Null(tracker.MaxTokens);
            Assert.False(tracker.BudgetCancellationToken.IsCancellationRequested);
        }
    }

    [Fact]
    public void RecordDetailed_TracksInputAndOutputSeparately()
    {
        var tracker = new TokenBudgetTracker();
        using var scope = tracker.BeginScope(maxTotalTokens: 10000);

        tracker.Record(inputTokens: 100, outputTokens: 200);
        tracker.Record(inputTokens: 50, outputTokens: 150);

        Assert.Equal(150, tracker.CurrentInputTokens);
        Assert.Equal(350, tracker.CurrentOutputTokens);
        Assert.Equal(500, tracker.CurrentTokens);
    }

    [Fact]
    public void RecordDetailed_InputBudgetExceeded_CancelsToken()
    {
        var tracker = new TokenBudgetTracker();
        using var scope = tracker.BeginScope(maxInputTokens: 100);

        tracker.Record(inputTokens: 150, outputTokens: 50);

        Assert.True(tracker.BudgetCancellationToken.IsCancellationRequested);
    }

    [Fact]
    public void RecordDetailed_OutputBudgetExceeded_CancelsToken()
    {
        var tracker = new TokenBudgetTracker();
        using var scope = tracker.BeginScope(maxOutputTokens: 100);

        tracker.Record(inputTokens: 50, outputTokens: 150);

        Assert.True(tracker.BudgetCancellationToken.IsCancellationRequested);
    }

    [Fact]
    public void RecordDetailed_TotalBudgetExceeded_CancelsToken()
    {
        var tracker = new TokenBudgetTracker();
        using var scope = tracker.BeginScope(maxTotalTokens: 100);

        tracker.Record(inputTokens: 60, outputTokens: 60);

        Assert.True(tracker.BudgetCancellationToken.IsCancellationRequested);
    }

    [Fact]
    public void RecordDetailed_UnderAllBudgets_DoesNotCancel()
    {
        var tracker = new TokenBudgetTracker();
        using var scope = tracker.BeginScope(
            maxInputTokens: 200, maxOutputTokens: 200, maxTotalTokens: 300);

        tracker.Record(inputTokens: 50, outputTokens: 50);

        Assert.False(tracker.BudgetCancellationToken.IsCancellationRequested);
    }

    [Fact]
    public void CurrentInputTokens_WithoutScope_ReturnsZero()
    {
        var tracker = new TokenBudgetTracker();
        Assert.Equal(0, tracker.CurrentInputTokens);
    }

    [Fact]
    public void CurrentOutputTokens_WithoutScope_ReturnsZero()
    {
        var tracker = new TokenBudgetTracker();
        Assert.Equal(0, tracker.CurrentOutputTokens);
    }

    [Fact]
    public void MaxInputTokens_WithoutScope_ReturnsNull()
    {
        var tracker = new TokenBudgetTracker();
        Assert.Null(tracker.MaxInputTokens);
    }

    [Fact]
    public void MaxOutputTokens_WithoutScope_ReturnsNull()
    {
        var tracker = new TokenBudgetTracker();
        Assert.Null(tracker.MaxOutputTokens);
    }

    // -------------------------------------------------------------------------
    // AsyncLocal isolation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ConcurrentScopes_AreIsolated()
    {
        var tracker = new TokenBudgetTracker();

        long tokensInTask1 = 0;
        long tokensInTask2 = 0;
        var ct = TestContext.Current.CancellationToken;

        var task1 = Task.Run(() =>
        {
            using var scope = tracker.BeginScope(10000);
            tracker.Record(1000);
            tracker.Record(2000);
            tokensInTask1 = tracker.CurrentTokens;
        }, ct);

        var task2 = Task.Run(() =>
        {
            using var scope = tracker.BeginScope(5000);
            tracker.Record(100);
            tokensInTask2 = tracker.CurrentTokens;
        }, ct);

        await Task.WhenAll(task1, task2);

        Assert.Equal(3000, tokensInTask1);
        Assert.Equal(100, tokensInTask2);
    }
}
