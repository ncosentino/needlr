namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Configures one provider-neutral experiment run.
/// </summary>
public sealed record ExperimentRunOptions
{
    /// <summary>Gets the caller-supplied stable run identifier.</summary>
    public required string RunId { get; init; }

    /// <summary>Gets the required maximum number of active task attempts.</summary>
    public required int MaxConcurrency { get; init; }

    /// <summary>
    /// Gets an optional cooperative timeout applied independently to each task attempt.
    /// </summary>
    public TimeSpan? AttemptTimeout { get; init; }

    /// <summary>
    /// Gets the optional bounded execution retry policy. A missing policy performs one attempt.
    /// </summary>
    public IExperimentRetryPolicy? RetryPolicy { get; init; }

    /// <summary>
    /// Gets the optional caller-owned concurrency limiter shared across experiment runs.
    /// </summary>
    public IExperimentConcurrencyLimiter? SharedLimiter { get; init; }

    /// <summary>
    /// Gets the total time allowed for item-scope abort and disposal after cancellation, or for
    /// bounded disposal after terminal completion.
    /// </summary>
    public TimeSpan ItemScopeCleanupTimeout { get; init; } = TimeSpan.FromSeconds(30);
}
