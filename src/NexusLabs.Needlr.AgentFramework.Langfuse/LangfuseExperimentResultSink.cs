using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Projects completed canonical experiment measurements to Langfuse trace and dataset-run scores
/// without changing the canonical quality decision.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
[DoNotAutoRegister]
public sealed class LangfuseExperimentResultSink<TCase, TOutput> :
    IExperimentResultSink<TCase, TOutput>
{
    private readonly object _snapshotGate = new();
    private readonly LangfuseScoreRecorder? _recorder;
    private readonly bool _isEnabled;
    private readonly ILangfuseExperimentRun? _run;
    private readonly ILangfuseExperimentScorePublisher? _runScorePublisher;
    private readonly Func<
        ExperimentItemResult<TCase, TOutput>,
        EvaluationMetric,
        string?>? _itemScoreIdProvider;
    private readonly Func<
        ExperimentRunEvaluationResult,
        EvaluationMetric,
        string?>? _runEvaluationScoreIdProvider;
    private readonly LangfuseExperimentDecisionScoreOptions? _decisionScore;
    private LangfuseExperimentResultSinkSnapshot _snapshot;
    private int _publishing;

    internal LangfuseExperimentResultSink(
        LangfuseScoreRecorder? recorder,
        bool isEnabled,
        ILangfuseExperimentRun? run,
        LangfuseExperimentResultSinkOptions<TCase, TOutput>? options)
    {
        options ??= new LangfuseExperimentResultSinkOptions<TCase, TOutput>();
        options.Validate();
        if (isEnabled)
        {
            ArgumentNullException.ThrowIfNull(recorder);
        }

        _recorder = recorder;
        _isEnabled = isEnabled;
        _run = run;
        _runScorePublisher = run as ILangfuseExperimentScorePublisher;
        if (run is not null && _runScorePublisher is null)
        {
            throw new ArgumentException(
                "The supplied experiment run does not expose structured Langfuse score publication.",
                nameof(run));
        }
        _itemScoreIdProvider = options.ItemScoreIdProvider;
        _runEvaluationScoreIdProvider = options.RunEvaluationScoreIdProvider;
        _decisionScore = options.DecisionScore is null
            ? null
            : options.DecisionScore with { };
        Name = options.Name;
        IsRequired = options.IsRequired;
        _snapshot = CreateSnapshot(
            LangfuseExperimentApiPublicationStatus.NotAttempted,
            [],
            [],
            decisionScore: null);
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public bool IsRequired { get; }

    /// <summary>Gets the latest detailed provider-specific publication snapshot.</summary>
    /// <returns>The latest immutable score and hosted-run publication snapshot.</returns>
    public LangfuseExperimentResultSinkSnapshot GetPublicationSnapshot()
    {
        lock (_snapshotGate)
        {
            return _snapshot;
        }
    }

    /// <inheritdoc />
    public async ValueTask<ExperimentSinkPublicationOperationResult> PublishAsync(
        ExperimentRunResult<TCase, TOutput> result,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.CompareExchange(ref _publishing, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                $"Langfuse result sink '{Name}' cannot publish concurrently.");
        }

        var itemScores = new List<LangfuseExperimentItemScoreResult>();
        var runEvaluationScores =
            new List<LangfuseExperimentRunEvaluationScoreResult>();
        LangfuseExperimentRunScoreResult? decisionScore = null;
        LangfuseExperimentResultSinkSnapshot? completedSnapshot = null;
        var unobservedFailure = false;
        SetSnapshot(CreateSnapshot(
            LangfuseExperimentApiPublicationStatus.InProgress,
            itemScores,
            runEvaluationScores,
            decisionScore));
        try
        {
            await PublishItemScoresAsync(
                result.Items,
                itemScores,
                cancellationToken).ConfigureAwait(false);
            await PublishRunEvaluationScoresAsync(
                result.RunEvaluations,
                runEvaluationScores,
                cancellationToken).ConfigureAwait(false);
            decisionScore = await PublishDecisionScoreAsync(
                result.Decision,
                score => decisionScore = score,
                cancellationToken).ConfigureAwait(false);

            var status = GetScorePublicationStatus(
                itemScores,
                runEvaluationScores,
                decisionScore,
                unobservedFailure: false);
            completedSnapshot = CreateSnapshot(
                status,
                itemScores,
                runEvaluationScores,
                decisionScore);
            SetSnapshot(completedSnapshot);
            return CreateSinkResult(completedSnapshot);
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            AddCanceledPendingItemScores(
                result.Items,
                itemScores);
            AddCanceledPendingRunEvaluationScores(
                result.RunEvaluations,
                runEvaluationScores);
            if (_decisionScore is not null && decisionScore is null)
            {
                decisionScore = CreateCanceledDecisionScore();
            }

            throw;
        }
        catch
        {
            unobservedFailure = true;
            throw;
        }
        finally
        {
            if (completedSnapshot is null)
            {
                SetSnapshot(CreateSnapshot(
                    GetScorePublicationStatus(
                        itemScores,
                        runEvaluationScores,
                        decisionScore,
                        unobservedFailure),
                    itemScores,
                    runEvaluationScores,
                    decisionScore));
            }

            Volatile.Write(ref _publishing, 0);
        }
    }

    private async Task PublishItemScoresAsync(
        IReadOnlyList<ExperimentItemResult<TCase, TOutput>> items,
        ICollection<LangfuseExperimentItemScoreResult> results,
        CancellationToken cancellationToken)
    {
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.Evaluation is not { } evaluation)
            {
                continue;
            }

            var traceId = GetTraceId(item);
            var metrics = evaluation.Metrics.Values.ToArray();
            var itemResultStart = results.Count;
            try
            {
                if (!_isEnabled)
                {
                    foreach (var metric in metrics)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        results.Add(new LangfuseExperimentItemScoreResult
                        {
                            CaseId = item.Case.Id,
                            TrialIndex = item.TrialIndex,
                            TraceId = traceId,
                            ScoreId = null,
                            Name = metric.Name,
                            Status = LangfuseScoreRecorder.HasPublishableValue(metric)
                                ? LangfuseExperimentScoreStatus.Disabled
                                : LangfuseExperimentScoreStatus.Skipped,
                        });
                    }

                    continue;
                }

                if (traceId is null)
                {
                    foreach (var metric in metrics)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var scoreId = GetItemScoreId(item, metric);
                        if (!LangfuseScoreRecorder.HasPublishableValue(metric))
                        {
                            results.Add(new LangfuseExperimentItemScoreResult
                            {
                                CaseId = item.Case.Id,
                                TrialIndex = item.TrialIndex,
                                TraceId = null,
                                ScoreId = scoreId,
                                Name = metric.Name,
                                Status = LangfuseExperimentScoreStatus.Skipped,
                            });
                            continue;
                        }

                        await _recorder!
                            .RecordUnavailableResultAsync(
                                metric.Name,
                                $"Cannot publish item score '{metric.Name}' for case " +
                                $"'{item.Case.Id}' trial {item.TrialIndex}: no sampled " +
                                "Langfuse trace correlation is available.",
                                score => results.Add(ToItemScoreResult(
                                    item,
                                    traceId: null,
                                    score,
                                    scoreId,
                                    LangfusePublicationFailureCode.TraceUnavailable)),
                                cancellationToken)
                            .ConfigureAwait(false);
                    }

                    continue;
                }

                var scoreOptions = _itemScoreIdProvider is null
                    ? null
                    : new LangfuseEvaluationScoreOptions
                    {
                        ScoreIdProvider = metric =>
                            _itemScoreIdProvider(item, metric),
                    };
                await _recorder!
                    .RecordEvaluationResultsAsync(
                        LangfuseScoreTarget.Trace(traceId),
                        evaluation,
                        scoreOptions,
                        score => results.Add(ToItemScoreResult(
                            item,
                            traceId,
                            score)),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (
                cancellationToken.IsCancellationRequested)
            {
                AddCanceledItemScores(
                    item,
                    traceId,
                    metrics,
                    results.Count - itemResultStart,
                    results);
                throw;
            }
        }
    }

    private async Task PublishRunEvaluationScoresAsync(
        IReadOnlyList<ExperimentRunEvaluationResult> evaluations,
        ICollection<LangfuseExperimentRunEvaluationScoreResult> results,
        CancellationToken cancellationToken)
    {
        foreach (var evaluation in evaluations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (evaluation.Status != ExperimentRunEvaluationStatus.Succeeded
                || evaluation.Evaluation is not { } evaluationResult)
            {
                continue;
            }

            if (_run is null)
            {
                foreach (var metric in evaluationResult.Metrics.Values)
                {
                    results.Add(new LangfuseExperimentRunEvaluationScoreResult
                    {
                        EvaluatorName = evaluation.Name,
                        ScoreId = null,
                        Name = metric.Name,
                        Status = LangfuseScoreRecorder.HasPublishableValue(metric)
                            ? _isEnabled
                                ? LangfuseExperimentScoreStatus.NotAttempted
                                : LangfuseExperimentScoreStatus.Disabled
                            : LangfuseExperimentScoreStatus.Skipped,
                    });
                }

                continue;
            }

            var metrics = evaluationResult.Metrics.Values.ToArray();
            var evaluationResultStart = results.Count;
            try
            {
                var scoreOptions = _runEvaluationScoreIdProvider is null
                    ? null
                    : new LangfuseEvaluationScoreOptions
                    {
                        ScoreIdProvider = metric =>
                            _runEvaluationScoreIdProvider(evaluation, metric),
                    };
                await _runScorePublisher!
                    .RecordEvaluationAsync(
                        evaluationResult,
                        scoreOptions,
                        scoreResult => results.Add(
                            ToRunEvaluationScoreResult(
                                evaluation,
                                scoreResult)),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (
                cancellationToken.IsCancellationRequested)
            {
                AddCanceledRunEvaluationScores(
                    evaluation,
                    metrics,
                    results.Count - evaluationResultStart,
                    results);
                throw;
            }
        }
    }

    private void AddCanceledPendingItemScores(
        IReadOnlyList<ExperimentItemResult<TCase, TOutput>> items,
        ICollection<LangfuseExperimentItemScoreResult> results)
    {
        foreach (var item in items)
        {
            if (item.Evaluation is not { } evaluation)
            {
                continue;
            }

            var observedCount = results.Count(result =>
                string.Equals(
                    result.CaseId,
                    item.Case.Id,
                    StringComparison.Ordinal)
                && result.TrialIndex == item.TrialIndex);
            AddCanceledItemScores(
                item,
                GetTraceId(item),
                evaluation.Metrics.Values.ToArray(),
                observedCount,
                results);
        }
    }

    private void AddCanceledPendingRunEvaluationScores(
        IReadOnlyList<ExperimentRunEvaluationResult> evaluations,
        ICollection<LangfuseExperimentRunEvaluationScoreResult> results)
    {
        foreach (var evaluation in evaluations)
        {
            if (evaluation.Status != ExperimentRunEvaluationStatus.Succeeded
                || evaluation.Evaluation is not { } evaluationResult)
            {
                continue;
            }

            var observedCount = results.Count(result => string.Equals(
                result.EvaluatorName,
                evaluation.Name,
                StringComparison.Ordinal));
            AddCanceledRunEvaluationScores(
                evaluation,
                evaluationResult.Metrics.Values.ToArray(),
                observedCount,
                results);
        }
    }

    private LangfuseExperimentRunScoreResult CreateCanceledDecisionScore() =>
        new(
            scoreId: null,
            _decisionScore!.Name,
            LangfuseExperimentScoreStatus.NotAttempted,
            _run?.DatasetRunId,
            new LangfusePublicationFailure(
                LangfusePublicationFailureCode.PublicationCanceled,
                $"Decision score '{_decisionScore.Name}' was canceled."));

    private async Task<LangfuseExperimentRunScoreResult?> PublishDecisionScoreAsync(
        ExperimentRunDecision decision,
        Action<LangfuseExperimentRunScoreResult> observer,
        CancellationToken cancellationToken)
    {
        if (_decisionScore is null)
        {
            return null;
        }

        var scoreId = _decisionScore.ScoreIdProvider?.Invoke(decision);
        if (scoreId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scoreId);
        }

        if (_run is null)
        {
            var localResult = new LangfuseExperimentRunScoreResult(
                scoreId,
                _decisionScore.Name,
                _isEnabled
                    ? LangfuseExperimentScoreStatus.NotAttempted
                    : LangfuseExperimentScoreStatus.Disabled,
                datasetRunId: null,
                failure: null);
            observer(localResult);
            return localResult;
        }

        LangfuseExperimentRunScoreResult? observed = null;
        try
        {
            return await _runScorePublisher!
                .RecordCategoricalScoreAsync(
                    _decisionScore.Name,
                    decision.ToString(),
                    new LangfuseScoreOptions
                    {
                        Id = scoreId,
                        Comment = _decisionScore.Comment,
                    },
                    score =>
                    {
                        observed = score;
                        observer(score);
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            if (observed is null)
            {
                observed = new LangfuseExperimentRunScoreResult(
                    scoreId,
                    _decisionScore.Name,
                    LangfuseExperimentScoreStatus.NotAttempted,
                    _run.DatasetRunId,
                    new LangfusePublicationFailure(
                        LangfusePublicationFailureCode.PublicationCanceled,
                        $"Decision score '{_decisionScore.Name}' was canceled."));
                observer(observed);
            }

            throw;
        }
    }

    private string? GetItemScoreId(
        ExperimentItemResult<TCase, TOutput> item,
        EvaluationMetric metric)
    {
        var scoreId = _itemScoreIdProvider?.Invoke(item, metric);
        if (scoreId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scoreId);
        }

        return scoreId;
    }

    private string? GetRunEvaluationScoreId(
        ExperimentRunEvaluationResult evaluation,
        EvaluationMetric metric)
    {
        var scoreId = _runEvaluationScoreIdProvider?.Invoke(
            evaluation,
            metric);
        if (scoreId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scoreId);
        }

        return scoreId;
    }

    private static string? GetTraceId(
        ExperimentItemResult<TCase, TOutput> item)
    {
        var publication = item.Publications.SingleOrDefault(
            candidate => string.Equals(
                candidate.Name,
                LangfuseExperimentItemScopeProvider<TCase, TOutput>.ProviderName,
                StringComparison.Ordinal));
        return publication?.Correlations.SingleOrDefault(
            correlation =>
                string.Equals(
                    correlation.Namespace,
                    LangfuseExperimentItemScopeProvider<TCase, TOutput>.CorrelationNamespace,
                    StringComparison.Ordinal)
                && string.Equals(
                    correlation.Name,
                    LangfuseExperimentItemScopeProvider<TCase, TOutput>.TraceIdCorrelationName,
                    StringComparison.Ordinal))
            ?.Value;
    }

    private static LangfuseExperimentItemScoreResult ToItemScoreResult(
        ExperimentItemResult<TCase, TOutput> item,
        string? traceId,
        LangfuseScoreRecordResult score,
        string? requestedScoreId = null,
        LangfusePublicationFailureCode failureCode =
            LangfusePublicationFailureCode.ApiRejected) =>
        new()
        {
            CaseId = item.Case.Id,
            TrialIndex = item.TrialIndex,
            TraceId = traceId,
            ScoreId = score.ScoreId ?? requestedScoreId,
            Name = score.Name,
            Status = ToExperimentScoreStatus(score.Status),
            Failure = score.Failure is null
                ? null
                : new LangfusePublicationFailure(
                    failureCode,
                    score.Failure.Message),
        };

    private static LangfuseExperimentRunEvaluationScoreResult
        ToRunEvaluationScoreResult(
            ExperimentRunEvaluationResult evaluation,
            LangfuseExperimentRunScoreResult score) =>
        new()
        {
            EvaluatorName = evaluation.Name,
            ScoreId = score.ScoreId,
            Name = score.Name,
            Status = score.Status,
            DatasetRunId = score.DatasetRunId,
            Failure = score.Failure,
        };

    private void AddCanceledItemScores(
        ExperimentItemResult<TCase, TOutput> item,
        string? traceId,
        IReadOnlyList<EvaluationMetric> metrics,
        int observedCount,
        ICollection<LangfuseExperimentItemScoreResult> results)
    {
        foreach (var metric in metrics.Skip(observedCount))
        {
            results.Add(new LangfuseExperimentItemScoreResult
            {
                CaseId = item.Case.Id,
                TrialIndex = item.TrialIndex,
                TraceId = traceId,
                ScoreId = GetItemScoreId(item, metric),
                Name = metric.Name,
                Status = LangfuseScoreRecorder.HasPublishableValue(metric)
                    ? LangfuseExperimentScoreStatus.NotAttempted
                    : LangfuseExperimentScoreStatus.Skipped,
                Failure = LangfuseScoreRecorder.HasPublishableValue(metric)
                    ? new LangfusePublicationFailure(
                        LangfusePublicationFailureCode.PublicationCanceled,
                        $"Item score '{metric.Name}' was canceled.")
                    : null,
            });
        }
    }

    private void AddCanceledRunEvaluationScores(
        ExperimentRunEvaluationResult evaluation,
        IReadOnlyList<EvaluationMetric> metrics,
        int observedCount,
        ICollection<LangfuseExperimentRunEvaluationScoreResult> results)
    {
        foreach (var metric in metrics.Skip(observedCount))
        {
            results.Add(new LangfuseExperimentRunEvaluationScoreResult
            {
                EvaluatorName = evaluation.Name,
                ScoreId = GetRunEvaluationScoreId(evaluation, metric),
                Name = metric.Name,
                Status = LangfuseScoreRecorder.HasPublishableValue(metric)
                    ? LangfuseExperimentScoreStatus.NotAttempted
                    : LangfuseExperimentScoreStatus.Skipped,
                DatasetRunId = _run?.DatasetRunId,
                Failure = LangfuseScoreRecorder.HasPublishableValue(metric)
                    ? new LangfusePublicationFailure(
                        LangfusePublicationFailureCode.PublicationCanceled,
                        $"Run-evaluation score '{metric.Name}' was canceled.")
                    : null,
            });
        }
    }

    private static LangfuseExperimentScoreStatus ToExperimentScoreStatus(
        LangfuseScoreRecordStatus status) =>
        status switch
        {
            LangfuseScoreRecordStatus.Accepted =>
                LangfuseExperimentScoreStatus.Accepted,
            LangfuseScoreRecordStatus.Failed =>
                LangfuseExperimentScoreStatus.Failed,
            LangfuseScoreRecordStatus.Skipped =>
                LangfuseExperimentScoreStatus.Skipped,
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "The Langfuse score record status is not defined."),
        };

    private LangfuseExperimentResultSinkSnapshot CreateSnapshot(
        LangfuseExperimentApiPublicationStatus status,
        IEnumerable<LangfuseExperimentItemScoreResult> itemScores,
        IEnumerable<LangfuseExperimentRunEvaluationScoreResult> runScores,
        LangfuseExperimentRunScoreResult? decisionScore) =>
        new()
        {
            ScorePublicationStatus = status,
            ItemScores = Array.AsReadOnly(itemScores.ToArray()),
            RunEvaluationScores = Array.AsReadOnly(runScores.ToArray()),
            DecisionScore = decisionScore,
            ExperimentRunPublication = _run?.GetPublicationSnapshot(),
        };

    private LangfuseExperimentApiPublicationStatus GetScorePublicationStatus(
            IReadOnlyCollection<LangfuseExperimentItemScoreResult> itemScores,
            IReadOnlyCollection<LangfuseExperimentRunEvaluationScoreResult> runScores,
            LangfuseExperimentRunScoreResult? decisionScore,
            bool unobservedFailure)
    {
        var statuses = itemScores
            .Select(score => (score.Status, score.Failure))
            .Concat(runScores.Select(score => (score.Status, score.Failure)))
            .Concat(decisionScore is null
                ? []
                :
                [
                    (decisionScore.Status, decisionScore.Failure),
                ])
            .ToArray();
        if (statuses.Length == 0)
        {
            return unobservedFailure
                ? LangfuseExperimentApiPublicationStatus.Failed
                : LangfuseExperimentApiPublicationStatus.NotAttempted;
        }

        var hasFailure = statuses.Any(result =>
            result.Status == LangfuseExperimentScoreStatus.Failed
            || result.Failure is not null)
            || unobservedFailure;
        var hasCompleted = statuses.Any(result =>
            result.Status is
                LangfuseExperimentScoreStatus.Accepted
                or LangfuseExperimentScoreStatus.Skipped);
        if (hasFailure)
        {
            return hasCompleted
                ? LangfuseExperimentApiPublicationStatus.Partial
                : LangfuseExperimentApiPublicationStatus.Failed;
        }

        if (!_isEnabled)
        {
            return LangfuseExperimentApiPublicationStatus.Disabled;
        }

        if (hasCompleted)
        {
            return LangfuseExperimentApiPublicationStatus.Complete;
        }

        return statuses.All(result =>
            result.Status == LangfuseExperimentScoreStatus.Disabled)
            ? LangfuseExperimentApiPublicationStatus.Disabled
            : LangfuseExperimentApiPublicationStatus.NotAttempted;
    }

    private static ExperimentSinkPublicationOperationResult CreateSinkResult(
        LangfuseExperimentResultSinkSnapshot snapshot) =>
        snapshot.ScorePublicationStatus switch
        {
            LangfuseExperimentApiPublicationStatus.Complete =>
                ExperimentSinkPublicationOperationResult.Succeeded(),
            LangfuseExperimentApiPublicationStatus.NotAttempted
                or LangfuseExperimentApiPublicationStatus.Disabled =>
                ExperimentSinkPublicationOperationResult.NotAttempted(),
            _ => ExperimentSinkPublicationOperationResult.Failed(
                new ExperimentFailure
                {
                    Code = ExperimentFailureCode.ResultSinkFailed,
                    Stage = ExperimentFailureStage.Publication,
                    ExceptionType = typeof(LangfuseException).FullName!,
                    Message =
                        $"Langfuse score publication ended with status " +
                        $"'{snapshot.ScorePublicationStatus}'.",
                }),
        };

    private void SetSnapshot(
        LangfuseExperimentResultSinkSnapshot snapshot)
    {
        lock (_snapshotGate)
        {
            _snapshot = snapshot;
        }
    }
}
