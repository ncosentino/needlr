namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Validates and executes deterministic final result-sink fan-out.
/// </summary>
[DoNotAutoRegister]
internal sealed class ExperimentResultSinkPipeline<TCase, TOutput>
{
    private readonly SinkRegistration[] _registrations;

    public ExperimentResultSinkPipeline(
        IReadOnlyList<IExperimentResultSink<TCase, TOutput>> sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        var names = new HashSet<string>(StringComparer.Ordinal);
        _registrations = new SinkRegistration[sinks.Count];
        for (var index = 0; index < sinks.Count; index++)
        {
            var sink = sinks[index];
            ArgumentNullException.ThrowIfNull(sink);
            var name = sink.Name;
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            if (!names.Add(name))
            {
                throw new ArgumentException(
                    $"Experiment contains duplicate result-sink name '{name}'.",
                    nameof(sinks));
            }

            _registrations[index] = new SinkRegistration(
                name,
                sink.IsRequired,
                sink);
        }
    }

    public async Task<ExperimentRunOutcome<TCase, TOutput>> PublishAsync(
        ExperimentRunResult<TCase, TOutput> result,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        cancellationToken.ThrowIfCancellationRequested();
        var sinkResults = new ExperimentSinkResult[_registrations.Length];
        for (var index = 0; index < _registrations.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var registration = _registrations[index];
            try
            {
                var sinkResult = await registration.Sink
                    .PublishAsync(result, cancellationToken)
                    .ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                sinkResults[index] = ValidateAndSnapshot(sinkResult, registration);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(ex.Message, ex, cancellationToken);
            }
            catch (Exception ex)
            {
                cancellationToken.ThrowIfCancellationRequested();
                sinkResults[index] = CreateFailedResult(registration, ex);
            }
        }

        var frozenSinkResults = Array.AsReadOnly(sinkResults);
        var publicationStatus = ReducePublicationStatus(
            result.Items,
            frozenSinkResults,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return new ExperimentRunOutcome<TCase, TOutput>
        {
            Result = result,
            PublicationStatus = publicationStatus,
            SinkResults = frozenSinkResults,
        };
    }

    private static ExperimentSinkResult ValidateAndSnapshot(
        ExperimentSinkPublicationOperationResult result,
        SinkRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Status switch
        {
            ExperimentPublicationOperationStatus.Succeeded =>
                ExperimentSinkResult.Succeeded(
                    registration.Name,
                    registration.IsRequired),
            ExperimentPublicationOperationStatus.NotAttempted =>
                ExperimentSinkResult.NotAttempted(
                    registration.Name,
                    registration.IsRequired),
            ExperimentPublicationOperationStatus.Failed =>
                ExperimentSinkResult.Failed(
                    registration.Name,
                    registration.IsRequired,
                    result.Failure!),
            _ => throw new ArgumentOutOfRangeException(
                nameof(result),
                result.Status,
                "The sink publication operation status is not defined."),
        };
    }

    private static ExperimentSinkResult CreateFailedResult(
        SinkRegistration registration,
        Exception exception) =>
        ExperimentSinkResult.Failed(
            registration.Name,
            registration.IsRequired,
            ExperimentFailureFactory.Create(
                ExperimentFailureCode.ResultSinkFailed,
                ExperimentFailureStage.Publication,
                exception,
                $"Result sink '{registration.Name}' failed: {exception.Message}"));

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
                Observe(
                    publication.IsRequired,
                    publication.Status,
                    ref hasAttemptedPublication,
                    ref hasOptionalFailure,
                    cancellationToken,
                    out var requiredFailure);
                if (requiredFailure)
                {
                    return ExperimentPublicationStatus.Failed;
                }
            }
        }

        foreach (var sinkResult in sinkResults)
        {
            Observe(
                sinkResult.IsRequired,
                sinkResult.Status,
                ref hasAttemptedPublication,
                ref hasOptionalFailure,
                cancellationToken,
                out var requiredFailure);
            if (requiredFailure)
            {
                return ExperimentPublicationStatus.Failed;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!hasAttemptedPublication)
        {
            return ExperimentPublicationStatus.NotRequested;
        }

        return hasOptionalFailure
            ? ExperimentPublicationStatus.PartiallyFailed
            : ExperimentPublicationStatus.Succeeded;
    }

    private static void Observe(
        bool isRequired,
        ExperimentPublicationOperationStatus status,
        ref bool hasAttemptedPublication,
        ref bool hasOptionalFailure,
        CancellationToken cancellationToken,
        out bool requiredFailure)
    {
        cancellationToken.ThrowIfCancellationRequested();
        requiredFailure = false;
        if (status == ExperimentPublicationOperationStatus.NotAttempted)
        {
            return;
        }

        hasAttemptedPublication = true;
        if (status != ExperimentPublicationOperationStatus.Failed)
        {
            return;
        }

        requiredFailure = isRequired;
        hasOptionalFailure |= !isRequired;
    }

    private sealed record SinkRegistration(
        string Name,
        bool IsRequired,
        IExperimentResultSink<TCase, TOutput> Sink);

}
