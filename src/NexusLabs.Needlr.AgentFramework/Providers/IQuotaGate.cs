namespace NexusLabs.Needlr.AgentFramework.Providers;

/// <summary>
/// Gates provider access based on quota. Before each provider attempt, the selector
/// calls <see cref="TryReserveAsync"/>. If denied, the provider is skipped.
/// After the attempt, <see cref="ReleaseAsync"/> reports success or failure.
/// </summary>
public interface IQuotaGate
{
    /// <summary>
    /// Attempts to reserve quota for the given provider.
    /// Returns <see langword="true"/> if the reservation was granted, <see langword="false"/> if denied.
    /// </summary>
    /// <param name="providerName">The provider requesting quota.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> TryReserveAsync(string providerName, CancellationToken cancellationToken);

    /// <summary>
    /// Releases a previously granted reservation, reporting whether the attempt succeeded.
    /// </summary>
    /// <param name="providerName">The provider that was attempted.</param>
    /// <param name="succeeded">Whether the provider call succeeded.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReleaseAsync(string providerName, bool succeeded, CancellationToken cancellationToken);
}
