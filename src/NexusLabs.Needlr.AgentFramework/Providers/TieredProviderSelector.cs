namespace NexusLabs.Needlr.AgentFramework.Providers;

/// <summary>
/// Default <see cref="ITieredProviderSelector{TQuery, TResult}"/> that iterates providers
/// in ascending <see cref="ITieredProvider{TQuery, TResult}.Priority"/> order, gated by
/// an <see cref="IQuotaGate"/>. On <see cref="ProviderUnavailableException"/> the selector
/// falls through to the next provider.
/// </summary>
[DoNotAutoRegister]
public sealed class TieredProviderSelector<TQuery, TResult> : ITieredProviderSelector<TQuery, TResult>
{
    private readonly IReadOnlyList<ITieredProvider<TQuery, TResult>> _providers;
    private readonly IQuotaGate _quotaGate;

    /// <param name="providers">All registered providers (filtering and ordering is handled internally).</param>
    /// <param name="quotaGate">Quota gate for reservation/release. Use <see cref="AlwaysGrantQuotaGate"/> for no-op.</param>
    public TieredProviderSelector(
        IEnumerable<ITieredProvider<TQuery, TResult>> providers,
        IQuotaGate quotaGate)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(quotaGate);

        _providers = providers
            .Where(p => p.IsEnabled)
            .OrderBy(p => p.Priority)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _quotaGate = quotaGate;
    }

    /// <inheritdoc />
    public async Task<TResult> ExecuteAsync(TQuery query, CancellationToken cancellationToken)
    {
        if (_providers.Count == 0)
            throw new InvalidOperationException("No enabled providers are registered.");

        var attempts = new List<string>();

        foreach (var provider in _providers)
        {
            if (!await _quotaGate.TryReserveAsync(provider.Name, cancellationToken)
                .ConfigureAwait(false))
            {
                attempts.Add($"{provider.Name}: quota denied");
                continue;
            }

            try
            {
                var result = await provider.ExecuteAsync(query, cancellationToken)
                    .ConfigureAwait(false);

                await _quotaGate.ReleaseAsync(provider.Name, succeeded: true, cancellationToken)
                    .ConfigureAwait(false);

                return result;
            }
            catch (ProviderUnavailableException ex)
            {
                attempts.Add($"{provider.Name}: {ex.Message}");

                await _quotaGate.ReleaseAsync(provider.Name, succeeded: false, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException(
            $"All providers failed. Attempts: [{string.Join("; ", attempts)}]");
    }
}
