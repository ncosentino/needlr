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
        IReadOnlyList<ExperimentItemScopeRegistration<TCase, TOutput>> providers,
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
                if (state.Registration.FailureMode
                    == ExperimentItemScopeFailureMode.ExecutionPrerequisite)
                {
                    _prerequisiteFailure = CreatePrerequisiteFailure(
                        state.Registration.Name,
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
                    && state.Registration.FailureMode
                    == ExperimentItemScopeFailureMode.ExecutionPrerequisite)
                {
                    prerequisiteFailure = CreatePrerequisiteFailure(
                        state.Registration.Name,
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
                state.Publication = ValidateAndSnapshot(publication, state.Registration);
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
        return result.WithPublications(Array.AsReadOnly(publications));
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
                    state.Registration.Name,
                    "abort",
                    cleanupTimeout,
                    ex));
            }
            catch (Exception ex)
            {
                cleanupFailures.Add(CreateCleanupFailure(
                    state.Registration.Name,
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
        ExperimentItemPublicationOperationResult publication,
        ExperimentItemScopeRegistration<TCase, TOutput> registration)
    {
        ArgumentNullException.ThrowIfNull(publication);
        return publication.Status switch
        {
            ExperimentPublicationOperationStatus.Succeeded =>
                ExperimentItemPublicationResult.Succeeded(
                    registration.Name,
                    registration.IsRequired,
                    publication.Correlations),
            ExperimentPublicationOperationStatus.NotAttempted =>
                ExperimentItemPublicationResult.NotAttempted(
                    registration.Name,
                    registration.IsRequired,
                    publication.Correlations),
            ExperimentPublicationOperationStatus.Failed =>
                ExperimentItemPublicationResult.Failed(
                    registration.Name,
                    registration.IsRequired,
                    publication.Correlations,
                    publication.Failure!),
            _ => throw new ArgumentOutOfRangeException(
                nameof(publication),
                publication.Status,
                "The item publication operation status is not defined."),
        };
    }

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
                    $"Item scope '{state.Registration.Name}' registered feature value " +
                    $"'{feature.Value.GetType().FullName}' for incompatible type " +
                    $"'{feature.Key.FullName}'.");
            }

            if (featureValues.ContainsKey(feature.Key) || !newTypes.Add(feature.Key))
            {
                throw new InvalidOperationException(
                    $"Item scope '{state.Registration.Name}' registered duplicate feature type " +
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
            state.Publication = ExperimentItemPublicationResult.NotAttempted(
                state.Registration.Name,
                state.Registration.IsRequired,
                correlations: []);
        }
    }

    private ExperimentItemPublicationResult CreateFinalPublication(ProviderState state)
    {
        var correlations = state.Publication?.Correlations ?? [];
        if (state.Failure is not null)
        {
            return ExperimentItemPublicationResult.Failed(
                state.Registration.Name,
                state.Registration.IsRequired,
                correlations,
                state.Failure);
        }

        return state.Publication ?? ExperimentItemPublicationResult.NotAttempted(
            state.Registration.Name,
            state.Registration.IsRequired,
            correlations: []);
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
                    state.Registration.Name,
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
                    state.Registration.Name,
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
        public ProviderState(ExperimentItemScopeRegistration<TCase, TOutput> registration)
        {
            Registration = registration;
        }

        public ExperimentItemScopeRegistration<TCase, TOutput> Registration { get; }

        public IExperimentItemScopeProvider<TCase, TOutput> Provider =>
            Registration.Provider;

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
                $"Item scope '{Registration.Name}' failed during {operation}: {exception.Message}");
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
