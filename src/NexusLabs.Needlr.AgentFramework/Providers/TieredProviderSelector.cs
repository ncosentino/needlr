using NexusLabs.Needlr.AgentFramework.Context;

namespace NexusLabs.Needlr.AgentFramework.Providers;

/// <summary>
/// Default <see cref="ITieredProviderSelector{TQuery, TResult}"/> that iterates providers
/// in ascending <see cref="ITieredProvider{TQuery, TResult}.Priority"/> order, gated by
/// an <see cref="IQuotaGate"/>. On <see cref="ProviderUnavailableException"/> the selector
/// falls through to the next provider.
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
/// </remarks>
[DoNotAutoRegister]
public sealed class TieredProviderSelector<TQuery, TResult> : ITieredProviderSelector<TQuery, TResult>
{
    private readonly IReadOnlyList<ITieredProvider<TQuery, TResult>> _providers;
    private readonly IQuotaGate _quotaGate;
    private readonly IAgentExecutionContextAccessor _contextAccessor;
    private readonly QuotaPartitionSelector _partitionSelector;

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
    public TieredProviderSelector(
        IEnumerable<ITieredProvider<TQuery, TResult>> providers,
        IQuotaGate quotaGate,
        IAgentExecutionContextAccessor contextAccessor,
        QuotaPartitionSelector? partitionSelector = null)
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
    }

    /// <inheritdoc />
    public async Task<TResult> ExecuteAsync(TQuery query, CancellationToken cancellationToken)
    {
        if (_providers.Count == 0)
            throw new InvalidOperationException("No enabled providers are registered.");

        var partition = _partitionSelector(_contextAccessor.Current);
        var attempts = new List<string>();

        foreach (var provider in _providers)
        {
            if (!await _quotaGate.TryReserveAsync(provider.Name, partition, cancellationToken)
                .ConfigureAwait(false))
            {
                attempts.Add($"{provider.Name}: quota denied");
                continue;
            }

            try
            {
                var result = await provider.ExecuteAsync(query, cancellationToken)
                    .ConfigureAwait(false);

                await _quotaGate.ReleaseAsync(provider.Name, partition, succeeded: true, cancellationToken)
                    .ConfigureAwait(false);

                return result;
            }
            catch (ProviderUnavailableException ex)
            {
                attempts.Add($"{provider.Name}: {ex.Message}");

                await _quotaGate.ReleaseAsync(provider.Name, partition, succeeded: false, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException(
            $"All providers failed. Attempts: [{string.Join("; ", attempts)}]");
    }
}
