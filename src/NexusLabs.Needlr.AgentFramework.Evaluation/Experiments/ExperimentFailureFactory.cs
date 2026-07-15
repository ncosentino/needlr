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
        return new ExperimentFailure
        {
            Code = code,
            Stage = stage,
            ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
            Message = message ?? exception.Message,
            IsRetryable = isRetryable,
        };
    }
}
