namespace NexusLabs.Needlr.AgentFramework.Providers;

/// <summary>
/// <see cref="IQuotaGate"/> that always grants reservations. Suitable for development
/// and scenarios where quota tracking is not needed.
/// </summary>
public sealed class AlwaysGrantQuotaGate : IQuotaGate
{
    /// <inheritdoc />
    public Task<bool> TryReserveAsync(string providerName, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    /// <inheritdoc />
    public Task ReleaseAsync(string providerName, bool succeeded, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
