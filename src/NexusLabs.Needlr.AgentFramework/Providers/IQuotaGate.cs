namespace NexusLabs.Needlr.AgentFramework.Providers;

/// <summary>
/// Gates provider access based on quota. Before each provider attempt, the selector
/// calls <see cref="TryReserveAsync"/>. If denied, the provider is skipped.
/// After the attempt, <see cref="ReleaseAsync"/> reports success or failure.
/// </summary>
/// <remarks>
/// <para>
/// Quota can be global (pass <see langword="null"/> for <c>quotaPartition</c>) or
/// scoped to a partition (e.g., a user, tenant, or API key). In multi-tenant
/// environments, one partition exhausting its quota should not degrade service
/// for other partitions. The partition identifier is opaque to the framework —
/// consumers decide what it represents.
/// </para>
/// <para>
/// <see cref="TieredProviderSelector{TQuery, TResult}"/> reads the partition from
/// the ambient <see cref="Context.IAgentExecutionContextAccessor"/> by default,
/// using <see cref="Context.IAgentExecutionContext.UserId"/> as the partition key.
/// Consumers can override this by providing a custom
/// <see cref="QuotaPartitionSelector"/> to the selector.
/// </para>
/// </remarks>
public interface IQuotaGate
{
    /// <summary>
    /// Attempts to reserve quota for the given provider and partition.
    /// Returns <see langword="true"/> if the reservation was granted, <see langword="false"/> if denied.
    /// </summary>
    /// <param name="providerName">The provider requesting quota.</param>
    /// <param name="quotaPartition">
    /// Partition key that scopes the quota (e.g., user ID, tenant ID, API key).
    /// Pass <see langword="null"/> for global (unpartitioned) quota.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> TryReserveAsync(string providerName, string? quotaPartition, CancellationToken cancellationToken);

    /// <summary>
    /// Releases a previously granted reservation, reporting whether the attempt succeeded.
    /// </summary>
    /// <param name="providerName">The provider that was attempted.</param>
    /// <param name="quotaPartition">
    /// The same partition key that was passed to <see cref="TryReserveAsync"/>.
    /// </param>
    /// <param name="succeeded">Whether the provider call succeeded.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReleaseAsync(string providerName, string? quotaPartition, bool succeeded, CancellationToken cancellationToken);
}

/// <summary>
/// Extracts the quota partition key from the current execution context.
/// Used by <see cref="TieredProviderSelector{TQuery, TResult}"/> to scope
/// quota to the ambient user/tenant without explicit passing at every call site.
/// </summary>
/// <param name="context">The current execution context, or <see langword="null"/> if outside a scope.</param>
/// <returns>The partition key, or <see langword="null"/> for global quota.</returns>
public delegate string? QuotaPartitionSelector(Context.IAgentExecutionContext? context);
