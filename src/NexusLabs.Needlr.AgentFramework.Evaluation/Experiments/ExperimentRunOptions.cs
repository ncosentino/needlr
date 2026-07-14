namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Configures one provider-neutral experiment run.
/// </summary>
public sealed class ExperimentRunOptions
{
    /// <summary>Gets the caller-supplied stable run identifier.</summary>
    public required string RunId { get; init; }

    /// <summary>Gets the required maximum number of active task attempts.</summary>
    public required int MaxConcurrency { get; init; }

    /// <summary>
    /// Gets an optional cooperative timeout applied independently to each task attempt.
    /// </summary>
    public TimeSpan? AttemptTimeout { get; init; }
}
