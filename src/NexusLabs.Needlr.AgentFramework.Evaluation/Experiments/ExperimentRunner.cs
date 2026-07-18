using System.Threading.Channels;

using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Default bounded-worker implementation of <see cref="IExperimentRunner"/>.
/// </summary>
public sealed class ExperimentRunner : IExperimentRunner
{
    private static readonly TimeSpan MaxAttemptTimeout =
        TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes an experiment runner that uses <see cref="TimeProvider.System"/>.
    /// </summary>
    public ExperimentRunner()
        : this(TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes an experiment runner with the specified time provider.
    /// </summary>
    /// <param name="timeProvider">
    /// The time provider used for timestamps, durations, deadlines, and retry readiness.
    /// A null value uses <see cref="TimeProvider.System"/>.
    /// </param>
    public ExperimentRunner(TimeProvider? timeProvider)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Materializes, validates, expands, and executes one experiment without caller cancellation.
    /// </summary>
    /// <typeparam name="TCase">The caller-owned case value type.</typeparam>
    /// <typeparam name="TOutput">The caller-owned output type.</typeparam>
    /// <param name="definition">The experiment definition.</param>
    /// <param name="options">The run options.</param>
    /// <returns>The canonical quality result plus independent publication outcomes.</returns>
    public Task<ExperimentRunOutcome<TCase, TOutput>> RunAsync<TCase, TOutput>(
        ExperimentDefinition<TCase, TOutput> definition,
        ExperimentRunOptions options) =>
        RunAsync(definition, options, CancellationToken.None);

    /// <summary>
    /// Materializes, validates, expands, and executes one experiment with caller cancellation.
    /// </summary>
    /// <typeparam name="TCase">The caller-owned case value type.</typeparam>
    /// <typeparam name="TOutput">The caller-owned output type.</typeparam>
    /// <param name="definition">The experiment definition.</param>
    /// <param name="options">The run options.</param>
    /// <param name="cancellationToken">A caller cancellation token.</param>
    /// <returns>The canonical quality result plus independent publication outcomes.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="definition"/> or <paramref name="options"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The experiment definition, run options, or materialized cases are invalid.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A run option, trial count, or policy value is outside its supported range.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    public async Task<ExperimentRunOutcome<TCase, TOutput>> RunAsync<TCase, TOutput>(
        ExperimentDefinition<TCase, TOutput> definition,
        ExperimentRunOptions options,
        CancellationToken cancellationToken)
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

        if (options.RetryPolicy is { MaxAttempts: < 1 } retryPolicy)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.RetryPolicy),
                retryPolicy.MaxAttempts,
                "An experiment retry policy must permit at least one attempt.");
        }

        if (options.ItemScopeCleanupTimeout <= TimeSpan.Zero
            || options.ItemScopeCleanupTimeout == Timeout.InfiniteTimeSpan
            || options.ItemScopeCleanupTimeout > MaxAttemptTimeout)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.ItemScopeCleanupTimeout),
                options.ItemScopeCleanupTimeout,
                $"Experiment item-scope cleanup timeout must be positive and no greater than {MaxAttemptTimeout}.");
        }

        var runEvaluators = ValidateRunEvaluators(definition.RunEvaluators);
        var policies = ValidatePolicies(definition.Policies);
        var itemScopes = ValidateItemScopes(definition.ItemScopes);
        var sinkPipeline = new ExperimentResultSinkPipeline<TCase, TOutput>(definition.Sinks);
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
        try
        {
            if (workItems.Length > 0)
            {
                await ExecuteWorkItemsAsync(
                    definition,
                    options,
                    workItems,
                    results,
                    itemScopes,
                    workerCount,
                    cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var items = Array.AsReadOnly(results);
            var runEvaluationContext = new ExperimentRunEvaluationContext<TCase, TOutput>(
                options.RunId,
                definition.Name,
                sourceResult.Source,
                items);
            var runEvaluationResults = await EvaluateRunAsync(
                runEvaluators,
                runEvaluationContext,
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var policyContext = new ExperimentPolicyContext<TCase, TOutput>(
                options.RunId,
                definition.Name,
                sourceResult.Source,
                items,
                runEvaluationResults);
            var policyResults = await EvaluatePoliciesAsync(
                policies,
                policyContext,
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var result = new ExperimentRunResult<TCase, TOutput>
            {
                RunId = options.RunId,
                ExperimentName = definition.Name,
                Source = sourceResult.Source,
                StartedAt = startedAt,
                Duration = _timeProvider.GetElapsedTime(runTimestamp),
                MaxConcurrency = options.MaxConcurrency,
                WorkerCount = workerCount,
                Items = items,
                RunEvaluations = runEvaluationResults,
                PolicyResults = policyResults,
                Decision = ReduceDecision(policyResults),
            };
            cancellationToken.ThrowIfCancellationRequested();
            return await sinkPipeline
                .PublishAsync(result, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(ex.Message, ex, cancellationToken);
        }
    }

    private async Task ExecuteWorkItemsAsync<TCase, TOutput>(
        ExperimentDefinition<TCase, TOutput> definition,
        ExperimentRunOptions options,
        WorkItem<TCase>[] workItems,
        ExperimentItemResult<TCase, TOutput>[] results,
        IReadOnlyList<ExperimentItemScopeRegistration<TCase, TOutput>> itemScopeProviders,
        int workerCount,
        CancellationToken cancellationToken)
    {
        var ready = Channel.CreateBounded<WorkItemState<TCase, TOutput>>(
            new BoundedChannelOptions(workItems.Length)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = workerCount == 1,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            });
        var delayed = Channel.CreateBounded<ScheduledRetry<TCase, TOutput>>(
            new BoundedChannelOptions(workItems.Length)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = workerCount == 1,
                AllowSynchronousContinuations = false,
            });
        WorkItemState<TCase, TOutput>?[] states = workItems
            .Select(workItem => new WorkItemState<TCase, TOutput>(
                workItem,
                new ExperimentItemScopeManager<TCase, TOutput>(
                    itemScopeProviders,
                    new ExperimentItemScopeContext<TCase>(
                        options.RunId,
                        workItem.Sequence,
                        workItem.Case,
                        workItem.TrialIndex))))
            .ToArray();
        foreach (var state in states)
        {
            if (!ready.Writer.TryWrite(state!))
            {
                throw new InvalidOperationException(
                    "The initial experiment ready queue could not accept materialized work.");
            }
        }

        var remainingItems = workItems.Length;
        long retrySequence = 0;
        async Task CompleteItemAsync(
            WorkItemState<TCase, TOutput> state,
            ExperimentItemResult<TCase, TOutput> result)
        {
            var finalizedResult = await state.ItemScopes
                .CompleteAndDisposeAsync(
                    result,
                    options.ItemScopeCleanupTimeout,
                    _timeProvider,
                    cancellationToken)
                .ConfigureAwait(false);
            results[state.WorkItem.Sequence] = finalizedResult;
            states[state.WorkItem.Sequence] = null;
            if (Interlocked.Decrement(ref remainingItems) == 0)
            {
                delayed.Writer.TryComplete();
                ready.Writer.TryComplete();
            }
        }

        var scheduler = RunRetrySchedulerAsync(
            delayed.Reader,
            ready.Writer,
            cancellationToken);
        var tasks = new Task[workerCount + 1];
        tasks[0] = scheduler;
        for (var workerIndex = 0; workerIndex < workerCount; workerIndex++)
        {
            tasks[workerIndex + 1] = Task.Run(
                async () =>
                {
                    await foreach (var state in ready.Reader.ReadAllAsync(cancellationToken)
                        .ConfigureAwait(false))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var attemptExecution = await ExecuteAttemptAsync(
                            definition,
                            options,
                            state,
                            cancellationToken).ConfigureAwait(false);
                        if (attemptExecution.PrerequisiteFailure is not null)
                        {
                            await CompleteItemAsync(
                               state,
                               CreateFailedItem(
                                   state,
                                   ExperimentItemStatus.PrerequisiteFailed,
                                   attemptExecution.PrerequisiteFailure))
                               .ConfigureAwait(false);
                            continue;
                        }

                        var attempt = attemptExecution.Attempt!;
                        state.Attempts.Add(attempt);
                        if (attemptExecution.Succeeded)
                        {
                            var successfulItem = await EvaluateSuccessfulItemAsync(
                                definition,
                                options.RunId,
                                state,
                                attemptExecution.Output!,
                                cancellationToken).ConfigureAwait(false);
                            await CompleteItemAsync(state, successfulItem)
                                .ConfigureAwait(false);
                            continue;
                        }

                        ExperimentRetryDecision? retryDecision = null;
                        if (options.RetryPolicy is { } retryPolicy
                            && attempt.AttemptNumber < retryPolicy.MaxAttempts)
                        {
                            try
                            {
                                retryDecision = retryPolicy.Decide(new ExperimentRetryContext(
                                    options.RunId,
                                    state.WorkItem.Sequence,
                                    state.WorkItem.Case.Id,
                                    state.WorkItem.TrialIndex,
                                    attempt));
                                ArgumentNullException.ThrowIfNull(retryDecision);
                                if (retryDecision.ShouldRetry)
                                {
                                    ExperimentRetryPolicy.ValidateDelay(
                                        retryDecision.Delay,
                                        nameof(ExperimentRetryDecision.Delay));
                                }
                            }
                            catch (Exception ex)
                            {
                                var policyFailure = ExperimentFailureFactory.Create(
                                    ExperimentFailureCode.RetryPolicyFailed,
                                    ExperimentFailureStage.Policy,
                                    ex);
                                await CompleteItemAsync(
                                    state,
                                    CreateFailedItem(
                                        state,
                                        ExperimentItemStatus.ExecutionFailed,
                                        policyFailure))
                                    .ConfigureAwait(false);
                                continue;
                            }
                        }

                        if (retryDecision?.ShouldRetry == true)
                        {
                            state.Attempts[^1] = WithRetry(
                                state.Attempts[^1],
                                retryDecision.Delay);
                            var scheduledRetry = new ScheduledRetry<TCase, TOutput>(
                                state,
                                _timeProvider.GetUtcNow() + retryDecision.Delay,
                                Interlocked.Increment(ref retrySequence));
                            if (!delayed.Writer.TryWrite(scheduledRetry))
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                throw new InvalidOperationException(
                                    "The experiment retry queue could not accept a scheduled retry.");
                            }

                            continue;
                        }

                        await CompleteItemAsync(
                            state,
                            CreateFailedItem(
                                state,
                                attemptExecution.ItemStatus,
                                attemptExecution.Failure!))
                            .ConfigureAwait(false);
                    }
                },
                CancellationToken.None);
        }

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            var cleanupFailures = await AbortItemScopesAsync(
                states,
                options.ItemScopeCleanupTimeout).ConfigureAwait(false);
            if (cleanupFailures.Count == 0)
            {
                throw new OperationCanceledException(ex.Message, ex, cancellationToken);
            }

            throw new OperationCanceledException(
                "Experiment execution was canceled and one or more item scopes failed to clean up.",
                new AggregateException([ex, .. cleanupFailures]),
                cancellationToken);
        }
        catch (Exception ex)
        {
            var cleanupFailures = await AbortItemScopesAsync(
                states,
                options.ItemScopeCleanupTimeout).ConfigureAwait(false);
            if (cleanupFailures.Count == 0)
            {
                throw;
            }

            throw new AggregateException([ex, .. cleanupFailures]);
        }
    }

    private async Task<IReadOnlyList<Exception>> AbortItemScopesAsync<TCase, TOutput>(
        IReadOnlyList<WorkItemState<TCase, TOutput>?> states,
        TimeSpan cleanupTimeout)
    {
        var failures = new List<Exception>();
        using var cleanupCancellation =
            new CancellationTokenSource(cleanupTimeout, _timeProvider);
        foreach (var state in states)
        {
            if (state is null)
            {
                continue;
            }

            failures.AddRange(await state.ItemScopes
                .AbortAndDisposeAsync(cleanupCancellation.Token, cleanupTimeout)
                .ConfigureAwait(false));
        }

        return Array.AsReadOnly(failures.ToArray());
    }

    private async Task RunRetrySchedulerAsync<TCase, TOutput>(
        ChannelReader<ScheduledRetry<TCase, TOutput>> delayedReader,
        ChannelWriter<WorkItemState<TCase, TOutput>> readyWriter,
        CancellationToken cancellationToken)
    {
        var queue = new PriorityQueue<
            ScheduledRetry<TCase, TOutput>,
            (long ReadyAtTicks, long Sequence)>();
        while (true)
        {
            while (delayedReader.TryRead(out var scheduledRetry))
            {
                queue.Enqueue(
                    scheduledRetry,
                    (scheduledRetry.ReadyAt.UtcDateTime.Ticks, scheduledRetry.Sequence));
            }

            var now = _timeProvider.GetUtcNow();
            while (queue.TryPeek(out var readyRetry, out _)
                && readyRetry.ReadyAt <= now)
            {
                queue.Dequeue();
                if (!readyWriter.TryWrite(readyRetry.State))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new InvalidOperationException(
                        "The experiment ready queue could not accept a due retry.");
                }
            }

            if (queue.Count == 0)
            {
                if (!await delayedReader
                    .WaitToReadAsync(cancellationToken)
                    .ConfigureAwait(false))
                {
                    return;
                }

                continue;
            }

            var nextRetry = queue.Peek();
            var delay = nextRetry.ReadyAt - _timeProvider.GetUtcNow();
            if (delay <= TimeSpan.Zero)
            {
                continue;
            }

            if (delayedReader.Completion.IsCompleted)
            {
                await Task.Delay(delay, _timeProvider, cancellationToken).ConfigureAwait(false);
                continue;
            }

            using var wakeCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var inputReady = delayedReader
                .WaitToReadAsync(wakeCancellation.Token)
                .AsTask();
            var timer = Task.Delay(delay, _timeProvider, wakeCancellation.Token);
            var completed = await Task.WhenAny(inputReady, timer).ConfigureAwait(false);
            if (completed == inputReady)
            {
                await inputReady.ConfigureAwait(false);
                wakeCancellation.Cancel();
                await IgnoreExpectedCancellationAsync(
                    timer,
                    wakeCancellation.Token).ConfigureAwait(false);
            }
            else
            {
                await timer.ConfigureAwait(false);
                wakeCancellation.Cancel();
                await IgnoreExpectedCancellationAsync(
                    inputReady,
                    wakeCancellation.Token).ConfigureAwait(false);
            }
        }
    }

    private async Task<AttemptExecution<TOutput>> ExecuteAttemptAsync<TCase, TOutput>(
        ExperimentDefinition<TCase, TOutput> definition,
        ExperimentRunOptions options,
        WorkItemState<TCase, TOutput> state,
        CancellationToken cancellationToken)
    {
        IAsyncDisposable? limiterLease = null;
        try
        {
            if (options.SharedLimiter is not null)
            {
                limiterLease = await options.SharedLimiter
                    .AcquireAsync(cancellationToken)
                    .ConfigureAwait(false);
                ArgumentNullException.ThrowIfNull(limiterLease);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var entryFailure = await state.ItemScopes
                .EnterAsync(cancellationToken)
                .ConfigureAwait(false);
            if (entryFailure is not null)
            {
                return CreatePrerequisiteFailure<TOutput>(entryFailure);
            }

            using var activation = state.ItemScopes.Activate(
                enforceExecutionPrerequisites: true,
                out var activationFailure);
            if (activationFailure is not null)
            {
                return CreatePrerequisiteFailure<TOutput>(activationFailure);
            }

            var attemptNumber = state.Attempts.Count + 1;
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
                state.WorkItem.Sequence,
                state.WorkItem.Case,
                state.WorkItem.TrialIndex,
                attemptNumber,
                state.ItemScopes.Features);
            try
            {
                var output = await definition.Task(context, attemptToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (timeoutCancellation?.IsCancellationRequested == true)
                {
                    return CreateFailedAttempt<TOutput>(
                        attemptNumber,
                        startedAt,
                        attemptTimestamp,
                        ExperimentAttemptStatus.TimedOut,
                        ExperimentItemStatus.TimedOut,
                        ExperimentFailureFactory.Create(
                            ExperimentFailureCode.AttemptTimedOut,
                            ExperimentFailureStage.Execution,
                            new TimeoutException(
                                $"Experiment attempt exceeded timeout {options.AttemptTimeout}.")));
                }

                return new AttemptExecution<TOutput>
                {
                    Succeeded = true,
                    Output = output,
                    Attempt = ExperimentAttemptResult.Succeeded(
                        attemptNumber,
                        startedAt,
                        _timeProvider.GetElapsedTime(attemptTimestamp)),
                    ItemStatus = ExperimentItemStatus.Succeeded,
                };
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(ex.Message, ex, cancellationToken);
            }
            catch (OperationCanceledException ex)
                when (timeoutCancellation?.IsCancellationRequested == true)
            {
                return CreateFailedAttempt<TOutput>(
                    attemptNumber,
                    startedAt,
                    attemptTimestamp,
                    ExperimentAttemptStatus.TimedOut,
                    ExperimentItemStatus.TimedOut,
                    ExperimentFailureFactory.Create(
                        ExperimentFailureCode.AttemptTimedOut,
                        ExperimentFailureStage.Execution,
                        ex));
            }
            catch (OperationCanceledException ex)
            {
                return CreateFailedAttempt<TOutput>(
                    attemptNumber,
                    startedAt,
                    attemptTimestamp,
                    ExperimentAttemptStatus.Canceled,
                    ExperimentItemStatus.Canceled,
                    ExperimentFailureFactory.Create(
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
                    return CreateFailedAttempt<TOutput>(
                        attemptNumber,
                        startedAt,
                        attemptTimestamp,
                        ExperimentAttemptStatus.TimedOut,
                        ExperimentItemStatus.TimedOut,
                        ExperimentFailureFactory.Create(
                            ExperimentFailureCode.AttemptTimedOut,
                            ExperimentFailureStage.Execution,
                            ex));
                }

                return CreateFailedAttempt<TOutput>(
                    attemptNumber,
                    startedAt,
                    attemptTimestamp,
                    ExperimentAttemptStatus.Failed,
                    ExperimentItemStatus.ExecutionFailed,
                    ExperimentFailureFactory.Create(
                        ExperimentFailureCode.ExecutionFailed,
                        ExperimentFailureStage.Execution,
                        ex));
            }
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(ex.Message, ex, cancellationToken);
        }
        finally
        {
            if (limiterLease is not null)
            {
                await limiterLease.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task<ExperimentItemResult<TCase, TOutput>> EvaluateSuccessfulItemAsync<TCase, TOutput>(
        ExperimentDefinition<TCase, TOutput> definition,
        string runId,
        WorkItemState<TCase, TOutput> state,
        TOutput output,
        CancellationToken cancellationToken)
    {
        var attempts = Array.AsReadOnly(state.Attempts.ToArray());
        if (definition.ItemEvaluator is null)
        {
            return CreateSuccessfulItem(
                state.WorkItem,
                output,
                attempts,
                evaluation: null,
                metrics: []);
        }

        try
        {
            using var activation = state.ItemScopes.Activate(
                enforceExecutionPrerequisites: false,
                out _);
            var evaluationContext = new ExperimentItemEvaluationContext<TCase, TOutput>(
                runId,
                state.WorkItem.Sequence,
                state.WorkItem.Case,
                state.WorkItem.TrialIndex,
                output,
                attempts,
                state.ItemScopes.Features);
            var evaluation = await definition.ItemEvaluator(
                evaluationContext,
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(evaluation);
            var metrics = ExperimentMetricSnapshotFactory.Create(evaluation);
            return CreateSuccessfulItem(
                state.WorkItem,
                output,
                attempts,
                evaluation,
                metrics);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(ex.Message, ex, cancellationToken);
        }
        catch (Exception ex)
        {
            var failure = ExperimentFailureFactory.Create(
                ExperimentFailureCode.EvaluationFailed,
                ExperimentFailureStage.ItemEvaluation,
                ex);
            return new ExperimentItemResult<TCase, TOutput>
            {
                Sequence = state.WorkItem.Sequence,
                Case = state.WorkItem.Case,
                TrialIndex = state.WorkItem.TrialIndex,
                Status = ExperimentItemStatus.EvaluationFailed,
                Attempts = attempts,
                HasOutput = true,
                Output = output,
                Metrics = [],
                Failure = failure,
            };
        }
    }

    private static async Task<IReadOnlyList<ExperimentRunEvaluationResult>> EvaluateRunAsync<TCase, TOutput>(
        IReadOnlyList<IExperimentRunEvaluator<TCase, TOutput>> evaluators,
        ExperimentRunEvaluationContext<TCase, TOutput> context,
        CancellationToken cancellationToken)
    {
        var results = new ExperimentRunEvaluationResult[evaluators.Count];
        for (var index = 0; index < evaluators.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var evaluator = evaluators[index];
            try
            {
                var evaluation = await evaluator
                    .EvaluateAsync(context, cancellationToken)
                    .ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                ArgumentNullException.ThrowIfNull(evaluation);
                results[index] = ExperimentRunEvaluationResult.Succeeded(
                    evaluator.Name,
                    evaluation);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(ex.Message, ex, cancellationToken);
            }
            catch (Exception ex)
            {
                results[index] = ExperimentRunEvaluationResult.Failed(
                    evaluator.Name,
                    ExperimentFailureFactory.Create(
                        ExperimentFailureCode.RunEvaluationFailed,
                        ExperimentFailureStage.RunEvaluation,
                        ex));
            }
        }

        return Array.AsReadOnly(results);
    }

    private static async Task<IReadOnlyList<ExperimentPolicyResult>> EvaluatePoliciesAsync<TCase, TOutput>(
        IReadOnlyList<IExperimentRunPolicy<TCase, TOutput>> policies,
        ExperimentPolicyContext<TCase, TOutput> context,
        CancellationToken cancellationToken)
    {
        var results = new ExperimentPolicyResult[policies.Count];
        for (var index = 0; index < policies.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var policy = policies[index];
            try
            {
                var evaluation = await policy
                    .EvaluateAsync(context, cancellationToken)
                    .ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                ArgumentNullException.ThrowIfNull(evaluation);
                results[index] = ExperimentPolicyResult.FromVerdict(
                    policy.Name,
                    policy.Kind,
                    policy.IsRequired,
                    evaluation);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(ex.Message, ex, cancellationToken);
            }
            catch (Exception ex)
            {
                results[index] = ExperimentPolicyResult.ExecutionFailed(
                    policy.Name,
                    policy.Kind,
                    policy.IsRequired,
                    ExperimentFailureFactory.Create(
                        ExperimentFailureCode.PolicyFailed,
                        ExperimentFailureStage.Policy,
                        ex));
            }
        }

        return Array.AsReadOnly(results);
    }

    private static ExperimentRunDecision ReduceDecision(
        IReadOnlyList<ExperimentPolicyResult> policies)
    {
        var required = policies.Where(policy => policy.IsRequired).ToArray();
        if (required.Length == 0)
        {
            return ExperimentRunDecision.NotEvaluated;
        }

        if (required.Any(policy => policy.Decision == EvaluationDecision.Failed))
        {
            return ExperimentRunDecision.Failed;
        }

        return required.Any(policy => policy.Decision == EvaluationDecision.Inconclusive)
            ? ExperimentRunDecision.Inconclusive
            : ExperimentRunDecision.Passed;
    }

    private static IExperimentRunEvaluator<TCase, TOutput>[] ValidateRunEvaluators<TCase, TOutput>(
        IReadOnlyList<IExperimentRunEvaluator<TCase, TOutput>> evaluators)
    {
        ArgumentNullException.ThrowIfNull(evaluators);
        var copy = evaluators.ToArray();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var evaluator in copy)
        {
            ArgumentNullException.ThrowIfNull(evaluator);
            ArgumentException.ThrowIfNullOrWhiteSpace(evaluator.Name);
            if (!names.Add(evaluator.Name))
            {
                throw new ArgumentException(
                    $"Experiment contains duplicate run evaluator name '{evaluator.Name}'.",
                    nameof(evaluators));
            }
        }

        return copy;
    }

    private static IExperimentRunPolicy<TCase, TOutput>[] ValidatePolicies<TCase, TOutput>(
        IReadOnlyList<IExperimentRunPolicy<TCase, TOutput>> policies)
    {
        ArgumentNullException.ThrowIfNull(policies);
        var copy = policies.ToArray();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var policy in copy)
        {
            ArgumentNullException.ThrowIfNull(policy);
            ArgumentException.ThrowIfNullOrWhiteSpace(policy.Name);
            if (!Enum.IsDefined(policy.Kind))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(policies),
                    policy.Kind,
                    $"Policy '{policy.Name}' has an undefined kind.");
            }

            if (!names.Add(policy.Name))
            {
                throw new ArgumentException(
                    $"Experiment contains duplicate policy name '{policy.Name}'.",
                    nameof(policies));
            }
        }

        return copy;
    }

    private static ExperimentItemScopeRegistration<TCase, TOutput>[] ValidateItemScopes<TCase, TOutput>(
        IReadOnlyList<IExperimentItemScopeProvider<TCase, TOutput>> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        var copy = providers.ToArray();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var registrations =
            new ExperimentItemScopeRegistration<TCase, TOutput>[copy.Length];
        for (var index = 0; index < copy.Length; index++)
        {
            var provider = copy[index];
            ArgumentNullException.ThrowIfNull(provider);
            var name = provider.Name;
            var isRequired = provider.IsRequired;
            var failureMode = provider.FailureMode;
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            if (!Enum.IsDefined(failureMode))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(providers),
                    failureMode,
                    $"Item-scope provider '{name}' has an undefined failure mode.");
            }

            if (!names.Add(name))
            {
                throw new ArgumentException(
                    $"Experiment contains duplicate item-scope provider name '{name}'.",
                    nameof(providers));
            }

            registrations[index] = new ExperimentItemScopeRegistration<TCase, TOutput>(
                name,
                isRequired,
                failureMode,
                provider);
        }

        return registrations;
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

    private AttemptExecution<TOutput> CreateFailedAttempt<TOutput>(
        int attemptNumber,
        DateTimeOffset startedAt,
        long attemptTimestamp,
        ExperimentAttemptStatus attemptStatus,
        ExperimentItemStatus itemStatus,
        ExperimentFailure failure) =>
        new()
        {
            Succeeded = false,
            Attempt = ExperimentAttemptResult.Unsuccessful(
                attemptNumber,
                attemptStatus,
                startedAt,
                _timeProvider.GetElapsedTime(attemptTimestamp),
                failure),
            ItemStatus = itemStatus,
            Failure = failure,
        };

    private static AttemptExecution<TOutput> CreatePrerequisiteFailure<TOutput>(
        ExperimentFailure failure) =>
        new()
        {
            Succeeded = false,
            ItemStatus = ExperimentItemStatus.PrerequisiteFailed,
            Failure = failure,
            PrerequisiteFailure = failure,
        };

    private ExperimentItemResult<TCase, TOutput> CreateFailedItem<TCase, TOutput>(
        WorkItemState<TCase, TOutput> state,
        ExperimentItemStatus itemStatus,
        ExperimentFailure failure) =>
        new()
        {
            Sequence = state.WorkItem.Sequence,
            Case = state.WorkItem.Case,
            TrialIndex = state.WorkItem.TrialIndex,
            Status = itemStatus,
            Attempts = Array.AsReadOnly(state.Attempts.ToArray()),
            HasOutput = false,
            Failure = failure,
        };

    private static ExperimentItemResult<TCase, TOutput> CreateSuccessfulItem<TCase, TOutput>(
        WorkItem<TCase> workItem,
        TOutput output,
        IReadOnlyList<ExperimentAttemptResult> attempts,
        EvaluationResult? evaluation,
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

    private static ExperimentAttemptResult WithRetry(
        ExperimentAttemptResult attempt,
        TimeSpan delay) =>
        ExperimentAttemptResult.RetryScheduled(
            attempt.AttemptNumber,
            attempt.Status,
            attempt.StartedAt,
            attempt.Duration,
            attempt.Failure!,
            delay);

    private static async Task IgnoreExpectedCancellationAsync(
        Task task,
        CancellationToken cancellationToken)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private sealed record WorkItem<TCase>(
        int Sequence,
        ExperimentCase<TCase> Case,
        int TrialIndex);

    private sealed class WorkItemState<TCase, TOutput>
    {
        public WorkItemState(
            WorkItem<TCase> workItem,
            ExperimentItemScopeManager<TCase, TOutput> itemScopes)
        {
            WorkItem = workItem;
            ItemScopes = itemScopes;
        }

        public WorkItem<TCase> WorkItem { get; }

        public List<ExperimentAttemptResult> Attempts { get; } = [];

        public ExperimentItemScopeManager<TCase, TOutput> ItemScopes { get; }
    }

    private sealed record ScheduledRetry<TCase, TOutput>(
        WorkItemState<TCase, TOutput> State,
        DateTimeOffset ReadyAt,
        long Sequence);

    private sealed record AttemptExecution<TOutput>
    {
        public required bool Succeeded { get; init; }

        public TOutput? Output { get; init; }

        public ExperimentAttemptResult? Attempt { get; init; }

        public required ExperimentItemStatus ItemStatus { get; init; }

        public ExperimentFailure? Failure { get; init; }

        public ExperimentFailure? PrerequisiteFailure { get; init; }
    }
}
