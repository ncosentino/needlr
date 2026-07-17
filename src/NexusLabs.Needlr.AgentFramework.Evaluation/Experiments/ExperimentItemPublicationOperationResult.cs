namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Describes item-scope publication work before the runner applies registered provider identity.
/// </summary>
public sealed record ExperimentItemPublicationOperationResult
{
    private ExperimentItemPublicationOperationResult(
        ExperimentPublicationOperationStatus status,
        IReadOnlyList<ExperimentItemCorrelation> correlations,
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

        ArgumentNullException.ThrowIfNull(correlations);
        var correlationSnapshot = new ExperimentItemCorrelation[correlations.Count];
        for (var index = 0; index < correlations.Count; index++)
        {
            var correlation = correlations[index];
            ArgumentNullException.ThrowIfNull(correlation);
            correlationSnapshot[index] = correlation;
        }

        Status = status;
        Correlations = Array.AsReadOnly(correlationSnapshot);
        Failure = failure;
    }

    /// <summary>Gets the publication operation status.</summary>
    public ExperimentPublicationOperationStatus Status { get; }

    /// <summary>Gets the snapshotted provider correlations produced by the operation.</summary>
    public IReadOnlyList<ExperimentItemCorrelation> Correlations { get; }

    /// <summary>Gets the structured publication failure, when the operation failed.</summary>
    public ExperimentFailure? Failure { get; }

    /// <summary>Creates a successful item publication operation result.</summary>
    /// <param name="correlations">The provider correlations produced by the operation.</param>
    /// <returns>A successful operation result containing a snapshot of the correlations.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="correlations"/> or one of its elements is <see langword="null"/>.
    /// </exception>
    public static ExperimentItemPublicationOperationResult Succeeded(
        IReadOnlyList<ExperimentItemCorrelation> correlations) =>
        new(
            ExperimentPublicationOperationStatus.Succeeded,
            correlations,
            failure: null);

    /// <summary>Creates an item publication operation result that was not attempted.</summary>
    /// <param name="correlations">The provider correlations available for the operation.</param>
    /// <returns>A not-attempted operation result containing a snapshot of the correlations.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="correlations"/> or one of its elements is <see langword="null"/>.
    /// </exception>
    public static ExperimentItemPublicationOperationResult NotAttempted(
        IReadOnlyList<ExperimentItemCorrelation> correlations) =>
        new(
            ExperimentPublicationOperationStatus.NotAttempted,
            correlations,
            failure: null);

    /// <summary>Creates a failed item publication operation result.</summary>
    /// <param name="correlations">The provider correlations available for the operation.</param>
    /// <param name="failure">The structured publication failure.</param>
    /// <returns>A failed operation result containing a snapshot of the correlations.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="correlations"/>, one of its elements, or <paramref name="failure"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static ExperimentItemPublicationOperationResult Failed(
        IReadOnlyList<ExperimentItemCorrelation> correlations,
        ExperimentFailure failure) =>
        new(
            ExperimentPublicationOperationStatus.Failed,
            correlations,
            failure);
}
