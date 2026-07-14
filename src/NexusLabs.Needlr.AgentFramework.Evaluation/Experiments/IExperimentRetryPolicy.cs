namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Selects bounded execution retries for experiment items.
/// </summary>
public interface IExperimentRetryPolicy
{
    /// <summary>Gets the maximum number of attempts permitted for one item.</summary>
    int MaxAttempts { get; }

    /// <summary>
    /// Decides whether a completed execution attempt should be retried.
    /// </summary>
    /// <param name="context">The completed attempt and stable item identity.</param>
    /// <returns>The retry decision.</returns>
    ExperimentRetryDecision Decide(ExperimentRetryContext context);
}
