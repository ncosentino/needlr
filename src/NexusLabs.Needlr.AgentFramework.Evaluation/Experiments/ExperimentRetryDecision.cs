namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Describes whether and when a failed execution attempt should be retried.
/// </summary>
public sealed class ExperimentRetryDecision
{
    /// <summary>Gets a value indicating whether another attempt should be scheduled.</summary>
    public required bool ShouldRetry { get; init; }

    /// <summary>Gets the delay before the next attempt becomes ready.</summary>
    public TimeSpan Delay { get; init; }
}
