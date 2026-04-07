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
