namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Provides one canonical quality result plus independent provider publication outcomes.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
public sealed record ExperimentRunOutcome<TCase, TOutput>
{
    /// <summary>Gets the current canonical outcome schema version.</summary>
    public const int CurrentSchemaVersion = 4;

    /// <summary>Initializes a validated canonical experiment outcome.</summary>
    /// <param name="result">The canonical quality result.</param>
    /// <param name="sinkResults">The final sink results in registration order.</param>
    /// <exception cref="ArgumentException">A sink name is duplicated.</exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="result"/>, <paramref name="sinkResults"/>, or one of its elements is
    /// <see langword="null"/>.
    /// </exception>
    public ExperimentRunOutcome(
        ExperimentRunResult<TCase, TOutput> result,
        IReadOnlyList<ExperimentSinkResult> sinkResults)
        : this(result, sinkResults, CancellationToken.None)
    {
    }

    internal ExperimentRunOutcome(
        ExperimentRunResult<TCase, TOutput> result,
        IReadOnlyList<ExperimentSinkResult> sinkResults,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(sinkResults);
        var names = new HashSet<string>(StringComparer.Ordinal);
        var snapshot = new ExperimentSinkResult[sinkResults.Count];
        for (var index = 0; index < sinkResults.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sink = sinkResults[index];
            ArgumentNullException.ThrowIfNull(sink);
            if (!names.Add(sink.Name))
            {
                throw new ArgumentException(
                    $"Sink name '{sink.Name}' appears more than once.",
                    nameof(sinkResults));
            }

            snapshot[index] = sink;
        }

        SchemaVersion = CurrentSchemaVersion;
        Result = result;
        SinkResults = Array.AsReadOnly(snapshot);
        PublicationStatus = ReducePublicationStatus(
            result.Items,
            SinkResults,
            cancellationToken);
    }

    /// <summary>Gets the canonical outcome schema version.</summary>
    public int SchemaVersion { get; }

    /// <summary>Gets the canonical quality result.</summary>
    public ExperimentRunResult<TCase, TOutput> Result { get; }

    /// <summary>Gets aggregate publication health across item scopes and final sinks.</summary>
    public ExperimentPublicationStatus PublicationStatus { get; }

    /// <summary>Gets final sink results in registration order.</summary>
    public IReadOnlyList<ExperimentSinkResult> SinkResults { get; }

    private static ExperimentPublicationStatus ReducePublicationStatus(
        IReadOnlyList<ExperimentItemResult<TCase, TOutput>> items,
        IReadOnlyList<ExperimentSinkResult> sinkResults,
        CancellationToken cancellationToken)
    {
        var hasAttemptedPublication = false;
        var hasOptionalFailure = false;
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var publication in item.Publications)
            {
                if (Observe(
                        publication.IsRequired,
                        publication.Status,
                        ref hasAttemptedPublication,
                        ref hasOptionalFailure,
                        cancellationToken))
                {
                    return ExperimentPublicationStatus.Failed;
                }
            }
        }

        foreach (var sink in sinkResults)
        {
            if (Observe(
                    sink.IsRequired,
                    sink.Status,
                    ref hasAttemptedPublication,
                    ref hasOptionalFailure,
                    cancellationToken))
            {
                return ExperimentPublicationStatus.Failed;
            }
        }

        if (!hasAttemptedPublication)
        {
            return ExperimentPublicationStatus.NotRequested;
        }

        return hasOptionalFailure
            ? ExperimentPublicationStatus.PartiallyFailed
            : ExperimentPublicationStatus.Succeeded;
    }

    private static bool Observe(
        bool isRequired,
        ExperimentPublicationOperationStatus status,
        ref bool hasAttemptedPublication,
        ref bool hasOptionalFailure,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (status == ExperimentPublicationOperationStatus.NotAttempted)
        {
            return false;
        }

        hasAttemptedPublication = true;
        if (status != ExperimentPublicationOperationStatus.Failed)
        {
            return false;
        }

        hasOptionalFailure |= !isRequired;
        return isRequired;
    }
}
