namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Describes one final result sink's publication outcome independently from run quality.
/// </summary>
public sealed record ExperimentSinkResult
{
    private ExperimentSinkResult(
        string name,
        bool isRequired,
        ExperimentPublicationOperationStatus status,
        ExperimentFailure? failure)
    {
        Name = name;
        IsRequired = isRequired;
        Status = status;
        Failure = failure;
    }

    /// <summary>Gets the unique result-sink name.</summary>
    public string Name { get; }

    /// <summary>
    /// Gets a value indicating whether failure contributes to aggregate required-publication
    /// failure.
    /// </summary>
    public bool IsRequired { get; }

    /// <summary>Gets the publication operation status.</summary>
    public ExperimentPublicationOperationStatus Status { get; }

    /// <summary>Gets the structured publication failure, when present.</summary>
    public ExperimentFailure? Failure { get; }

    /// <summary>Creates a successful canonical result-sink publication result.</summary>
    /// <param name="name">The registered result-sink name.</param>
    /// <param name="isRequired">Whether publication failure is required.</param>
    /// <returns>A successful canonical sink result.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    public static ExperimentSinkResult Succeeded(string name, bool isRequired) =>
        Create(
            name,
            isRequired,
            ExperimentPublicationOperationStatus.Succeeded,
            failure: null);

    /// <summary>Creates a canonical result-sink publication result that was not attempted.</summary>
    /// <param name="name">The registered result-sink name.</param>
    /// <param name="isRequired">Whether publication failure is required.</param>
    /// <returns>A not-attempted canonical sink result.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    public static ExperimentSinkResult NotAttempted(string name, bool isRequired) =>
        Create(
            name,
            isRequired,
            ExperimentPublicationOperationStatus.NotAttempted,
            failure: null);

    /// <summary>Creates a failed canonical result-sink publication result.</summary>
    /// <param name="name">The registered result-sink name.</param>
    /// <param name="isRequired">Whether publication failure is required.</param>
    /// <param name="failure">The structured result-sink publication failure.</param>
    /// <returns>A failed canonical sink result.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="failure"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="failure"/> is not a non-retryable result-sink publication failure.
    /// </exception>
    public static ExperimentSinkResult Failed(
        string name,
        bool isRequired,
        ExperimentFailure failure) =>
        Create(
            name,
            isRequired,
            ExperimentPublicationOperationStatus.Failed,
            failure);

    private static ExperimentSinkResult Create(
        string name,
        bool isRequired,
        ExperimentPublicationOperationStatus status,
        ExperimentFailure? failure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var failureSnapshot = status == ExperimentPublicationOperationStatus.Failed
            ? ExperimentFailureFactory.ValidateAndSnapshotPublicationFailure(
                failure!,
                ExperimentFailureCode.ResultSinkFailed,
                $"Result sink '{name}'")
            : null;
        return new ExperimentSinkResult(
            name,
            isRequired,
            status,
            failureSnapshot);
    }
}
