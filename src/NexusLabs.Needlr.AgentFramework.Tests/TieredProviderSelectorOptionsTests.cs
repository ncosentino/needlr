using NexusLabs.Needlr.AgentFramework.Providers;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class TieredProviderSelectorOptionsTests
{
    [Fact]
    public void Default_IsNotNull()
    {
        Assert.NotNull(TieredProviderSelectorOptions.Default);
    }

    [Fact]
    public void Default_FailurePolicies_ContainsExactlyProviderUnavailableExceptionRule()
    {
        var options = TieredProviderSelectorOptions.Default;

        Assert.Single(options.FailurePolicies);

        var rule = options.FailurePolicies[0];
        Assert.True(rule.Match(new ProviderUnavailableException("P", "down")));
        Assert.False(rule.Match(new InvalidOperationException()));
        Assert.Null(rule.SkipDuration);
        Assert.Null(rule.OnHit);
    }

    [Fact]
    public void DefaultInstance_IsSingleton()
    {
        var a = TieredProviderSelectorOptions.Default;
        var b = TieredProviderSelectorOptions.Default;

        Assert.Same(a, b);
    }

    [Fact]
    public void With_AppendsCustomPolicy_PreservingDefault()
    {
        var customPolicy = new ProviderFailurePolicy(
            Match: ex => ex is ArgumentException,
            SkipDuration: TimeSpan.FromMinutes(1));

        var options = TieredProviderSelectorOptions.Default with
        {
            FailurePolicies =
            [
                .. TieredProviderSelectorOptions.Default.FailurePolicies,
                customPolicy,
            ],
        };

        Assert.Equal(2, options.FailurePolicies.Count);
        Assert.True(options.FailurePolicies[0].Match(new ProviderUnavailableException("P", "down")));
        Assert.Same(customPolicy, options.FailurePolicies[1]);
    }

    [Fact]
    public void Default_FailurePoliciesIsNotMutatedByWithClone()
    {
        var snapshotCount = TieredProviderSelectorOptions.Default.FailurePolicies.Count;

        _ = TieredProviderSelectorOptions.Default with
        {
            FailurePolicies =
            [
                new ProviderFailurePolicy(_ => true, TimeSpan.Zero),
            ],
        };

        Assert.Equal(snapshotCount, TieredProviderSelectorOptions.Default.FailurePolicies.Count);
    }

    [Fact]
    public void New_DefaultCtor_HasEmptyFailurePolicies()
    {
        var options = new TieredProviderSelectorOptions();

        Assert.Empty(options.FailurePolicies);
    }
}
