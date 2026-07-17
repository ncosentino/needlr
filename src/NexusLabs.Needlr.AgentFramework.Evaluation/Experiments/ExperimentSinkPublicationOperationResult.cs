namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Describes result-sink publication work before the runner applies registered sink identity.
/// </summary>
public sealed record ExperimentSinkPublicationOperationResult
{
    private ExperimentSinkPublicationOperationResult(
        ExperimentPublicationOperationStatus status,
        ExperimentFailure? failure)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "The publication operation status is not defined.");
        }

        if (status == ExperimentPublicationOperationStatus.Failed)
        {
            ArgumentNullException.ThrowIfNull(failure);
        }
        else if (failure is not null)
        {
            throw new ArgumentException(
                $"Publication status '{status}' cannot include a failure.",
                nameof(failure));
        }

        Status = status;
        Failure = failure;
    }

    /// <summary>Gets the publication operation status.</summary>
    public ExperimentPublicationOperationStatus Status { get; }

    /// <summary>Gets the structured publication failure, when the operation failed.</summary>
    public ExperimentFailure? Failure { get; }

    /// <summary>Creates a successful sink publication operation result.</summary>
    /// <returns>A successful operation result.</returns>
    public static ExperimentSinkPublicationOperationResult Succeeded() =>
        new(
            ExperimentPublicationOperationStatus.Succeeded,
            failure: null);

    /// <summary>Creates a sink publication operation result that was not attempted.</summary>
    /// <returns>A not-attempted operation result.</returns>
    public static ExperimentSinkPublicationOperationResult NotAttempted() =>
        new(
            ExperimentPublicationOperationStatus.NotAttempted,
            failure: null);

    /// <summary>Creates a failed sink publication operation result.</summary>
    /// <param name="failure">The structured publication failure.</param>
    /// <returns>A failed operation result.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="failure"/> is <see langword="null"/>.
    /// </exception>
    public static ExperimentSinkPublicationOperationResult Failed(
        ExperimentFailure failure) =>
        new(
            ExperimentPublicationOperationStatus.Failed,
            failure);
}
