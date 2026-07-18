namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Describes one item scope's publication outcome independently from item quality.
/// </summary>
public sealed record ExperimentItemPublicationResult
{
    private ExperimentItemPublicationResult(
        string name,
        bool isRequired,
        ExperimentPublicationOperationStatus status,
        IReadOnlyList<ExperimentItemCorrelation> correlations,
        ExperimentFailure? failure)
    {
        Name = name;
        IsRequired = isRequired;
        Status = status;
        Correlations = correlations;
        Failure = failure;
    }

    /// <summary>Gets the unique item-scope provider name.</summary>
    public string Name { get; }

    /// <summary>
    /// Gets a value indicating whether publication failure is required for aggregate publication
    /// health.
    /// </summary>
    public bool IsRequired { get; }

    /// <summary>Gets the publication status.</summary>
    public ExperimentPublicationOperationStatus Status { get; }

    /// <summary>Gets namespaced provider identifiers produced by this scope.</summary>
    public IReadOnlyList<ExperimentItemCorrelation> Correlations { get; }

    /// <summary>Gets the structured publication failure, when present.</summary>
    public ExperimentFailure? Failure { get; }

    /// <summary>Creates a successful canonical item-scope publication result.</summary>
    /// <param name="name">The registered item-scope provider name.</param>
    /// <param name="isRequired">Whether publication failure is required.</param>
    /// <param name="correlations">The provider correlations to snapshot.</param>
    /// <returns>A successful canonical publication result.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> or a correlation component is blank, or correlation identity is
    /// duplicated.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="correlations"/> or one of its elements is <see langword="null"/>.
    /// </exception>
    public static ExperimentItemPublicationResult Succeeded(
        string name,
        bool isRequired,
        IReadOnlyList<ExperimentItemCorrelation> correlations) =>
        Create(
            name,
            isRequired,
            ExperimentPublicationOperationStatus.Succeeded,
            correlations,
            failure: null);

    /// <summary>Creates a canonical item-scope publication result that was not attempted.</summary>
    /// <param name="name">The registered item-scope provider name.</param>
    /// <param name="isRequired">Whether publication failure is required.</param>
    /// <param name="correlations">The provider correlations to snapshot.</param>
    /// <returns>A not-attempted canonical publication result.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> or a correlation component is blank, or correlation identity is
    /// duplicated.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="correlations"/> or one of its elements is <see langword="null"/>.
    /// </exception>
    public static ExperimentItemPublicationResult NotAttempted(
        string name,
        bool isRequired,
        IReadOnlyList<ExperimentItemCorrelation> correlations) =>
        Create(
            name,
            isRequired,
            ExperimentPublicationOperationStatus.NotAttempted,
            correlations,
            failure: null);

    /// <summary>Creates a failed canonical item-scope publication result.</summary>
    /// <param name="name">The registered item-scope provider name.</param>
    /// <param name="isRequired">Whether publication failure is required.</param>
    /// <param name="correlations">The provider correlations to snapshot.</param>
    /// <param name="failure">The structured item-scope publication failure.</param>
    /// <returns>A failed canonical publication result.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> or a correlation component is blank, or correlation identity is
    /// duplicated.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="correlations"/>, one of its elements, or <paramref name="failure"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="failure"/> is not a non-retryable item-scope publication failure.
    /// </exception>
    public static ExperimentItemPublicationResult Failed(
        string name,
        bool isRequired,
        IReadOnlyList<ExperimentItemCorrelation> correlations,
        ExperimentFailure failure) =>
        Create(
            name,
            isRequired,
            ExperimentPublicationOperationStatus.Failed,
            correlations,
            failure);

    private static ExperimentItemPublicationResult Create(
        string name,
        bool isRequired,
        ExperimentPublicationOperationStatus status,
        IReadOnlyList<ExperimentItemCorrelation> correlations,
        ExperimentFailure? failure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var correlationSnapshot = SnapshotCorrelations(correlations);
        var failureSnapshot = status == ExperimentPublicationOperationStatus.Failed
            ? ExperimentFailureFactory.ValidateAndSnapshotPublicationFailure(
                failure!,
                ExperimentFailureCode.ItemScopeFailed,
                $"Item scope '{name}'")
            : null;
        return new ExperimentItemPublicationResult(
            name,
            isRequired,
            status,
            correlationSnapshot,
            failureSnapshot);
    }

    private static IReadOnlyList<ExperimentItemCorrelation> SnapshotCorrelations(
        IReadOnlyList<ExperimentItemCorrelation> correlations)
    {
        ArgumentNullException.ThrowIfNull(correlations);
        var keys = new HashSet<(string Namespace, string Name)>();
        var snapshot = new ExperimentItemCorrelation[correlations.Count];
        for (var index = 0; index < correlations.Count; index++)
        {
            var correlation = correlations[index];
            ArgumentNullException.ThrowIfNull(correlation);
            ArgumentException.ThrowIfNullOrWhiteSpace(correlation.Namespace);
            ArgumentException.ThrowIfNullOrWhiteSpace(correlation.Name);
            ArgumentException.ThrowIfNullOrWhiteSpace(correlation.Value);
            if (!keys.Add((correlation.Namespace, correlation.Name)))
            {
                throw new ArgumentException(
                    $"Correlation '{correlation.Namespace}:{correlation.Name}' appears more than once.",
                    nameof(correlations));
            }

            snapshot[index] = new ExperimentItemCorrelation
            {
                Namespace = correlation.Namespace,
                Name = correlation.Name,
                Value = correlation.Value,
            };
        }

        return Array.AsReadOnly(snapshot);
    }
}
