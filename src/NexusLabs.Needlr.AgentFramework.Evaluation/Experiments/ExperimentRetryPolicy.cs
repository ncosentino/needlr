namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Provides a bounded retry policy with explicit outcome selection and delay calculation.
/// </summary>
[DoNotAutoRegister]
public sealed class ExperimentRetryPolicy : IExperimentRetryPolicy
{
    private static readonly TimeSpan MaxDelay = TimeSpan.FromMilliseconds(uint.MaxValue - 1);
    private readonly ExperimentRetryDelayProvider _delayProvider;

    /// <summary>
    /// Initializes a bounded fixed-delay retry policy.
    /// </summary>
    /// <param name="maxAttempts">The maximum total attempt count, including the initial attempt.</param>
    /// <param name="retryOn">The execution outcomes eligible for retry.</param>
    /// <param name="delay">The delay before each selected retry.</param>
    public ExperimentRetryPolicy(
        int maxAttempts,
        ExperimentRetryableOutcome retryOn,
        TimeSpan delay)
        : this(maxAttempts, retryOn, _ => delay)
    {
        ValidateDelay(delay, nameof(delay));
    }

    /// <summary>
    /// Initializes a bounded retry policy with caller-defined delay calculation.
    /// </summary>
    /// <param name="maxAttempts">The maximum total attempt count, including the initial attempt.</param>
    /// <param name="retryOn">The execution outcomes eligible for retry.</param>
    /// <param name="delayProvider">
    /// The explicit delay calculation. Reproducible jitter requires caller-controlled deterministic
    /// state, such as a seeded random source.
    /// </param>
    public ExperimentRetryPolicy(
        int maxAttempts,
        ExperimentRetryableOutcome retryOn,
        ExperimentRetryDelayProvider delayProvider)
    {
        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxAttempts),
                maxAttempts,
                "The maximum attempt count must be positive.");
        }

        const ExperimentRetryableOutcome allOutcomes =
            ExperimentRetryableOutcome.ExecutionFailure
            | ExperimentRetryableOutcome.Timeout
            | ExperimentRetryableOutcome.TaskCancellation;
        if ((retryOn & ~allOutcomes) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retryOn),
                retryOn,
                "The retry outcome selection contains undefined values.");
        }

        ArgumentNullException.ThrowIfNull(delayProvider);
        MaxAttempts = maxAttempts;
        RetryOn = retryOn;
        _delayProvider = delayProvider;
    }

    /// <inheritdoc />
    public int MaxAttempts { get; }

    /// <summary>Gets the execution outcomes eligible for retry.</summary>
    public ExperimentRetryableOutcome RetryOn { get; }

    /// <inheritdoc />
    public ExperimentRetryDecision Decide(ExperimentRetryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var retryableOutcome = context.Attempt.Status switch
        {
            ExperimentAttemptStatus.Failed => ExperimentRetryableOutcome.ExecutionFailure,
            ExperimentAttemptStatus.TimedOut => ExperimentRetryableOutcome.Timeout,
            ExperimentAttemptStatus.Canceled => ExperimentRetryableOutcome.TaskCancellation,
            ExperimentAttemptStatus.Succeeded => ExperimentRetryableOutcome.None,
            _ => throw new ArgumentOutOfRangeException(
                nameof(context),
                context.Attempt.Status,
                "The attempt status is not defined."),
        };
        if (context.Attempt.AttemptNumber >= MaxAttempts
            || retryableOutcome == ExperimentRetryableOutcome.None
            || (RetryOn & retryableOutcome) == 0)
        {
            return ExperimentRetryDecision.DoNotRetry();
        }

        var delay = _delayProvider(context);
        return ExperimentRetryDecision.RetryAfter(delay);
    }

    internal static void ValidateDelay(TimeSpan delay, string parameterName)
    {
        if (delay < TimeSpan.Zero
            || delay == Timeout.InfiniteTimeSpan
            || delay > MaxDelay)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                delay,
                $"An experiment retry delay must be non-negative and no greater than {MaxDelay}.");
        }
    }
}
