using NexusLabs.Needlr.AgentFramework.Providers;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class ProviderFailurePolicyTests
{
    [Fact]
    public void Constructor_WithMatchOnly_HasNullSkipDurationAndOnHit()
    {
        var policy = new ProviderFailurePolicy(
            Match: ex => ex is InvalidOperationException);

        Assert.Null(policy.SkipDuration);
        Assert.Null(policy.OnHit);
    }

    [Fact]
    public void Constructor_WithSkipDuration_StoresIt()
    {
        var duration = TimeSpan.FromMinutes(5);
        var policy = new ProviderFailurePolicy(
            Match: _ => true,
            SkipDuration: duration);

        Assert.Equal(duration, policy.SkipDuration);
    }

    [Fact]
    public void Constructor_WithOnHit_StoresIt()
    {
        Func<ProviderFailureContext, ValueTask> onHit = _ => ValueTask.CompletedTask;
        var policy = new ProviderFailurePolicy(
            Match: _ => true,
            SkipDuration: null,
            OnHit: onHit);

        Assert.Same(onHit, policy.OnHit);
    }

    [Fact]
    public void IndefiniteSkip_EqualsTimeSpanMaxValue()
    {
        Assert.Equal(TimeSpan.MaxValue, ProviderFailurePolicy.IndefiniteSkip);
    }

    [Fact]
    public void Match_InvokedAgainstException_ReturnsExpectedResult()
    {
        var policy = new ProviderFailurePolicy(
            Match: ex => ex is InvalidOperationException);

        Assert.True(policy.Match(new InvalidOperationException()));
        Assert.False(policy.Match(new ArgumentException()));
    }

    [Fact]
    public void With_OverridesIndividualMembers_PreservesOthers()
    {
        Func<ProviderFailureContext, ValueTask> onHit = _ => ValueTask.CompletedTask;
        var original = new ProviderFailurePolicy(
            Match: ex => ex is InvalidOperationException,
            SkipDuration: TimeSpan.FromMinutes(1),
            OnHit: onHit);

        var modified = original with { SkipDuration = TimeSpan.FromMinutes(10) };

        Assert.Equal(TimeSpan.FromMinutes(10), modified.SkipDuration);
        Assert.Same(original.Match, modified.Match);
        Assert.Same(onHit, modified.OnHit);
    }
}
