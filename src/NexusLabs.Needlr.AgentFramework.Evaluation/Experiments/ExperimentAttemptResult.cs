namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Describes one operational execution attempt.
/// </summary>
public sealed record ExperimentAttemptResult
{
    /// <summary>Gets the one-based attempt number.</summary>
    public required int AttemptNumber { get; init; }

    /// <summary>Gets the terminal attempt status.</summary>
    public required ExperimentAttemptStatus Status { get; init; }

    /// <summary>Gets the UTC attempt start time.</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>Gets the elapsed attempt duration.</summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>Gets the structured failure, when present.</summary>
    public ExperimentFailure? Failure { get; init; }

    /// <summary>
    /// Gets the scheduled delay before the next attempt, or <see langword="null"/> when no retry
    /// followed this attempt.
    /// </summary>
    public TimeSpan? DelayBeforeNextAttempt { get; init; }
}
