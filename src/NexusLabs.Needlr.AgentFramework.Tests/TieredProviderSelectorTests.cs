using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.AgentFramework.Providers;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class TieredProviderSelectorTests
{
    // -------------------------------------------------------------------------
    // Happy path — first provider succeeds
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_FirstProvider_Succeeds()
    {
        var providers = new[]
        {
            new StubProvider("A", priority: 1, result: "result-A"),
        };

        var selector = new TieredProviderSelector<string, string>(
            providers, new AlwaysGrantQuotaGate(), new AgentExecutionContextAccessor());

        var result = await selector.ExecuteAsync("query", CancellationToken.None);

        Assert.Equal("result-A", result);
    }

    // -------------------------------------------------------------------------
    // Fallback — first fails, second succeeds
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_FirstFails_FallsToSecond()
    {
        var providers = new ITieredProvider<string, string>[]
        {
            new FailingProvider("A", priority: 1),
            new StubProvider("B", priority: 2, result: "result-B"),
        };

        var selector = new TieredProviderSelector<string, string>(
            providers, new AlwaysGrantQuotaGate(), new AgentExecutionContextAccessor());

        var result = await selector.ExecuteAsync("query", CancellationToken.None);

        Assert.Equal("result-B", result);
    }

    // -------------------------------------------------------------------------
    // Priority ordering
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ProvidersOrderedByPriority()
    {
        var providers = new[]
        {
            new StubProvider("Low", priority: 100, result: "low"),
            new StubProvider("High", priority: 1, result: "high"),
            new StubProvider("Medium", priority: 50, result: "medium"),
        };

        var selector = new TieredProviderSelector<string, string>(
            providers, new AlwaysGrantQuotaGate(), new AgentExecutionContextAccessor());

        var result = await selector.ExecuteAsync("query", CancellationToken.None);

        Assert.Equal("high", result);
    }

    [Fact]
    public async Task ExecuteAsync_SamePriority_OrderedByName()
    {
        var providers = new[]
        {
            new StubProvider("Zebra", priority: 1, result: "zebra"),
            new StubProvider("Alpha", priority: 1, result: "alpha"),
        };

        var selector = new TieredProviderSelector<string, string>(
            providers, new AlwaysGrantQuotaGate(), new AgentExecutionContextAccessor());

        var result = await selector.ExecuteAsync("query", CancellationToken.None);

        Assert.Equal("alpha", result);
    }

    // -------------------------------------------------------------------------
    // Disabled providers are skipped
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_DisabledProviders_AreSkipped()
    {
        var providers = new ITieredProvider<string, string>[]
        {
            new StubProvider("Disabled", priority: 1, result: "should-not-see", enabled: false),
            new StubProvider("Enabled", priority: 2, result: "correct"),
        };

        var selector = new TieredProviderSelector<string, string>(
            providers, new AlwaysGrantQuotaGate(), new AgentExecutionContextAccessor());

        var result = await selector.ExecuteAsync("query", CancellationToken.None);

        Assert.Equal("correct", result);
    }

    // -------------------------------------------------------------------------
    // All providers fail — throws
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_AllFail_ThrowsWithAttemptChain()
    {
        var providers = new ITieredProvider<string, string>[]
        {
            new FailingProvider("A", priority: 1),
            new FailingProvider("B", priority: 2),
        };

        var selector = new TieredProviderSelector<string, string>(
            providers, new AlwaysGrantQuotaGate(), new AgentExecutionContextAccessor());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            selector.ExecuteAsync("query", CancellationToken.None));

        Assert.Contains("All providers failed", ex.Message);
        Assert.Contains("A:", ex.Message);
        Assert.Contains("B:", ex.Message);
    }

    // -------------------------------------------------------------------------
    // No providers — throws
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_NoProviders_Throws()
    {
        var selector = new TieredProviderSelector<string, string>(
            [], new AlwaysGrantQuotaGate(), new AgentExecutionContextAccessor());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            selector.ExecuteAsync("query", CancellationToken.None));
    }

    // -------------------------------------------------------------------------
    // Quota gate — denied skips provider
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_QuotaDenied_SkipsProvider()
    {
        var providers = new[]
        {
            new StubProvider("QuotaDenied", priority: 1, result: "denied"),
            new StubProvider("Fallback", priority: 2, result: "fallback"),
        };

        var gate = new DenySpecificGate("QuotaDenied");

        var selector = new TieredProviderSelector<string, string>(
            providers, gate, new AgentExecutionContextAccessor());

        var result = await selector.ExecuteAsync("query", CancellationToken.None);

        Assert.Equal("fallback", result);
    }

    // -------------------------------------------------------------------------
    // Quota gate — release called on success and failure
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_Success_ReleasesWithSucceeded()
    {
        var providers = new[]
        {
            new StubProvider("A", priority: 1, result: "ok"),
        };

        var gate = new TrackingQuotaGate();

        var selector = new TieredProviderSelector<string, string>(
            providers, gate, new AgentExecutionContextAccessor());

        await selector.ExecuteAsync("query", CancellationToken.None);

        Assert.Single(gate.Releases);
        Assert.True(gate.Releases[0].Succeeded);
        Assert.Equal("A", gate.Releases[0].ProviderName);
    }

    [Fact]
    public async Task ExecuteAsync_Failure_ReleasesWithFailed()
    {
        var providers = new ITieredProvider<string, string>[]
        {
            new FailingProvider("A", priority: 1),
            new StubProvider("B", priority: 2, result: "ok"),
        };

        var gate = new TrackingQuotaGate();

        var selector = new TieredProviderSelector<string, string>(
            providers, gate, new AgentExecutionContextAccessor());

        await selector.ExecuteAsync("query", CancellationToken.None);

        // A failed, B succeeded
        Assert.Equal(2, gate.Releases.Count);
        Assert.False(gate.Releases[0].Succeeded);
        Assert.Equal("A", gate.Releases[0].ProviderName);
        Assert.True(gate.Releases[1].Succeeded);
        Assert.Equal("B", gate.Releases[1].ProviderName);
    }

    // -------------------------------------------------------------------------
    // Constructor validation
    // -------------------------------------------------------------------------

    // -------------------------------------------------------------------------
    // Partition flows from context to quota gate
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WithContextScope_PassesUserIdAsPartition()
    {
        var providers = new ITieredProvider<string, string>[]
        {
            new StubProvider("A", priority: 1, result: "ok"),
        };

        var gate = new TrackingQuotaGate();
        var accessor = new AgentExecutionContextAccessor();

        var selector = new TieredProviderSelector<string, string>(
            providers, gate, accessor);

        using (accessor.BeginScope(new AgentExecutionContext("user-42", "orch-1")))
        {
            await selector.ExecuteAsync("query", CancellationToken.None);
        }

        Assert.Single(gate.Releases);
        Assert.Equal("user-42", gate.Releases[0].Partition);
    }

    [Fact]
    public async Task ExecuteAsync_NoContext_PartitionIsNull()
    {
        var providers = new ITieredProvider<string, string>[]
        {
            new StubProvider("A", priority: 1, result: "ok"),
        };

        var gate = new TrackingQuotaGate();
        var accessor = new AgentExecutionContextAccessor();

        var selector = new TieredProviderSelector<string, string>(
            providers, gate, accessor);

        await selector.ExecuteAsync("query", CancellationToken.None);

        Assert.Single(gate.Releases);
        Assert.Null(gate.Releases[0].Partition);
    }

    [Fact]
    public async Task ExecuteAsync_CustomPartitionSelector_UsesIt()
    {
        var providers = new ITieredProvider<string, string>[]
        {
            new StubProvider("A", priority: 1, result: "ok"),
        };

        var gate = new TrackingQuotaGate();
        var accessor = new AgentExecutionContextAccessor();

        var selector = new TieredProviderSelector<string, string>(
            providers, gate, accessor,
            partitionSelector: ctx => ctx?.OrchestrationId);

        using (accessor.BeginScope(new AgentExecutionContext("user-42", "orch-99")))
        {
            await selector.ExecuteAsync("query", CancellationToken.None);
        }

        Assert.Single(gate.Releases);
        Assert.Equal("orch-99", gate.Releases[0].Partition);
    }

    // -------------------------------------------------------------------------
    // Null guards
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_NullProviders_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TieredProviderSelector<string, string>(null!, new AlwaysGrantQuotaGate(), new AgentExecutionContextAccessor()));
    }

    [Fact]
    public void Constructor_NullQuotaGate_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TieredProviderSelector<string, string>([], null!, new AgentExecutionContextAccessor()));
    }

    [Fact]
    public void Constructor_NullContextAccessor_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TieredProviderSelector<string, string>([], new AlwaysGrantQuotaGate(), null!));
    }

    // -------------------------------------------------------------------------
    // Test helpers
    // -------------------------------------------------------------------------

    private sealed class StubProvider(string name, int priority, string result, bool enabled = true)
        : ITieredProvider<string, string>
    {
        public string Name => name;
        public int Priority => priority;
        public bool IsEnabled => enabled;

        public Task<string> ExecuteAsync(string query, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class FailingProvider(string name, int priority) : ITieredProvider<string, string>
    {
        public string Name => name;
        public int Priority => priority;
        public bool IsEnabled => true;

        public Task<string> ExecuteAsync(string query, CancellationToken cancellationToken) =>
            throw new ProviderUnavailableException(name, $"{name} is unavailable");
    }

    private sealed class DenySpecificGate(string denyName) : IQuotaGate
    {
        public Task<bool> TryReserveAsync(string providerName, string? quotaPartition, CancellationToken ct) =>
            Task.FromResult(!string.Equals(providerName, denyName, StringComparison.OrdinalIgnoreCase));

        public Task ReleaseAsync(string providerName, string? quotaPartition, bool succeeded, CancellationToken ct) =>
            Task.CompletedTask;
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
}
