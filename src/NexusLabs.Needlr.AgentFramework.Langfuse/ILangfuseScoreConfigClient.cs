namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Idempotently registers Langfuse score configs so eval scores appear on the dashboard with
/// defined data types, ranges, and category sets.
/// </summary>
public interface ILangfuseScoreConfigClient
{
    /// <summary>
    /// Gets a value indicating whether score configs are being created. <see langword="false"/>
    /// when Langfuse is not configured, in which case <see cref="EnsureScoreConfigAsync"/> is a
    /// no-op.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Ensures a score config with the given name exists, creating it only when absent. Safe to
    /// call on every run.
    /// </summary>
    /// <param name="config">The score config to ensure.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the config exists in Langfuse.</returns>
    Task EnsureScoreConfigAsync(LangfuseScoreConfig config, CancellationToken cancellationToken = default);
}
