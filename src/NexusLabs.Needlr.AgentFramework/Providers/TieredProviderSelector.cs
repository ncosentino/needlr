using System.Collections.Concurrent;

using NexusLabs.Needlr.AgentFramework.Context;

namespace NexusLabs.Needlr.AgentFramework.Providers;

/// <summary>
/// Default <see cref="ITieredProviderSelector{TQuery, TResult}"/> that iterates providers
/// in ascending <see cref="ITieredProvider{TQuery, TResult}.Priority"/> order, gated by
/// an <see cref="IQuotaGate"/>. Exception handling is configurable via
/// <see cref="TieredProviderSelectorOptions.FailurePolicies"/>; the default options
/// (<see cref="TieredProviderSelectorOptions.Default"/>) preserve the framework's
/// historical behaviour of falling through to the next provider on
/// <see cref="ProviderUnavailableException"/>.
/// </summary>
/// <remarks>
/// <para>
/// The quota partition key is resolved from the ambient
/// <see cref="IAgentExecutionContextAccessor"/> using a <see cref="QuotaPartitionSelector"/>.
/// By default, <see cref="IAgentExecutionContext.UserId"/> is used as the partition.
/// Consumers that need a different partitioning strategy (e.g., tenant ID, API key)
/// can provide a custom <see cref="QuotaPartitionSelector"/> via the constructor.
/// </para>
/// <para>
/// When no execution context is active (e.g., during integration tests that don't
/// establish a scope), the partition is <see langword="null"/> and quota is global.
/// </para>
/// <para>
/// <b>Failure policies and skip cache.</b> When a provider throws,
/// <see cref="TieredProviderSelectorOptions.FailurePolicies"/> are evaluated in order
/// against the thrown exception (first match wins). A matching policy causes the
/// selector to fall through to the next provider; if the policy specifies a
/// <see cref="ProviderFailurePolicy.SkipDuration"/>, an entry is added to a per-instance
/// in-memory skip cache so subsequent calls bypass the failing provider until the
/// skip-until timestamp elapses. The cache is a thread-safe
/// <see cref="ConcurrentDictionary{TKey, TValue}"/> keyed by provider name. Skip state
/// is per-selector-instance and lives only in the host process; it is not persisted.
/// </para>
/// <para>
/// <b>Quota release.</b> Quota release happens in a single <see langword="finally"/>
/// block so it runs on the success path, on a matched-policy fall-through, on an
/// unmatched-exception re-throw, and even when a
/// <see cref="ProviderFailurePolicy.OnHit"/> callback throws. The release records
/// <c>succeeded: true</c> only when the provider returned a value.
/// </para>
/// <para>
/// <b>Cancellation.</b> When the active <see cref="CancellationToken"/> is cancelled,
/// the selector skips policy evaluation entirely and propagates the
/// <see cref="OperationCanceledException"/> directly. Cancelled calls do not apply
/// skip mode and do not fall through to the next provider.
/// </para>
/// </remarks>
[DoNotAutoRegister]
public sealed class TieredProviderSelector<TQuery, TResult> : ITieredProviderSelector<TQuery, TResult>
{
    private readonly IReadOnlyList<ITieredProvider<TQuery, TResult>> _providers;
    private readonly IQuotaGate _quotaGate;
    private readonly IAgentExecutionContextAccessor _contextAccessor;
    private readonly QuotaPartitionSelector _partitionSelector;
    private readonly TieredProviderSelectorOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _skipUntil =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The default partition selector: uses <see cref="IAgentExecutionContext.UserId"/>
    /// from the ambient context.
    /// </summary>
    public static readonly QuotaPartitionSelector DefaultPartitionSelector =
        context => context?.UserId;

    /// <param name="providers">All registered providers (filtering and ordering is handled internally).</param>
    /// <param name="quotaGate">Quota gate for reservation/release. Use <see cref="AlwaysGrantQuotaGate"/> for no-op.</param>
    /// <param name="contextAccessor">Accessor for ambient execution context (provides partition identity).</param>
    /// <param name="partitionSelector">
    /// Custom partition selector. Defaults to <see cref="DefaultPartitionSelector"/>
    /// (<see cref="IAgentExecutionContext.UserId"/>).
    /// </param>
    /// <param name="options">
    /// Failure-handling policy configuration. Defaults to
    /// <see cref="TieredProviderSelectorOptions.Default"/>, which falls through on
    /// <see cref="ProviderUnavailableException"/> with no skip and no callback.
    /// </param>
    /// <param name="timeProvider">
    /// Time source used for skip-cache "now" comparisons. Defaults to
    /// <see cref="TimeProvider.System"/>. Inject a fake time provider in tests to
    /// drive deterministic skip-mode behaviour.
    /// </param>
    public TieredProviderSelector(
        IEnumerable<ITieredProvider<TQuery, TResult>> providers,
        IQuotaGate quotaGate,
        IAgentExecutionContextAccessor contextAccessor,
        QuotaPartitionSelector? partitionSelector = null,
        TieredProviderSelectorOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(quotaGate);
        ArgumentNullException.ThrowIfNull(contextAccessor);

        _providers = providers
            .Where(p => p.IsEnabled)
            .OrderBy(p => p.Priority)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _quotaGate = quotaGate;
        _contextAccessor = contextAccessor;
        _partitionSelector = partitionSelector ?? DefaultPartitionSelector;
        _options = options ?? TieredProviderSelectorOptions.Default;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<TResult> ExecuteAsync(TQuery query, CancellationToken cancellationToken)
    {
        if (_providers.Count == 0)
            throw new NoProvidersRegisteredException();

        var partition = _partitionSelector(_contextAccessor.Current);
        var attempts = new List<string>();

        foreach (var provider in _providers)
        {
            var now = _timeProvider.GetUtcNow();
            if (_skipUntil.TryGetValue(provider.Name, out var skipUntilCached) &&
                skipUntilCached > now)
            {
                attempts.Add(skipUntilCached == DateTimeOffset.MaxValue
                    ? $"{provider.Name}: skipped indefinitely"
                    : $"{provider.Name}: skipped until {skipUntilCached:o}");
                continue;
            }

            if (!await _quotaGate.TryReserveAsync(provider.Name, partition, cancellationToken)
                .ConfigureAwait(false))
            {
                attempts.Add($"{provider.Name}: quota denied");
                continue;
            }

            var succeeded = false;
            try
            {
                var result = await provider.ExecuteAsync(query, cancellationToken)
                    .ConfigureAwait(false);
                succeeded = true;
                return result;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                ProviderFailurePolicy? matched = null;
                foreach (var policy in _options.FailurePolicies)
                {
                    if (policy.Match(ex))
                    {
                        matched = policy;
                        break;
                    }
                }

                if (matched is null)
                {
                    throw;
                }

                attempts.Add($"{provider.Name}: {ex.Message}");

                DateTimeOffset? skipUntil = null;
                if (matched.SkipDuration is { } duration)
                {
                    skipUntil = ComputeSkipUntil(now, duration);
                    _skipUntil[provider.Name] = skipUntil.Value;
                }

                if (matched.OnHit is { } onHit)
                {
                    await onHit(new ProviderFailureContext(provider.Name, ex, skipUntil))
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                await _quotaGate
                    .ReleaseAsync(provider.Name, partition, succeeded, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        throw new AllProvidersFailedException(attempts);
    }

    private static DateTimeOffset ComputeSkipUntil(DateTimeOffset now, TimeSpan duration)
    {
        if (DateTimeOffset.MaxValue - now <= duration)
        {
            return DateTimeOffset.MaxValue;
        }
        return now + duration;
    }
}
