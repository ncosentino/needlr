namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Creates immutable structured failures from caught exceptions.
/// </summary>
internal static class ExperimentFailureFactory
{
    public static ExperimentFailure Create(
        ExperimentFailureCode code,
        ExperimentFailureStage stage,
        Exception exception,
        string? message = null,
        bool isRetryable = false)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new ExperimentFailure(
            code,
            stage,
            exception.GetType().FullName ?? exception.GetType().Name,
            message ?? exception.Message,
            isRetryable);
    }

    public static ExperimentFailure ValidateAndSnapshotPublicationFailure(
        ExperimentFailure failure,
        ExperimentFailureCode expectedCode,
        string owner)
    {
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        if (failure.Code != expectedCode
            || failure.Stage != ExperimentFailureStage.Publication
            || failure.IsRetryable)
        {
            throw new InvalidOperationException(
                $"{owner} returned an invalid publication failure.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(failure.ExceptionType);
        ArgumentException.ThrowIfNullOrWhiteSpace(failure.Message);
        return new ExperimentFailure(
            failure.Code,
            failure.Stage,
            failure.ExceptionType,
            failure.Message,
            isRetryable: false);
    }
}
