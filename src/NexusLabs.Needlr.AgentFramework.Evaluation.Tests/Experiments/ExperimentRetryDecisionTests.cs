using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

public sealed class ExperimentRetryDecisionTests
{
    private static readonly TimeSpan MaxDelay = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    [Fact]
    public void DoNotRetry_StopsRetryingAndStoresZeroDelay()
    {
        var decision = ExperimentRetryDecision.DoNotRetry();

        Assert.False(decision.ShouldRetry, "Expected the no-retry decision to stop scheduling.");
        Assert.Equal(TimeSpan.Zero, decision.Delay);
    }

    [Fact]
    public void RetryAfter_ZeroDelay_SchedulesRetry()
    {
        var decision = ExperimentRetryDecision.RetryAfter(TimeSpan.Zero);

        Assert.True(decision.ShouldRetry, "Expected a zero delay to remain a scheduled retry.");
        Assert.Equal(TimeSpan.Zero, decision.Delay);
    }

    [Fact]
    public void RetryAfter_PositiveDelay_StoresDelay()
    {
        var decision = ExperimentRetryDecision.RetryAfter(TimeSpan.FromSeconds(3));

        Assert.True(decision.ShouldRetry, "Expected the positive delay to schedule another attempt.");
        Assert.Equal(TimeSpan.FromSeconds(3), decision.Delay);
    }

    [Fact]
    public void RetryAfter_MaximumDelay_IsAllowed()
    {
        var decision = ExperimentRetryDecision.RetryAfter(MaxDelay);

        Assert.True(decision.ShouldRetry, "Expected the maximum supported delay to schedule a retry.");
        Assert.Equal(MaxDelay, decision.Delay);
    }

    [Fact]
    public void RetryAfter_NegativeDelay_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ExperimentRetryDecision.RetryAfter(TimeSpan.FromMilliseconds(-1)));
    }

    [Fact]
    public void RetryAfter_InfiniteDelay_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ExperimentRetryDecision.RetryAfter(Timeout.InfiniteTimeSpan));
    }

    [Fact]
    public void RetryAfter_DelayAboveMaximum_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ExperimentRetryDecision.RetryAfter(MaxDelay + TimeSpan.FromMilliseconds(1)));
    }

    [Fact]
    public void Contract_HasNoPublicConstructors()
    {
        var type = typeof(ExperimentRetryDecision);
        Assert.True(type.IsSealed, "Expected retry decisions to remain sealed.");
        Assert.Empty(type.GetConstructors());
    }
}
