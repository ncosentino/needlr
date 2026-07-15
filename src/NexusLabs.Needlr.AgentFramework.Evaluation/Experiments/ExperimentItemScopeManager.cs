namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Coordinates one statistical trial's provider-neutral item scopes.
/// </summary>
[DoNotAutoRegister]
internal sealed class ExperimentItemScopeManager<TCase, TOutput>
{
    private readonly ExperimentItemScopeContext<TCase> _context;
    private readonly ProviderState[] _states;
    private ExperimentItemFeatureCollection _features =
        new(new Dictionary<Type, object>());
    private ExperimentFailure? _prerequisiteFailure;
    private bool _entryStarted;
    private bool _entryCompleted;
    private bool _finished;

    public ExperimentItemScopeManager(
        IReadOnlyList<IExperimentItemScopeProvider<TCase, TOutput>> providers,
        ExperimentItemScopeContext<TCase> context)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _states = providers
            .Select(provider => new ProviderState(provider))
            .ToArray();
    }

    public ExperimentItemFeatureCollection Features => _features;

    public async ValueTask<ExperimentFailure?> EnterAsync(
        CancellationToken cancellationToken)
    {
        if (_entryCompleted)
        {
            return _prerequisiteFailure;
        }

        if (_entryStarted)
        {
            throw new InvalidOperationException(
                "Experiment item scopes cannot be entered concurrently.");
        }

        _entryStarted = true;
        var featureValues = new Dictionary<Type, object>();
        for (var index = 0; index < _states.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = _states[index];
            try
            {
                var scope = await state.Provider
                    .EnterAsync(_context, cancellationToken)
                    .ConfigureAwait(false);
                state.Scope = scope;
                cancellationToken.ThrowIfCancellationRequested();
                ArgumentNullException.ThrowIfNull(scope);
                AddFeatures(state, scope.Features, featureValues);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(ex.Message, ex, cancellationToken);
            }
            catch (Exception ex)
            {
                state.RecordFailure(ex, "entry");
                if (state.Provider.FailureMode
                    == ExperimentItemScopeFailureMode.ExecutionPrerequisite)
                {
                    _prerequisiteFailure = CreatePrerequisiteFailure(
                        state.Provider.Name,
                        "entry",
                        ex);
                    MarkRemainingNotAttempted(index + 1);
                    break;
                }
            }
        }

        _features = new ExperimentItemFeatureCollection(featureValues);
        _entryCompleted = true;
        return _prerequisiteFailure;
    }

    public IDisposable? Activate(
        bool enforceExecutionPrerequisites,
        out ExperimentFailure? prerequisiteFailure)
    {
        if (!_entryCompleted)
        {
            throw new InvalidOperationException(
                "Experiment item scopes must be entered before activation.");
        }

        prerequisiteFailure = null;
        var activations = new List<ScopeActivation>();
        foreach (var state in _states)
        {
            if (state.Scope is null || state.ActivationDisabled)
            {
                continue;
            }

            try
            {
                var activation = state.Scope.Activate();
                if (activation is not null)
                {
                    activations.Add(new ScopeActivation(state, activation));
                }
            }
            catch (Exception ex)
            {
                state.ActivationDisabled = true;
                state.RecordFailure(ex, "activation");
                if (enforceExecutionPrerequisites
                    && state.Provider.FailureMode
                    == ExperimentItemScopeFailureMode.ExecutionPrerequisite)
                {
                    prerequisiteFailure = CreatePrerequisiteFailure(
                        state.Provider.Name,
                        "activation",
                        ex);
                    break;
                }
            }
        }

        if (activations.Count == 0)
        {
            return null;
        }

        var group = new ActivationGroup(activations);
        if (prerequisiteFailure is not null)
        {
            group.Dispose();
            return null;
        }

        return group;
    }

    public async Task<ExperimentItemResult<TCase, TOutput>> CompleteAndDisposeAsync(
        ExperimentItemResult<TCase, TOutput> result,
        TimeSpan cleanupTimeout,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (_finished)
        {
            throw new InvalidOperationException(
                "Experiment item scopes have already finished.");
        }

        if (_states.Length == 0)
        {
            _finished = true;
            return result;
        }

        foreach (var state in _states)
        {
            if (state.Scope is null)
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                state.CompletionStarted = true;
                var publication = await state.Scope
                    .CompleteAsync(result, cancellationToken)
                    .ConfigureAwait(false);
                state.Publication = ValidateAndSnapshot(publication, state.Provider);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(ex.Message, ex, cancellationToken);
            }
            catch (Exception ex)
            {
                state.RecordFailure(ex, "completion");
            }
        }

        using var cleanupCancellation =
            new CancellationTokenSource(cleanupTimeout, timeProvider);
        var cleanupFailures = new List<Exception>();
        await DisposeScopesAsync(
            cleanupCancellation.Token,
            cleanupTimeout,
            recordPublicationFailures: true,
            cleanupFailures).ConfigureAwait(false);
        _finished = true;
        cancellationToken.ThrowIfCancellationRequested();

        var publications = _states
            .Select(CreateFinalPublication)
            .ToArray();
        var correlations = publications
            .SelectMany(publication => publication.Correlations)
            .ToArray();
        return WithPublications(
            result,
            Array.AsReadOnly(correlations),
            Array.AsReadOnly(publications));
    }

    public async Task<IReadOnlyList<Exception>> AbortAndDisposeAsync(
        CancellationToken cleanupToken,
        TimeSpan cleanupTimeout)
    {
        if (_finished)
        {
            return [];
        }

        var cleanupFailures = new List<Exception>();
        for (var index = _states.Length - 1; index >= 0; index--)
        {
            var state = _states[index];
            if (state.Scope is null
                || state.CompletionStarted
                || state.AbortNotified)
            {
                continue;
            }

            state.AbortNotified = true;
            Task? abortTask = null;
            try
            {
                abortTask = state.Scope.AbortAsync(cleanupToken).AsTask();
                await abortTask.WaitAsync(cleanupToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
                when (cleanupToken.IsCancellationRequested)
            {
                if (abortTask is { IsCompleted: false })
                {
                    ObserveLateCompletion(abortTask);
                }

                cleanupFailures.Add(CreateCleanupTimeout(
                    state.Provider.Name,
                    "abort",
                    cleanupTimeout,
                    ex));
            }
            catch (Exception ex)
            {
                cleanupFailures.Add(CreateCleanupFailure(
                    state.Provider.Name,
                    "abort",
                    ex));
            }
        }

        await DisposeScopesAsync(
            cleanupToken,
            cleanupTimeout,
            recordPublicationFailures: false,
            cleanupFailures).ConfigureAwait(false);
        _finished = true;
        return Array.AsReadOnly(cleanupFailures.ToArray());
    }

    private static ExperimentItemPublicationResult ValidateAndSnapshot(
        ExperimentItemPublicationResult publication,
        IExperimentItemScopeProvider<TCase, TOutput> provider)
    {
        ArgumentNullException.ThrowIfNull(publication);
        ArgumentException.ThrowIfNullOrWhiteSpace(publication.Name);
        if (!string.Equals(publication.Name, provider.Name, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Item scope '{provider.Name}' returned publication name '{publication.Name}'.");
        }

        if (publication.IsRequired != provider.IsRequired)
        {
            throw new InvalidOperationException(
                $"Item scope '{provider.Name}' returned an inconsistent required-publication flag.");
        }

        if (!Enum.IsDefined(publication.Status))
        {
            throw new InvalidOperationException(
                $"Item scope '{provider.Name}' returned an undefined publication status.");
        }

        ArgumentNullException.ThrowIfNull(publication.Correlations);
        var correlationKeys = new HashSet<(string Namespace, string Name)>();
        var correlations = new ExperimentItemCorrelation[publication.Correlations.Count];
        for (var index = 0; index < publication.Correlations.Count; index++)
        {
            var correlation = publication.Correlations[index];
            ArgumentNullException.ThrowIfNull(correlation);
            ArgumentException.ThrowIfNullOrWhiteSpace(correlation.Namespace);
            ArgumentException.ThrowIfNullOrWhiteSpace(correlation.Name);
            ArgumentException.ThrowIfNullOrWhiteSpace(correlation.Value);
            if (!correlationKeys.Add((correlation.Namespace, correlation.Name)))
            {
                throw new InvalidOperationException(
                    $"Item scope '{provider.Name}' returned duplicate correlation " +
                    $"'{correlation.Namespace}:{correlation.Name}'.");
            }

            correlations[index] = new ExperimentItemCorrelation
            {
                Namespace = correlation.Namespace,
                Name = correlation.Name,
                Value = correlation.Value,
            };
        }

        if (publication.Status == ExperimentItemPublicationStatus.Failed)
        {
            ArgumentNullException.ThrowIfNull(publication.Failure);
            if (publication.Failure.Stage != ExperimentFailureStage.Publication
                || publication.Failure.Code != ExperimentFailureCode.ItemScopeFailed
                || publication.Failure.IsRetryable)
            {
                throw new InvalidOperationException(
                    $"Item scope '{provider.Name}' returned an invalid publication failure.");
            }
        }
        else if (publication.Failure is not null)
        {
            throw new InvalidOperationException(
                $"Item scope '{provider.Name}' returned a failure for status " +
                $"'{publication.Status}'.");
        }

        return new ExperimentItemPublicationResult
        {
            Name = publication.Name,
            IsRequired = publication.IsRequired,
            Status = publication.Status,
            Correlations = Array.AsReadOnly(correlations),
            Failure = publication.Failure is null
                ? null
                : SnapshotFailure(publication.Failure),
        };
    }

    private static ExperimentFailure SnapshotFailure(ExperimentFailure failure) =>
        new()
        {
            Code = failure.Code,
            Stage = failure.Stage,
            ExceptionType = failure.ExceptionType,
            Message = failure.Message,
            IsRetryable = failure.IsRetryable,
        };

    private static ExperimentItemResult<TCase, TOutput> WithPublications(
        ExperimentItemResult<TCase, TOutput> result,
        IReadOnlyList<ExperimentItemCorrelation> correlations,
        IReadOnlyList<ExperimentItemPublicationResult> publications) =>
        new()
        {
            Sequence = result.Sequence,
            Case = result.Case,
            TrialIndex = result.TrialIndex,
            Status = result.Status,
            Attempts = result.Attempts,
            HasOutput = result.HasOutput,
            Output = result.Output,
            Evaluation = result.Evaluation,
            Metrics = result.Metrics,
            Correlations = correlations,
            Publications = publications,
            Failure = result.Failure,
        };

    private static ExperimentFailure CreatePrerequisiteFailure(
        string providerName,
        string operation,
        Exception exception) =>
        ExperimentFailureFactory.Create(
            ExperimentFailureCode.ItemScopePrerequisiteFailed,
            ExperimentFailureStage.Publication,
            exception,
            $"Item scope '{providerName}' failed during {operation}: {exception.Message}");

    private static Exception CreateCleanupFailure(
        string providerName,
        string operation,
        Exception exception) =>
        new InvalidOperationException(
            $"Item scope '{providerName}' failed during {operation}.",
            exception);

    private static Exception CreateCleanupTimeout(
        string providerName,
        string operation,
        TimeSpan cleanupTimeout,
        Exception exception) =>
        new TimeoutException(
            $"Item scope '{providerName}' did not finish {operation} within " +
            $"{cleanupTimeout}.",
            exception);

    private static void ObserveLateCompletion(Task task)
    {
        _ = task.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously
                | TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private void AddFeatures(
        ProviderState state,
        IReadOnlyDictionary<Type, object> features,
        IDictionary<Type, object> featureValues)
    {
        ArgumentNullException.ThrowIfNull(features);
        var additions = new List<KeyValuePair<Type, object>>();
        var newTypes = new HashSet<Type>();
        foreach (var feature in features)
        {
            ArgumentNullException.ThrowIfNull(feature.Key);
            ArgumentNullException.ThrowIfNull(feature.Value);
            if (!feature.Key.IsInstanceOfType(feature.Value))
            {
                throw new InvalidOperationException(
                    $"Item scope '{state.Provider.Name}' registered feature value " +
                    $"'{feature.Value.GetType().FullName}' for incompatible type " +
                    $"'{feature.Key.FullName}'.");
            }

            if (featureValues.ContainsKey(feature.Key) || !newTypes.Add(feature.Key))
            {
                throw new InvalidOperationException(
                    $"Item scope '{state.Provider.Name}' registered duplicate feature type " +
                    $"'{feature.Key.FullName}'.");
            }

            additions.Add(feature);
        }

        foreach (var feature in additions)
        {
            featureValues.Add(feature.Key, feature.Value);
        }
    }

    private void MarkRemainingNotAttempted(int startIndex)
    {
        for (var index = startIndex; index < _states.Length; index++)
        {
            var state = _states[index];
            state.Publication = new ExperimentItemPublicationResult
            {
                Name = state.Provider.Name,
                IsRequired = state.Provider.IsRequired,
                Status = ExperimentItemPublicationStatus.NotAttempted,
            };
        }
    }

    private ExperimentItemPublicationResult CreateFinalPublication(ProviderState state)
    {
        var correlations = state.Publication?.Correlations ?? [];
        if (state.Failure is not null)
        {
            return new ExperimentItemPublicationResult
            {
                Name = state.Provider.Name,
                IsRequired = state.Provider.IsRequired,
                Status = ExperimentItemPublicationStatus.Failed,
                Correlations = correlations,
                Failure = state.Failure,
            };
        }

        return state.Publication ?? new ExperimentItemPublicationResult
        {
            Name = state.Provider.Name,
            IsRequired = state.Provider.IsRequired,
            Status = ExperimentItemPublicationStatus.NotAttempted,
        };
    }

    private async Task DisposeScopesAsync(
        CancellationToken cleanupToken,
        TimeSpan cleanupTimeout,
        bool recordPublicationFailures,
        ICollection<Exception> cleanupFailures)
    {
        for (var index = _states.Length - 1; index >= 0; index--)
        {
            var state = _states[index];
            if (state.Scope is null || state.Disposed)
            {
                continue;
            }

            state.Disposed = true;
            Task? disposalTask = null;
            try
            {
                disposalTask = state.Scope.DisposeAsync().AsTask();
                await disposalTask.WaitAsync(cleanupToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (cleanupToken.IsCancellationRequested)
            {
                if (disposalTask is { IsCompleted: false })
                {
                    ObserveLateCompletion(disposalTask);
                }

                var failure = CreateCleanupTimeout(
                    state.Provider.Name,
                    "disposal",
                    cleanupTimeout,
                    ex);
                cleanupFailures.Add(failure);
                if (recordPublicationFailures)
                {
                    state.RecordFailure(failure, "disposal");
                }
            }
            catch (Exception ex)
            {
                var failure = CreateCleanupFailure(
                    state.Provider.Name,
                    "disposal",
                    ex);
                cleanupFailures.Add(failure);
                if (recordPublicationFailures)
                {
                    state.RecordFailure(ex, "disposal");
                }
            }
        }
    }

    private sealed class ProviderState
    {
        public ProviderState(IExperimentItemScopeProvider<TCase, TOutput> provider)
        {
            Provider = provider;
        }

        public IExperimentItemScopeProvider<TCase, TOutput> Provider { get; }

        public IExperimentItemScope<TCase, TOutput>? Scope { get; set; }

        public ExperimentItemPublicationResult? Publication { get; set; }

        public ExperimentFailure? Failure { get; private set; }

        public bool ActivationDisabled { get; set; }

        public bool CompletionStarted { get; set; }

        public bool AbortNotified { get; set; }

        public bool Disposed { get; set; }

        public void RecordFailure(Exception exception, string operation)
        {
            Failure ??= ExperimentFailureFactory.Create(
                ExperimentFailureCode.ItemScopeFailed,
                ExperimentFailureStage.Publication,
                exception,
                $"Item scope '{Provider.Name}' failed during {operation}: {exception.Message}");
        }
    }

    private sealed record ScopeActivation(
        ProviderState State,
        IDisposable Handle);

    private sealed class ActivationGroup(IReadOnlyList<ScopeActivation> activations) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            for (var index = activations.Count - 1; index >= 0; index--)
            {
                var activation = activations[index];
                try
                {
                    activation.Handle.Dispose();
                }
                catch (Exception ex)
                {
                    activation.State.RecordFailure(ex, "activation disposal");
                }
            }
        }
    }
}
