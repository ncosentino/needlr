using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

public sealed class ExperimentAttemptResultTests
{
    private static readonly DateTimeOffset StartedAtUtc =
        new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan MaxDelay = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    [Fact]
    public void Succeeded_ValidArguments_HasNoFailureOrDelay()
    {
        var attempt = ExperimentAttemptResult.Succeeded(
            attemptNumber: 1,
            StartedAtUtc,
            TimeSpan.FromMilliseconds(5));

        Assert.Equal(1, attempt.AttemptNumber);
        Assert.Equal(ExperimentAttemptStatus.Succeeded, attempt.Status);
        Assert.Equal(StartedAtUtc, attempt.StartedAt);
        Assert.Equal(TimeSpan.FromMilliseconds(5), attempt.Duration);
        Assert.Null(attempt.Failure);
        Assert.Null(attempt.DelayBeforeNextAttempt);
    }

    [Fact]
    public void Succeeded_NonPositiveAttemptNumber_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ExperimentAttemptResult.Succeeded(0, StartedAtUtc, TimeSpan.Zero));
    }

    [Fact]
    public void Succeeded_NonUtcStartedAt_Throws()
    {
        var nonUtc = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(() =>
            ExperimentAttemptResult.Succeeded(1, nonUtc, TimeSpan.Zero));
    }

    [Fact]
    public void Succeeded_NegativeDuration_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ExperimentAttemptResult.Succeeded(1, StartedAtUtc, TimeSpan.FromMilliseconds(-1)));
    }

    [Fact]
    public void Unsuccessful_CopiesFailureAsNonRetryable_WithoutMutatingCaller()
    {
        var retryableFailure = CreateFailure(isRetryable: true);

        var attempt = ExperimentAttemptResult.Unsuccessful(
            attemptNumber: 2,
            ExperimentAttemptStatus.Failed,
            StartedAtUtc,
            TimeSpan.FromMilliseconds(10),
            retryableFailure);

        Assert.Equal(ExperimentAttemptStatus.Failed, attempt.Status);
        Assert.NotNull(attempt.Failure);
        Assert.NotSame(retryableFailure, attempt.Failure);
        Assert.False(
            attempt.Failure!.IsRetryable,
            "Expected a terminal unsuccessful attempt to carry a non-retryable failure.");
        Assert.Null(attempt.DelayBeforeNextAttempt);
        Assert.True(
            retryableFailure.IsRetryable,
            "Expected factory construction not to mutate the caller's failure.");
    }

    [Fact]
    public void Unsuccessful_SucceededStatus_Throws()
    {
        Assert.Throws<ArgumentException>(() => ExperimentAttemptResult.Unsuccessful(
            1,
            ExperimentAttemptStatus.Succeeded,
            StartedAtUtc,
            TimeSpan.Zero,
            CreateFailure(isRetryable: false)));
    }

    [Fact]
    public void Unsuccessful_UndefinedStatus_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ExperimentAttemptResult.Unsuccessful(
            1,
            (ExperimentAttemptStatus)99,
            StartedAtUtc,
            TimeSpan.Zero,
            CreateFailure(isRetryable: false)));
    }

    [Fact]
    public void Unsuccessful_NullFailure_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ExperimentAttemptResult.Unsuccessful(
            1,
            ExperimentAttemptStatus.Failed,
            StartedAtUtc,
            TimeSpan.Zero,
            null!));
    }

    [Fact]
    public void RetryScheduled_CopiesFailureAsRetryable_WithoutMutatingCaller()
    {
        var nonRetryableFailure = CreateFailure(isRetryable: false);

        var attempt = ExperimentAttemptResult.RetryScheduled(
            attemptNumber: 3,
            ExperimentAttemptStatus.TimedOut,
            StartedAtUtc,
            TimeSpan.FromMilliseconds(20),
            nonRetryableFailure,
            TimeSpan.FromSeconds(1));

        Assert.Equal(ExperimentAttemptStatus.TimedOut, attempt.Status);
        Assert.NotNull(attempt.Failure);
        Assert.NotSame(nonRetryableFailure, attempt.Failure);
        Assert.True(
            attempt.Failure!.IsRetryable,
            "Expected a scheduled retry to carry a retryable failure.");
        Assert.Equal(TimeSpan.FromSeconds(1), attempt.DelayBeforeNextAttempt);
        Assert.False(
            nonRetryableFailure.IsRetryable,
            "Expected factory construction not to mutate the caller's failure.");
    }

    [Fact]
    public void Unsuccessful_AlreadyNonRetryableFailure_StillCopies()
    {
        var nonRetryableFailure = CreateFailure(isRetryable: false);

        var attempt = ExperimentAttemptResult.Unsuccessful(
            attemptNumber: 1,
            ExperimentAttemptStatus.Failed,
            StartedAtUtc,
            TimeSpan.Zero,
            nonRetryableFailure);

        Assert.NotSame(nonRetryableFailure, attempt.Failure);
        Assert.False(
            attempt.Failure!.IsRetryable,
            "Expected unsuccessful attempts to normalize failures as non-retryable.");
    }

    [Fact]
    public void RetryScheduled_AlreadyRetryableFailure_StillCopies()
    {
        var retryableFailure = CreateFailure(isRetryable: true);

        var attempt = ExperimentAttemptResult.RetryScheduled(
            attemptNumber: 1,
            ExperimentAttemptStatus.Failed,
            StartedAtUtc,
            TimeSpan.Zero,
            retryableFailure,
            delayBeforeNextAttempt: TimeSpan.Zero);

        Assert.NotSame(retryableFailure, attempt.Failure);
        Assert.True(
            attempt.Failure!.IsRetryable,
            "Expected scheduled retries to normalize failures as retryable.");
    }

    [Fact]
    public void RetryScheduled_MaximumDelay_IsAllowed()
    {
        var attempt = ExperimentAttemptResult.RetryScheduled(
            1,
            ExperimentAttemptStatus.Failed,
            StartedAtUtc,
            TimeSpan.Zero,
            CreateFailure(isRetryable: false),
            MaxDelay);

        Assert.Equal(MaxDelay, attempt.DelayBeforeNextAttempt);
    }

    [Fact]
    public void RetryScheduled_DelayAboveMaximum_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ExperimentAttemptResult.RetryScheduled(
            1,
            ExperimentAttemptStatus.Failed,
            StartedAtUtc,
            TimeSpan.Zero,
            CreateFailure(isRetryable: false),
            MaxDelay + TimeSpan.FromMilliseconds(1)));
    }

    [Fact]
    public void RetryScheduled_SucceededStatus_Throws()
    {
        Assert.Throws<ArgumentException>(() => ExperimentAttemptResult.RetryScheduled(
            1,
            ExperimentAttemptStatus.Succeeded,
            StartedAtUtc,
            TimeSpan.Zero,
            CreateFailure(isRetryable: false),
            TimeSpan.Zero));
    }

    [Fact]
    public void Contract_HasNoPublicConstructors()
    {
        var type = typeof(ExperimentAttemptResult);
        Assert.True(type.IsSealed, "Expected attempt results to remain sealed.");
        Assert.Empty(type.GetConstructors());
    }

    private static ExperimentFailure CreateFailure(bool isRetryable) =>
        new(
            ExperimentFailureCode.ExecutionFailed,
            ExperimentFailureStage.Execution,
            typeof(InvalidOperationException).FullName!,
            "boom",
            isRetryable);
}
