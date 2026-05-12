using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.AgentFramework.Providers;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class TieredProviderSelectorServiceCollectionExtensionsTests
{
    [Fact]
    public async Task NoConfigure_ResolvesSelectorWithDefaultPolicies()
    {
        var services = BuildBaseServices();
        services.AddSingleton<ITieredProvider<string, string>>(
            new ThrowingProvider("A", 1, () => new ProviderUnavailableException("A", "down")));
        services.AddSingleton<ITieredProvider<string, string>>(
            new StubProvider("B", 2, "result-B"));

        services.AddTieredProviderSelector<string, string>();

        await using var sp = services.BuildServiceProvider();

        var selector = sp.GetRequiredService<ITieredProviderSelector<string, string>>();

        var result = await selector.ExecuteAsync("q", CancellationToken.None);

        Assert.Equal("result-B", result);
    }

    [Fact]
    public async Task SimpleConfigureOverload_ReceivesDefaultAsStartingPoint()
    {
        TieredProviderSelectorOptions? captured = null;

        var services = BuildBaseServices();
        services.AddSingleton<ITieredProvider<string, string>>(
            new ThrowingProvider("A", 1, () => new MyAuthException("a-down")));
        services.AddSingleton<ITieredProvider<string, string>>(
            new StubProvider("B", 2, "result-B"));

        services.AddTieredProviderSelector<string, string>(opts =>
        {
            captured = opts;
            return opts with
            {
                FailurePolicies =
                [
                    .. opts.FailurePolicies,
                    new ProviderFailurePolicy(Match: ex => ex is MyAuthException),
                ],
            };
        });

        await using var sp = services.BuildServiceProvider();
        var selector = sp.GetRequiredService<ITieredProviderSelector<string, string>>();

        await selector.ExecuteAsync("q", CancellationToken.None);

        Assert.Same(TieredProviderSelectorOptions.Default, captured);
    }

    [Fact]
    public async Task DiAwareConfigureOverload_CanResolveServicesFromContainer()
    {
        ILogger<MyAuthException>? loggerSeenInOnHit = null;

        var services = BuildBaseServices();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton<ITieredProvider<string, string>>(
            new ThrowingProvider("A", 1, () => new MyAuthException("a-down")));
        services.AddSingleton<ITieredProvider<string, string>>(
            new StubProvider("B", 2, "result-B"));

        services.AddTieredProviderSelector<string, string>((sp, opts) =>
        {
            var logger = sp.GetRequiredService<ILogger<MyAuthException>>();
            return opts with
            {
                FailurePolicies =
                [
                    .. opts.FailurePolicies,
                    new ProviderFailurePolicy(
                        Match: ex => ex is MyAuthException,
                        OnHit: _ =>
                        {
                            loggerSeenInOnHit = logger;
                            return ValueTask.CompletedTask;
                        }),
                ],
            };
        });

        await using var sp = services.BuildServiceProvider();
        var selector = sp.GetRequiredService<ITieredProviderSelector<string, string>>();

        await selector.ExecuteAsync("q", CancellationToken.None);

        Assert.NotNull(loggerSeenInOnHit);
    }

    [Fact]
    public async Task PerType_TwoCallsWithDifferentGenericArgs_HaveIndependentPolicies()
    {
        var stringConfigureCount = 0;
        var intConfigureCount = 0;

        var services = BuildBaseServices();
        services.AddSingleton<ITieredProvider<string, string>>(
            new StubProvider("S", 1, "string-result"));
        services.AddSingleton<ITieredProvider<int, int>>(new IntStubProvider("I", 1, 42));

        services.AddTieredProviderSelector<string, string>(opts =>
        {
            stringConfigureCount++;
            return opts;
        });
        services.AddTieredProviderSelector<int, int>(opts =>
        {
            intConfigureCount++;
            return opts;
        });

        await using var sp = services.BuildServiceProvider();
        var stringSelector = sp.GetRequiredService<ITieredProviderSelector<string, string>>();
        var intSelector = sp.GetRequiredService<ITieredProviderSelector<int, int>>();

        await stringSelector.ExecuteAsync("q", CancellationToken.None);
        await intSelector.ExecuteAsync(0, CancellationToken.None);

        Assert.Equal(1, stringConfigureCount);
        Assert.Equal(1, intConfigureCount);
    }

    [Fact]
    public async Task TimeProviderRegistration_NotPresent_RegistersSystem()
    {
        var services = BuildBaseServices();
        services.AddSingleton<ITieredProvider<string, string>>(new StubProvider("A", 1, "ok"));

        services.AddTieredProviderSelector<string, string>();

        await using var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<TimeProvider>();

        Assert.Same(TimeProvider.System, resolved);
    }

    [Fact]
    public async Task TimeProviderRegistration_AlreadyPresent_NotOverridden()
    {
        var fakeTime = new FakeTimeProvider();

        var services = BuildBaseServices();
        services.AddSingleton<TimeProvider>(fakeTime);
        services.AddSingleton<ITieredProvider<string, string>>(new StubProvider("A", 1, "ok"));

        services.AddTieredProviderSelector<string, string>();

        await using var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<TimeProvider>();

        Assert.Same(fakeTime, resolved);
    }

    [Fact]
    public async Task ConfigureLambda_RunsExactlyOncePerSingletonResolution()
    {
        var configureInvocations = 0;

        var services = BuildBaseServices();
        services.AddSingleton<ITieredProvider<string, string>>(new StubProvider("A", 1, "ok"));

        services.AddTieredProviderSelector<string, string>(opts =>
        {
            configureInvocations++;
            return opts;
        });

        await using var sp = services.BuildServiceProvider();
        var selector = sp.GetRequiredService<ITieredProviderSelector<string, string>>();

        await selector.ExecuteAsync("q1", CancellationToken.None);
        await selector.ExecuteAsync("q2", CancellationToken.None);
        await selector.ExecuteAsync("q3", CancellationToken.None);

        Assert.Equal(1, configureInvocations);
    }

    [Fact]
    public async Task ConfigureLambda_ReturnsNull_ThrowsInvalidOperationException()
    {
        var services = BuildBaseServices();
        services.AddSingleton<ITieredProvider<string, string>>(new StubProvider("A", 1, "ok"));

        services.AddTieredProviderSelector<string, string>(_ => null!);

        await using var sp = services.BuildServiceProvider();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Task.Run(() => sp.GetRequiredService<ITieredProviderSelector<string, string>>()));

        Assert.Contains("configure", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NoProvidersRegistered_FirstExecuteAsyncThrowsNoProvidersRegisteredException()
    {
        var services = BuildBaseServices();

        services.AddTieredProviderSelector<string, string>();

        await using var sp = services.BuildServiceProvider();
        var selector = sp.GetRequiredService<ITieredProviderSelector<string, string>>();

        await Assert.ThrowsAsync<NoProvidersRegisteredException>(() =>
            selector.ExecuteAsync("q", CancellationToken.None));
    }

    [Fact]
    public async Task LastWins_PreToPost_SecondCallCustomConfigureWins()
    {
        var defaultUsed = false;
        var customUsed = false;

        var services = BuildBaseServices();
        services.AddSingleton<ITieredProvider<string, string>>(
            new ThrowingProvider("A", 1, () => new MyAuthException("down")));
        services.AddSingleton<ITieredProvider<string, string>>(
            new StubProvider("B", 2, "ok"));

        services.AddTieredProviderSelector<string, string>(opts =>
        {
            defaultUsed = true;
            return opts;
        });

        services.AddTieredProviderSelector<string, string>(opts =>
        {
            customUsed = true;
            return opts with
            {
                FailurePolicies =
                [
                    .. opts.FailurePolicies,
                    new ProviderFailurePolicy(Match: ex => ex is MyAuthException),
                ],
            };
        });

        await using var sp = services.BuildServiceProvider();
        var selector = sp.GetRequiredService<ITieredProviderSelector<string, string>>();

        var result = await selector.ExecuteAsync("q", CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.False(defaultUsed);
        Assert.True(customUsed);
    }

    [Fact]
    public async Task LastWins_PostToPre_SecondCallDefaultConfigureWins()
    {
        var customUsed = false;
        var defaultUsed = false;

        var services = BuildBaseServices();
        services.AddSingleton<ITieredProvider<string, string>>(
            new ThrowingProvider("A", 1, () => new MyAuthException("down")));
        services.AddSingleton<ITieredProvider<string, string>>(
            new StubProvider("B", 2, "ok"));

        services.AddTieredProviderSelector<string, string>(opts =>
        {
            customUsed = true;
            return opts with
            {
                FailurePolicies =
                [
                    .. opts.FailurePolicies,
                    new ProviderFailurePolicy(Match: ex => ex is MyAuthException),
                ],
            };
        });

        services.AddTieredProviderSelector<string, string>(opts =>
        {
            defaultUsed = true;
            return opts;
        });

        await using var sp = services.BuildServiceProvider();
        var selector = sp.GetRequiredService<ITieredProviderSelector<string, string>>();

        await Assert.ThrowsAsync<MyAuthException>(() =>
            selector.ExecuteAsync("q", CancellationToken.None));
        Assert.False(customUsed);
        Assert.True(defaultUsed);
    }

    [Fact]
    public async Task LastWins_SamePluginDoubleCall_SecondConfigureWins()
    {
        TieredProviderSelectorOptions? lastConfigureSnapshot = null;

        var services = BuildBaseServices();
        services.AddSingleton<ITieredProvider<string, string>>(new StubProvider("A", 1, "ok"));

        services.AddTieredProviderSelector<string, string>(opts => opts with
        {
            FailurePolicies = [new ProviderFailurePolicy(Match: ex => ex is InvalidOperationException)],
        });

        services.AddTieredProviderSelector<string, string>(opts =>
        {
            var modified = opts with
            {
                FailurePolicies = [new ProviderFailurePolicy(Match: ex => ex is ArgumentException)],
            };
            lastConfigureSnapshot = modified;
            return modified;
        });

        await using var sp = services.BuildServiceProvider();
        var selector = sp.GetRequiredService<ITieredProviderSelector<string, string>>();

        await selector.ExecuteAsync("q", CancellationToken.None);

        Assert.NotNull(lastConfigureSnapshot);
        Assert.Single(lastConfigureSnapshot.FailurePolicies);
        Assert.True(lastConfigureSnapshot.FailurePolicies[0].Match(new ArgumentException()));
        Assert.False(lastConfigureSnapshot.FailurePolicies[0].Match(new InvalidOperationException()));
    }

    [Fact]
    public async Task FailurePolicy_FromConfiguredOptions_AppliedAtRuntime()
    {
        var services = BuildBaseServices();
        services.AddSingleton<ITieredProvider<string, string>>(
            new ThrowingProvider("A", 1, () => new MyAuthException("a-down")));
        services.AddSingleton<ITieredProvider<string, string>>(
            new StubProvider("B", 2, "result-B"));

        services.AddTieredProviderSelector<string, string>(opts => opts with
        {
            FailurePolicies =
            [
                .. opts.FailurePolicies,
                new ProviderFailurePolicy(Match: ex => ex is MyAuthException),
            ],
        });

        await using var sp = services.BuildServiceProvider();
        var selector = sp.GetRequiredService<ITieredProviderSelector<string, string>>();

        var result = await selector.ExecuteAsync("q", CancellationToken.None);

        Assert.Equal("result-B", result);
    }

    [Fact]
    public void NullServices_NoConfigureOverload_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(() =>
            services.AddTieredProviderSelector<string, string>());
    }

    [Fact]
    public void NullServices_SimpleConfigureOverload_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(() =>
            services.AddTieredProviderSelector<string, string>(opts => opts));
    }

    [Fact]
    public void NullServices_DiAwareConfigureOverload_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(() =>
            services.AddTieredProviderSelector<string, string>((_, opts) => opts));
    }

    [Fact]
    public void NullConfigure_SimpleOverload_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            services.AddTieredProviderSelector<string, string>(
                (Func<TieredProviderSelectorOptions, TieredProviderSelectorOptions>)null!));
    }

    [Fact]
    public void NullConfigure_DiAwareOverload_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            services.AddTieredProviderSelector<string, string>(
                (Func<IServiceProvider, TieredProviderSelectorOptions, TieredProviderSelectorOptions>)null!));
    }

    private static IServiceCollection BuildBaseServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IQuotaGate, AlwaysGrantQuotaGate>();
        services.AddSingleton<IAgentExecutionContextAccessor, AgentExecutionContextAccessor>();
        return services;
    }

    private sealed class StubProvider(string name, int priority, string result, bool enabled = true)
        : ITieredProvider<string, string>
    {
        public string Name => name;
        public int Priority => priority;
        public bool IsEnabled => enabled;

        public Task<string> ExecuteAsync(string query, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class IntStubProvider(string name, int priority, int result)
        : ITieredProvider<int, int>
    {
        public string Name => name;
        public int Priority => priority;
        public bool IsEnabled => true;

        public Task<int> ExecuteAsync(int query, CancellationToken cancellationToken) =>
            Task.FromResult(result);
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

    private sealed class MyAuthException(string message) : Exception(message);
}
