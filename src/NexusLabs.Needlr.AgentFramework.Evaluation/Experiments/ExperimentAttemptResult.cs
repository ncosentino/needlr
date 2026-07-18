namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Describes one operational execution attempt.
/// </summary>
public sealed record ExperimentAttemptResult
{
    private ExperimentAttemptResult(
        int attemptNumber,
        ExperimentAttemptStatus status,
        DateTimeOffset startedAt,
        TimeSpan duration,
        ExperimentFailure? failure,
        TimeSpan? delayBeforeNextAttempt)
    {
        AttemptNumber = attemptNumber;
        Status = status;
        StartedAt = startedAt;
        Duration = duration;
        Failure = failure;
        DelayBeforeNextAttempt = delayBeforeNextAttempt;
    }

    /// <summary>Gets the one-based attempt number.</summary>
    public int AttemptNumber { get; }

    /// <summary>Gets the terminal attempt status.</summary>
    public ExperimentAttemptStatus Status { get; }

    /// <summary>Gets the UTC attempt start time.</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>Gets the elapsed attempt duration.</summary>
    public TimeSpan Duration { get; }

    /// <summary>Gets the structured failure, when present.</summary>
    public ExperimentFailure? Failure { get; }

    /// <summary>
    /// Gets the scheduled delay before the next attempt, or <see langword="null"/> when no retry
    /// followed this attempt.
    /// </summary>
    public TimeSpan? DelayBeforeNextAttempt { get; }

    /// <summary>
    /// Creates a successful attempt that carries no failure and no scheduled retry delay.
    /// </summary>
    /// <param name="attemptNumber">The one-based attempt number.</param>
    /// <param name="startedAt">The UTC attempt start time.</param>
    /// <param name="duration">The elapsed attempt duration.</param>
    /// <returns>A successful attempt result.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="attemptNumber"/> is not positive, or <paramref name="duration"/> is
    /// negative.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="startedAt"/> does not use a UTC (zero) offset.
    /// </exception>
    public static ExperimentAttemptResult Succeeded(
        int attemptNumber,
        DateTimeOffset startedAt,
        TimeSpan duration)
    {
        ValidateCommon(attemptNumber, startedAt, duration);
        return new ExperimentAttemptResult(
            attemptNumber,
            ExperimentAttemptStatus.Succeeded,
            startedAt,
            duration,
            failure: null,
            delayBeforeNextAttempt: null);
    }

    /// <summary>
    /// Creates a terminal unsuccessful attempt whose failure is recorded as non-retryable.
    /// </summary>
    /// <param name="attemptNumber">The one-based attempt number.</param>
    /// <param name="status">The terminal non-success status.</param>
    /// <param name="startedAt">The UTC attempt start time.</param>
    /// <param name="duration">The elapsed attempt duration.</param>
    /// <param name="failure">The structured failure that ended the attempt.</param>
    /// <returns>
    /// An unsuccessful attempt result whose failure is a non-retryable copy of
    /// <paramref name="failure"/>. The caller-supplied instance is not modified.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="attemptNumber"/> is not positive, <paramref name="duration"/> is negative,
    /// or <paramref name="status"/> is not a defined enumeration value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="startedAt"/> does not use a UTC (zero) offset, or
    /// <paramref name="status"/> is <see cref="ExperimentAttemptStatus.Succeeded"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is <see langword="null"/>.</exception>
    public static ExperimentAttemptResult Unsuccessful(
        int attemptNumber,
        ExperimentAttemptStatus status,
        DateTimeOffset startedAt,
        TimeSpan duration,
        ExperimentFailure failure)
    {
        ValidateCommon(attemptNumber, startedAt, duration);
        ValidateUnsuccessfulStatus(status);
        ArgumentNullException.ThrowIfNull(failure);
        return new ExperimentAttemptResult(
            attemptNumber,
            status,
            startedAt,
            duration,
            CopyFailure(failure, isRetryable: false),
            delayBeforeNextAttempt: null);
    }

    /// <summary>
    /// Creates an unsuccessful attempt that scheduled another attempt, recording its failure as
    /// retryable and capturing the retry delay.
    /// </summary>
    /// <param name="attemptNumber">The one-based attempt number.</param>
    /// <param name="status">The terminal non-success status.</param>
    /// <param name="startedAt">The UTC attempt start time.</param>
    /// <param name="duration">The elapsed attempt duration.</param>
    /// <param name="failure">The structured failure that ended the attempt.</param>
    /// <param name="delayBeforeNextAttempt">
    /// The delay before the scheduled retry becomes ready.
    /// </param>
    /// <returns>
    /// An unsuccessful attempt result whose failure is a retryable copy of
    /// <paramref name="failure"/>. The caller-supplied instance is not modified.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="attemptNumber"/> is not positive, <paramref name="duration"/> is negative,
    /// <paramref name="status"/> is not a defined enumeration value, or
    /// <paramref name="delayBeforeNextAttempt"/> is negative, infinite, or greater than the maximum
    /// representable experiment retry delay.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="startedAt"/> does not use a UTC (zero) offset, or
    /// <paramref name="status"/> is <see cref="ExperimentAttemptStatus.Succeeded"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is <see langword="null"/>.</exception>
    public static ExperimentAttemptResult RetryScheduled(
        int attemptNumber,
        ExperimentAttemptStatus status,
        DateTimeOffset startedAt,
        TimeSpan duration,
        ExperimentFailure failure,
        TimeSpan delayBeforeNextAttempt)
    {
        ValidateCommon(attemptNumber, startedAt, duration);
        ValidateUnsuccessfulStatus(status);
        ArgumentNullException.ThrowIfNull(failure);
        ExperimentRetryPolicy.ValidateDelay(
            delayBeforeNextAttempt,
            nameof(delayBeforeNextAttempt));
        return new ExperimentAttemptResult(
            attemptNumber,
            status,
            startedAt,
            duration,
            CopyFailure(failure, isRetryable: true),
            delayBeforeNextAttempt);
    }

    private static void ValidateCommon(
        int attemptNumber,
        DateTimeOffset startedAt,
        TimeSpan duration)
    {
        if (attemptNumber < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attemptNumber),
                attemptNumber,
                "The attempt number must be positive.");
        }

        if (startedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The attempt start time must use a UTC (zero) offset.",
                nameof(startedAt));
        }

        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                duration,
                "The attempt duration must be non-negative.");
        }
    }

    private static void ValidateUnsuccessfulStatus(ExperimentAttemptStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "The attempt status is not defined.");
        }

        if (status == ExperimentAttemptStatus.Succeeded)
        {
            throw new ArgumentException(
                "An unsuccessful attempt cannot use the succeeded status.",
                nameof(status));
        }
    }

    private static ExperimentFailure CopyFailure(ExperimentFailure failure, bool isRetryable) =>
        new(
            failure.Code,
            failure.Stage,
            failure.ExceptionType,
            failure.Message,
            isRetryable);
}
