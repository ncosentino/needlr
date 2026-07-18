namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Describes whether and when a failed execution attempt should be retried.
/// </summary>
public sealed record ExperimentRetryDecision
{
    private ExperimentRetryDecision(bool shouldRetry, TimeSpan delay)
    {
        ShouldRetry = shouldRetry;
        Delay = delay;
    }

    /// <summary>Gets a value indicating whether another attempt should be scheduled.</summary>
    public bool ShouldRetry { get; }

    /// <summary>Gets the delay before the next attempt becomes ready.</summary>
    public TimeSpan Delay { get; }

    /// <summary>
    /// Creates a decision that stops retrying and records a zero delay.
    /// </summary>
    /// <returns>A decision indicating that no further attempt should be scheduled.</returns>
    public static ExperimentRetryDecision DoNotRetry() =>
        new(shouldRetry: false, TimeSpan.Zero);

    /// <summary>
    /// Creates a decision that schedules another attempt after the specified delay.
    /// </summary>
    /// <param name="delay">
    /// The delay before the next attempt becomes ready. The delay must be non-negative and no
    /// greater than the maximum delay enforced by <see cref="ExperimentRetryPolicy"/>.
    /// </param>
    /// <returns>A decision indicating that another attempt should be scheduled.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="delay"/> is negative, infinite, or greater than the maximum representable
    /// experiment retry delay.
    /// </exception>
    public static ExperimentRetryDecision RetryAfter(TimeSpan delay)
    {
        ExperimentRetryPolicy.ValidateDelay(delay, nameof(delay));
        return new ExperimentRetryDecision(shouldRetry: true, delay);
    }
}
