using Microsoft.Extensions.Time.Testing;

using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.AgentFramework.Providers;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class TieredProviderSelectorPolicyTests
{
    [Fact]
    public async Task DefaultOptions_ProviderUnavailableException_FallsThroughToNextProvider()
    {
        var providers = new ITieredProvider<string, string>[]
        {
            new ThrowingProvider("A", 1, () => new ProviderUnavailableException("A", "down")),
            new StubProvider("B", 2, "result-B"),
        };

        var selector = CreateSelector(providers);

        var result = await selector.ExecuteAsync("query", CancellationToken.None);

        Assert.Equal("result-B", result);
    }

    [Fact]
    public async Task DefaultOptions_NonPueException_PropagatesRawWithoutFallthrough()
    {
        var bExecuted = false;
        var providers = new ITieredProvider<string, string>[]
        {
            new ThrowingProvider("A", 1, () => new InvalidOperationException("auth failed")),
            new StubProvider("B", 2, "result-B", onExecute: () => bExecuted = true),
        };

        var selector = CreateSelector(providers);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            selector.ExecuteAsync("query", CancellationToken.None));

        Assert.Equal("auth failed", ex.Message);
        Assert.False(bExecuted);
    }

    [Fact]
    public async Task CustomPolicy_NonPueException_FallsThroughToNextProvider()
    {
        var providers = new ITieredProvider<string, string>[]
        {
            new ThrowingProvider("A", 1, () => new MyAuthException("auth failed")),
            new StubProvider("B", 2, "result-B"),
        };

        var options = TieredProviderSelectorOptions.Default with
        {
            FailurePolicies =
            [
                .. TieredProviderSelectorOptions.Default.FailurePolicies,
                new ProviderFailurePolicy(Match: ex => ex is MyAuthException),
            ],
        };

        var selector = CreateSelector(providers, options);

        var result = await selector.ExecuteAsync("query", CancellationToken.None);

        Assert.Equal("result-B", result);
    }

    [Fact]
    public async Task CustomPolicy_AllProvidersThrowMatchedException_ThrowsAllProvidersFailed()
    {
        var providers = new ITieredProvider<string, string>[]
        {
            new ThrowingProvider("A", 1, () => new MyAuthException("a-down")),
            new ThrowingProvider("B", 2, () => new MyAuthException("b-down")),
        };

        var options = TieredProviderSelectorOptions.Default with
        {
            FailurePolicies =
            [
                new ProviderFailurePolicy(Match: ex => ex is MyAuthException),
            ],
        };

        var selector = CreateSelector(providers, options);

        var ex = await Assert.ThrowsAsync<AllProvidersFailedException>(() =>
            selector.ExecuteAsync("query", CancellationToken.None));

        Assert.Equal(2, ex.Attempts.Count);
        Assert.Contains(ex.Attempts, a => a.StartsWith("A:", StringComparison.Ordinal) && a.Contains("a-down"));
        Assert.Contains(ex.Attempts, a => a.StartsWith("B:", StringComparison.Ordinal) && a.Contains("b-down"));
    }

    [Fact]
    public async Task PolicyOrdering_FirstMatchWins()
    {
        ProviderFailureContext? capturedFromFirst = null;
        ProviderFailureContext? capturedFromSecond = null;

        var providers = new ITieredProvider<string, string>[]
        {
            new ThrowingProvider("A", 1, () => new InvalidOperationException("boom")),
            new StubProvider("B", 2, "result-B"),
        };

        var options = TieredProviderSelectorOptions.Default with
        {
            FailurePolicies =
            [
                new ProviderFailurePolicy(
                    Match: ex => ex is InvalidOperationException,
                    OnHit: ctx => { capturedFromFirst = ctx; return ValueTask.CompletedTask; }),
                new ProviderFailurePolicy(
                    Match: _ => true,
                    OnHit: ctx => { capturedFromSecond = ctx; return ValueTask.CompletedTask; }),
            ],
        };

        var selector = CreateSelector(providers, options);

        await selector.ExecuteAsync("query", CancellationToken.None);

        Assert.NotNull(capturedFromFirst);
        Assert.Null(capturedFromSecond);
    }

    [Fact]
    public async Task SkipDuration_FailureCachesProvider_BypassedOnSubsequentCallsUntilElapsed()
    {
        var startTime = new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var fakeTime = new FakeTimeProvider(startTime);

        var aExecutionCount = 0;
        var providers = new ITieredProvider<string, string>[]
        {
            new ThrowingProvider("A", 1, () =>
            {
                aExecutionCount++;
                return new MyAuthException("a-down");
            }),
            new StubProvider("B", 2, "result-B"),
        };

        var options = TieredProviderSelectorOptions.Default with
        {
            FailurePolicies =
            [
                new ProviderFailurePolicy(
                    Match: ex => ex is MyAuthException,
                    SkipDuration: TimeSpan.FromMinutes(5)),
            ],
        };

        var selector = CreateSelector(providers, options, fakeTime);

        await selector.ExecuteAsync("q1", CancellationToken.None);
        Assert.Equal(1, aExecutionCount);

        fakeTime.Advance(TimeSpan.FromMinutes(1));
        await selector.ExecuteAsync("q2", CancellationToken.None);
        Assert.Equal(1, aExecutionCount);

        fakeTime.Advance(TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(1));
        await selector.ExecuteAsync("q3", CancellationToken.None);
        Assert.Equal(2, aExecutionCount);
    }

    [Fact]
    public async Task SkipDuration_BypassedProvider_LogsSkippedAttemptInChain()
    {
        var fakeTime = new FakeTimeProvider();
        var providers = new ITieredProvider<string, string>[]
        {
            new ThrowingProvider("A", 1, () => new MyAuthException("a-down")),
            new ThrowingProvider("B", 2, () => new MyAuthException("b-down")),
        };

        var options = TieredProviderSelectorOptions.Default with
        {
            FailurePolicies =
            [
                new ProviderFailurePolicy(
                    Match: ex => ex is MyAuthException,
                    SkipDuration: TimeSpan.FromMinutes(5)),
            ],
        };

        var selector = CreateSelector(providers, options, fakeTime);

        await Assert.ThrowsAsync<AllProvidersFailedException>(() =>
            selector.ExecuteAsync("q1", CancellationToken.None));

        var ex = await Assert.ThrowsAsync<AllProvidersFailedException>(() =>
            selector.ExecuteAsync("q2", CancellationToken.None));

        Assert.Equal(2, ex.Attempts.Count);
        Assert.All(ex.Attempts, a =>
            Assert.Contains("skipped", a, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task IndefiniteSkip_ProviderRemainsSkippedAfterLargeTimeAdvance()
    {
        var fakeTime = new FakeTimeProvider();
        var aExecutionCount = 0;

        var providers = new ITieredProvider<string, string>[]
        {
            new ThrowingProvider("A", 1, () =>
            {
                aExecutionCount++;
                return new MyAuthException("a-down");
            }),
            new StubProvider("B", 2, "result-B"),
        };

        var options = TieredProviderSelectorOptions.Default with
        {
            FailurePolicies =
            [
                new ProviderFailurePolicy(
                    Match: ex => ex is MyAuthException,
                    SkipDuration: ProviderFailurePolicy.IndefiniteSkip),
            ],
        };

        var selector = CreateSelector(providers, options, fakeTime);

        await selector.ExecuteAsync("q1", CancellationToken.None);
        Assert.Equal(1, aExecutionCount);

        fakeTime.Advance(TimeSpan.FromDays(365 * 100));
        await selector.ExecuteAsync("q2", CancellationToken.None);
        Assert.Equal(1, aExecutionCount);
    }

    [Fact]
    public async Task IndefiniteSkip_DoesNotOverflow_ResolvesToDateTimeOffsetMaxValue()
    {
        var fakeTime = new FakeTimeProvider();
        ProviderFailureContext? captured = null;

        var providers = new ITieredProvider<string, string>[]
        {
            new ThrowingProvider("A", 1, () => new MyAuthException("a-down")),
            new StubProvider("B", 2, "result-B"),
        };

        var options = TieredProviderSelectorOptions.Default with
        {
            FailurePolicies =
            [
                new ProviderFailurePolicy(
                    Match: ex => ex is MyAuthException,
                    SkipDuration: ProviderFailurePolicy.IndefiniteSkip,
                    OnHit: ctx => { captured = ctx; return ValueTask.CompletedTask; }),
            ],
        };

        var selector = CreateSelector(providers, options, fakeTime);

        await selector.ExecuteAsync("q", CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(DateTimeOffset.MaxValue, captured.SkipUntil);
    }

    [Fact]
    public async Task NullSkipDuration_NoSkipCacheEntry_ProviderRetriedNextCall()
    {
        var fakeTime = new FakeTimeProvider();
        var aExecutionCount = 0;

        var providers = new ITieredProvider<string, string>[]
        {
            new ThrowingProvider("A", 1, () =>
            {
                aExecutionCount++;
                return new MyAuthException("a-down");
            }),
            new StubProvider("B", 2, "result-B"),
        };

        var options = TieredProviderSelectorOptions.Default with
        {
            FailurePolicies =
            [
                new ProviderFailurePolicy(
                    Match: ex => ex is MyAuthException,
                    SkipDuration: null),
            ],
        };

        var selector = CreateSelector(providers, options, fakeTime);

        await selector.ExecuteAsync("q1", CancellationToken.None);
        await selector.ExecuteAsync("q2", CancellationToken.None);
        await selector.ExecuteAsync("q3", CancellationToken.None);

        Assert.Equal(3, aExecutionCount);
    }

    [Fact]
    public async Task OnHit_FiresWithProviderNameExceptionAndSkipUntil()
    {
        var startTime = new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var fakeTime = new FakeTimeProvider(startTime);
        var thrown = new MyAuthException("a-down");

        ProviderFailureContext? captured = null;
        var providers = new ITieredProvider<string, string>[]
        {
            new ThrowingProvider("A", 1, () => thrown),
            new StubProvider("B", 2, "result-B"),
        };

        var options = TieredProviderSelectorOptions.Default with
        {
            FailurePolicies =
            [
                new ProviderFailurePolicy(
                    Match: ex => ex is MyAuthException,
                    SkipDuration: TimeSpan.FromMinutes(5),
                    OnHit: ctx => { captured = ctx; return ValueTask.CompletedTask; }),
            ],
        };

        var selector = CreateSelector(providers, options, fakeTime);

        await selector.ExecuteAsync("q", CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("A", captured.ProviderName);
        Assert.Same(thrown, captured.Exception);
        Assert.Equal(startTime + TimeSpan.FromMinutes(5), captured.SkipUntil);
    }

    [Fact]
    public async Task OnHit_NullSkipDuration_SkipUntilOnContextIsNull()
    {
        ProviderFailureContext? captured = null;
        var providers = new ITieredProvider<string, string>[]
        {
            new ThrowingProvider("A", 1, () => new MyAuthException("a-down")),
            new StubProvider("B", 2, "result-B"),
        };

        var options = TieredProviderSelectorOptions.Default with
        {
            FailurePolicies =
            [
                new ProviderFailurePolicy(
                    Match: ex => ex is MyAuthException,
                    SkipDuration: null,
                    OnHit: ctx => { captured = ctx; return ValueTask.CompletedTask; }),
            ],
        };

        var selector = CreateSelector(providers, options);

        await selector.ExecuteAsync("q", CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Null(captured.SkipUntil);
    }

    [Fact]
    public async Task Cancellation_NotMatchedByPolicy_PropagatesRawAndDoesNotSkip()
    {
        var providers = new ITieredProvider<string, string>[]
        {
            new ThrowingProvider("A", 1, () => new OperationCanceledException()),
            new StubProvider("B", 2, "result-B"),
        };

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var options = TieredProviderSelectorOptions.Default with
        {
            FailurePolicies =
            [
                new ProviderFailurePolicy(
                    Match: _ => true,
                    SkipDuration: ProviderFailurePolicy.IndefiniteSkip),
            ],
        };

        var selector = CreateSelector(providers, options);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            selector.ExecuteAsync("q", cts.Token));
    }

    [Fact]
    public async Task QuotaRelease_OnUnmatchedThrow_ReleasesQuotaWithFailedFlag()
    {
        var gate = new TrackingQuotaGate();
        var providers = new ITieredProvider<string, string>[]
        {
            new ThrowingProvider("A", 1, () => new InvalidOperationException("boom")),
        };

        var selector = CreateSelector(providers, gate: gate);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            selector.ExecuteAsync("q", CancellationToken.None));

        Assert.Single(gate.Releases);
        Assert.Equal("A", gate.Releases[0].ProviderName);
        Assert.False(gate.Releases[0].Succeeded);
    }

    [Fact]
    public async Task QuotaRelease_OnMatchedPolicy_ReleasesQuotaWithFailedFlag()
    {
        var gate = new TrackingQuotaGate();
        var providers = new ITieredProvider<string, string>[]
        {
            new ThrowingProvider("A", 1, () => new MyAuthException("down")),
            new StubProvider("B", 2, "result-B"),
        };

        var options = TieredProviderSelectorOptions.Default with
        {
            FailurePolicies =
            [
                new ProviderFailurePolicy(Match: ex => ex is MyAuthException),
            ],
        };

        var selector = CreateSelector(providers, options, gate: gate);

        await selector.ExecuteAsync("q", CancellationToken.None);

        Assert.Equal(2, gate.Releases.Count);
        Assert.Equal("A", gate.Releases[0].ProviderName);
        Assert.False(gate.Releases[0].Succeeded);
        Assert.Equal("B", gate.Releases[1].ProviderName);
        Assert.True(gate.Releases[1].Succeeded);
    }

    [Fact]
    public async Task QuotaRelease_WhenOnHitThrows_StillReleasesQuota()
    {
        var gate = new TrackingQuotaGate();
        var providers = new ITieredProvider<string, string>[]
        {
            new ThrowingProvider("A", 1, () => new MyAuthException("down")),
        };

        var options = TieredProviderSelectorOptions.Default with
        {
            FailurePolicies =
            [
                new ProviderFailurePolicy(
                    Match: ex => ex is MyAuthException,
                    OnHit: _ => throw new ApplicationException("callback boom")),
            ],
        };

        var selector = CreateSelector(providers, options, gate: gate);

        await Assert.ThrowsAsync<ApplicationException>(() =>
            selector.ExecuteAsync("q", CancellationToken.None));

        Assert.Single(gate.Releases);
        Assert.Equal("A", gate.Releases[0].ProviderName);
        Assert.False(gate.Releases[0].Succeeded);
    }

    [Fact]
    public async Task QuotaRelease_OnSuccess_ReleasesQuotaWithSucceededFlag()
    {
        var gate = new TrackingQuotaGate();
        var providers = new ITieredProvider<string, string>[]
        {
            new StubProvider("A", 1, "result-A"),
        };

        var selector = CreateSelector(providers, gate: gate);

        await selector.ExecuteAsync("q", CancellationToken.None);

        Assert.Single(gate.Releases);
        Assert.True(gate.Releases[0].Succeeded);
    }

    [Fact]
    public void Constructor_NullOptions_FallsBackToDefault()
    {
        var providers = new ITieredProvider<string, string>[]
        {
            new StubProvider("A", 1, "ok"),
        };

        var selector = new TieredProviderSelector<string, string>(
            providers,
            new AlwaysGrantQuotaGate(),
            new AgentExecutionContextAccessor(),
            partitionSelector: null,
            options: null,
            timeProvider: null);

        Assert.NotNull(selector);
    }

    [Fact]
    public async Task SkipState_PerInstance_NotSharedBetweenSelectors()
    {
        var fakeTime = new FakeTimeProvider();
        var aExecutionCount = 0;
        ITieredProvider<string, string>[] MakeProviders() => new ITieredProvider<string, string>[]
        {
            new ThrowingProvider("A", 1, () =>
            {
                aExecutionCount++;
                return new MyAuthException("a-down");
            }),
            new StubProvider("B", 2, "result-B"),
        };

        var options = TieredProviderSelectorOptions.Default with
        {
            FailurePolicies =
            [
                new ProviderFailurePolicy(
                    Match: ex => ex is MyAuthException,
                    SkipDuration: TimeSpan.FromMinutes(5)),
            ],
        };

        var selector1 = CreateSelector(MakeProviders(), options, fakeTime);
        var selector2 = CreateSelector(MakeProviders(), options, fakeTime);

        await selector1.ExecuteAsync("q", CancellationToken.None);
        await selector2.ExecuteAsync("q", CancellationToken.None);

        Assert.Equal(2, aExecutionCount);
    }

    [Fact]
    public async Task DenyAllQuota_NoProviderEverInvoked_ThrowsAllProvidersFailed()
    {
        var executed = false;
        var providers = new ITieredProvider<string, string>[]
        {
            new StubProvider("A", 1, "ok", onExecute: () => executed = true),
        };

        var selector = CreateSelector(providers, gate: new DenyAllQuotaGate());

        await Assert.ThrowsAsync<AllProvidersFailedException>(() =>
            selector.ExecuteAsync("q", CancellationToken.None));

        Assert.False(executed);
    }

    private static TieredProviderSelector<string, string> CreateSelector(
        IEnumerable<ITieredProvider<string, string>> providers,
        TieredProviderSelectorOptions? options = null,
        TimeProvider? timeProvider = null,
        IQuotaGate? gate = null)
    {
        return new TieredProviderSelector<string, string>(
            providers,
            gate ?? new AlwaysGrantQuotaGate(),
            new AgentExecutionContextAccessor(),
            partitionSelector: null,
            options: options,
            timeProvider: timeProvider);
    }

    private sealed class StubProvider(
        string name,
        int priority,
        string result,
        bool enabled = true,
        Action? onExecute = null)
        : ITieredProvider<string, string>
    {
        public string Name => name;
        public int Priority => priority;
        public bool IsEnabled => enabled;

        public Task<string> ExecuteAsync(string query, CancellationToken cancellationToken)
        {
            onExecute?.Invoke();
            return Task.FromResult(result);
        }
    }

    private sealed class ThrowingProvider(string name, int priority, Func<Exception> exceptionFactory)
        : ITieredProvider<string, string>
    {
        public string Name => name;
        public int Priority => priority;
        public bool IsEnabled => true;

        public Task<string> ExecuteAsync(string query, CancellationToken cancellationToken) =>
            throw exceptionFactory();
    }

    private sealed class TrackingQuotaGate : IQuotaGate
    {
        public List<(string ProviderName, string? Partition, bool Succeeded)> Releases { get; } = [];

        public Task<bool> TryReserveAsync(string providerName, string? quotaPartition, CancellationToken ct) =>
            Task.FromResult(true);

        public Task ReleaseAsync(string providerName, string? quotaPartition, bool succeeded, CancellationToken ct)
        {
            Releases.Add((providerName, quotaPartition, succeeded));
            return Task.CompletedTask;
        }
    }

    private sealed class DenyAllQuotaGate : IQuotaGate
    {
        public Task<bool> TryReserveAsync(string providerName, string? quotaPartition, CancellationToken ct) =>
            Task.FromResult(false);

        public Task ReleaseAsync(string providerName, string? quotaPartition, bool succeeded, CancellationToken ct) =>
            Task.CompletedTask;
    }

    private sealed class MyAuthException(string message) : Exception(message);
}
