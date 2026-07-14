namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Default fixed-worker implementation of <see cref="IExperimentRunner"/>.
/// </summary>
public sealed class ExperimentRunner : IExperimentRunner
{
    private static readonly TimeSpan MaxAttemptTimeout =
        TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes an experiment runner.
    /// </summary>
    /// <param name="timeProvider">
    /// The time provider used for timestamps, durations, and attempt deadlines. Uses
    /// <see cref="TimeProvider.System"/> when omitted.
    /// </param>
    public ExperimentRunner(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<ExperimentRunResult<TCase, TOutput>> RunAsync<TCase, TOutput>(
        ExperimentDefinition<TCase, TOutput> definition,
        ExperimentRunOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Name);
        ArgumentNullException.ThrowIfNull(definition.CaseSource);
        ArgumentNullException.ThrowIfNull(definition.Task);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.RunId);
        if (options.MaxConcurrency < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.MaxConcurrency),
                options.MaxConcurrency,
                "Experiment maximum concurrency must be positive.");
        }

        if (options.AttemptTimeout is { } attemptTimeout
            && (attemptTimeout <= TimeSpan.Zero
                || attemptTimeout == Timeout.InfiniteTimeSpan
                || attemptTimeout > MaxAttemptTimeout))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.AttemptTimeout),
                attemptTimeout,
                $"Experiment attempt timeout must be positive and no greater than {MaxAttemptTimeout}.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        ExperimentCaseSourceResult<TCase> sourceResult;
        try
        {
            sourceResult = await definition.CaseSource
                .LoadAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(ex.Message, ex, cancellationToken);
        }

        var workItems = MaterializeAndValidate(definition, sourceResult);
        cancellationToken.ThrowIfCancellationRequested();

        var startedAt = _timeProvider.GetUtcNow();
        var runTimestamp = _timeProvider.GetTimestamp();
        var results = new ExperimentItemResult<TCase, TOutput>[workItems.Length];
        var workerCount = Math.Min(options.MaxConcurrency, workItems.Length);
        var nextIndex = -1;
        var workers = new Task[workerCount];
        for (var workerIndex = 0; workerIndex < workerCount; workerIndex++)
        {
            workers[workerIndex] = Task.Run(
                async () =>
                {
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var index = Interlocked.Increment(ref nextIndex);
                        if (index >= workItems.Length)
                        {
                            return;
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        results[index] = await ExecuteItemAsync(
                            definition,
                            options,
                            workItems[index],
                            cancellationToken).ConfigureAwait(false);
                    }
                },
                CancellationToken.None);
        }

        try
        {
            await Task.WhenAll(workers).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(ex.Message, ex, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new ExperimentRunResult<TCase, TOutput>
        {
            RunId = options.RunId,
            ExperimentName = definition.Name,
            Source = sourceResult.Source,
            StartedAt = startedAt,
            Duration = _timeProvider.GetElapsedTime(runTimestamp),
            MaxConcurrency = options.MaxConcurrency,
            WorkerCount = workerCount,
            Items = Array.AsReadOnly(results),
        };
    }

    private static WorkItem<TCase>[] MaterializeAndValidate<TCase, TOutput>(
        ExperimentDefinition<TCase, TOutput> definition,
        ExperimentCaseSourceResult<TCase> sourceResult)
    {
        ArgumentNullException.ThrowIfNull(sourceResult);
        ArgumentNullException.ThrowIfNull(sourceResult.Source);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceResult.Source.Name);
        ArgumentNullException.ThrowIfNull(sourceResult.Cases);

        var cases = sourceResult.Cases.ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        long totalItems = 0;
        foreach (var @case in cases)
        {
            ArgumentNullException.ThrowIfNull(@case);
            ArgumentException.ThrowIfNullOrWhiteSpace(@case.Id);
            if (!ids.Add(@case.Id))
            {
                throw new ArgumentException(
                    $"Experiment '{definition.Name}' contains duplicate case id '{@case.Id}'.",
                    nameof(sourceResult));
            }

            if (@case.TrialCount < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(@case.TrialCount),
                    @case.TrialCount,
                    $"Experiment case '{@case.Id}' must have a positive trial count.");
            }

            totalItems += @case.TrialCount;
            if (totalItems >= int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sourceResult),
                    totalItems,
                    "The expanded experiment item count must be less than Int32.MaxValue.");
            }
        }

        var workItems = new WorkItem<TCase>[(int)totalItems];
        var sequence = 0;
        foreach (var @case in cases)
        {
            var frozenCase = new ExperimentCase<TCase>
            {
                Id = @case.Id,
                Value = @case.Value,
                TrialCount = @case.TrialCount,
                Tags = Array.AsReadOnly(@case.Tags?.ToArray() ?? []),
            };
            for (var trialIndex = 1; trialIndex <= @case.TrialCount; trialIndex++)
            {
                workItems[sequence] = new WorkItem<TCase>(
                    sequence,
                    frozenCase,
                    trialIndex);
                sequence++;
            }
        }

        return workItems;
    }

    private async Task<ExperimentItemResult<TCase, TOutput>> ExecuteItemAsync<TCase, TOutput>(
        ExperimentDefinition<TCase, TOutput> definition,
        ExperimentRunOptions options,
        WorkItem<TCase> workItem,
        CancellationToken cancellationToken)
    {
        const int attemptNumber = 1;
        var startedAt = _timeProvider.GetUtcNow();
        var attemptTimestamp = _timeProvider.GetTimestamp();
        using var timeoutCancellation = options.AttemptTimeout is { } timeout
            ? new CancellationTokenSource(timeout, _timeProvider)
            : null;
        using var linkedCancellation = timeoutCancellation is null
            ? null
            : CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCancellation.Token);
        var attemptToken = linkedCancellation?.Token ?? cancellationToken;
        var context = new ExperimentTaskContext<TCase>(
            options.RunId,
            workItem.Sequence,
            workItem.Case,
            workItem.TrialIndex,
            attemptNumber);

        try
        {
            var output = await definition.Task(context, attemptToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (timeoutCancellation?.IsCancellationRequested == true)
            {
                return CreateFailedItem<TCase, TOutput>(
                    workItem,
                    startedAt,
                    attemptTimestamp,
                    ExperimentAttemptStatus.TimedOut,
                    ExperimentItemStatus.TimedOut,
                    CreateFailure(
                        ExperimentFailureCode.AttemptTimedOut,
                        ExperimentFailureStage.Execution,
                        new TimeoutException(
                            $"Experiment attempt exceeded timeout {options.AttemptTimeout}.")));
            }

            var attempt = new ExperimentAttemptResult
            {
                AttemptNumber = attemptNumber,
                Status = ExperimentAttemptStatus.Succeeded,
                StartedAt = startedAt,
                Duration = _timeProvider.GetElapsedTime(attemptTimestamp),
            };
            var attempts = Array.AsReadOnly([attempt]);
            if (definition.ItemEvaluator is null)
            {
                return CreateSuccessfulItem(
                    workItem,
                    output,
                    attempts,
                    evaluation: null,
                    metrics: []);
            }

            try
            {
                var evaluationContext =
                    new ExperimentItemEvaluationContext<TCase, TOutput>(
                        options.RunId,
                        workItem.Sequence,
                        workItem.Case,
                        workItem.TrialIndex,
                        output,
                        attempts);
                var evaluation = await definition.ItemEvaluator(
                    evaluationContext,
                    cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                ArgumentNullException.ThrowIfNull(evaluation);
                var metrics = ExperimentMetricSnapshotFactory.Create(evaluation);
                return CreateSuccessfulItem(
                    workItem,
                    output,
                    attempts,
                    evaluation,
                    metrics);
            }
            catch (OperationCanceledException ex)
                when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(
                    ex.Message,
                    ex,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                var failure = CreateFailure(
                    ExperimentFailureCode.EvaluationFailed,
                    ExperimentFailureStage.ItemEvaluation,
                    ex);
                return new ExperimentItemResult<TCase, TOutput>
                {
                    Sequence = workItem.Sequence,
                    Case = workItem.Case,
                    TrialIndex = workItem.TrialIndex,
                    Status = ExperimentItemStatus.EvaluationFailed,
                    Attempts = attempts,
                    HasOutput = true,
                    Output = output,
                    Metrics = [],
                    Failure = failure,
                };
            }
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(ex.Message, ex, cancellationToken);
        }
        catch (OperationCanceledException ex)
            when (timeoutCancellation?.IsCancellationRequested == true)
        {
            return CreateFailedItem<TCase, TOutput>(
                workItem,
                startedAt,
                attemptTimestamp,
                ExperimentAttemptStatus.TimedOut,
                ExperimentItemStatus.TimedOut,
                CreateFailure(
                    ExperimentFailureCode.AttemptTimedOut,
                    ExperimentFailureStage.Execution,
                    ex));
        }
        catch (OperationCanceledException ex)
        {
            return CreateFailedItem<TCase, TOutput>(
                workItem,
                startedAt,
                attemptTimestamp,
                ExperimentAttemptStatus.Canceled,
                ExperimentItemStatus.Canceled,
                CreateFailure(
                    ExperimentFailureCode.TaskCanceled,
                    ExperimentFailureStage.Execution,
                    ex));
        }
        catch (Exception ex)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(
                    "Experiment execution was canceled by the caller.",
                    ex,
                    cancellationToken);
            }

            if (timeoutCancellation?.IsCancellationRequested == true)
            {
                return CreateFailedItem<TCase, TOutput>(
                    workItem,
                    startedAt,
                    attemptTimestamp,
                    ExperimentAttemptStatus.TimedOut,
                    ExperimentItemStatus.TimedOut,
                    CreateFailure(
                        ExperimentFailureCode.AttemptTimedOut,
                        ExperimentFailureStage.Execution,
                        ex));
            }

            return CreateFailedItem<TCase, TOutput>(
                workItem,
                startedAt,
                attemptTimestamp,
                ExperimentAttemptStatus.Failed,
                ExperimentItemStatus.ExecutionFailed,
                CreateFailure(
                    ExperimentFailureCode.ExecutionFailed,
                    ExperimentFailureStage.Execution,
                    ex));
        }
    }

    private ExperimentItemResult<TCase, TOutput> CreateFailedItem<TCase, TOutput>(
        WorkItem<TCase> workItem,
        DateTimeOffset startedAt,
        long attemptTimestamp,
        ExperimentAttemptStatus attemptStatus,
        ExperimentItemStatus itemStatus,
        ExperimentFailure failure)
    {
        var attempt = new ExperimentAttemptResult
        {
            AttemptNumber = 1,
            Status = attemptStatus,
            StartedAt = startedAt,
            Duration = _timeProvider.GetElapsedTime(attemptTimestamp),
            Failure = failure,
        };
        return new ExperimentItemResult<TCase, TOutput>
        {
            Sequence = workItem.Sequence,
            Case = workItem.Case,
            TrialIndex = workItem.TrialIndex,
            Status = itemStatus,
            Attempts = Array.AsReadOnly([attempt]),
            HasOutput = false,
            Failure = failure,
        };
    }

    private static ExperimentItemResult<TCase, TOutput> CreateSuccessfulItem<TCase, TOutput>(
        WorkItem<TCase> workItem,
        TOutput output,
        IReadOnlyList<ExperimentAttemptResult> attempts,
        Microsoft.Extensions.AI.Evaluation.EvaluationResult? evaluation,
        IReadOnlyList<ExperimentMetricSnapshot> metrics) =>
        new()
        {
            Sequence = workItem.Sequence,
            Case = workItem.Case,
            TrialIndex = workItem.TrialIndex,
            Status = ExperimentItemStatus.Succeeded,
            Attempts = attempts,
            HasOutput = true,
            Output = output,
            Evaluation = evaluation,
            Metrics = metrics,
        };

    private static ExperimentFailure CreateFailure(
        ExperimentFailureCode code,
        ExperimentFailureStage stage,
        Exception exception) =>
        new()
        {
            Code = code,
            Stage = stage,
            ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
            Message = exception.Message,
            IsRetryable = false,
        };

    private sealed record WorkItem<TCase>(
        int Sequence,
        ExperimentCase<TCase> Case,
        int TrialIndex);
}
