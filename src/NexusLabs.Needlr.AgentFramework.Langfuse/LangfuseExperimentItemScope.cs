using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Adapts one Langfuse trial lifecycle to the provider-neutral item-scope contract.
/// </summary>
[DoNotAutoRegister]
internal sealed class LangfuseExperimentItemScope<TCase, TOutput> :
    IExperimentItemScope<TCase, TOutput>
{
    private readonly LangfuseExperimentTrialLifecycle _lifecycle;
    private readonly bool _isRequired;

    public LangfuseExperimentItemScope(
        LangfuseExperimentTrialLifecycle lifecycle,
        bool isRequired)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        _lifecycle = lifecycle;
        _isRequired = isRequired;
        Features = new Dictionary<Type, object>
        {
            [typeof(ILangfuseScenario)] = lifecycle.Scenario,
        };
    }

    public IReadOnlyDictionary<Type, object> Features { get; }

    public IDisposable? Activate() => _lifecycle.Activate();

    public ValueTask<ExperimentItemPublicationResult> CompleteAsync(
        ExperimentItemResult<TCase, TOutput> result,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(CreatePublication());
    }

    public ValueTask AbortAsync(CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => _lifecycle.DisposeAsync();

    private ExperimentItemPublicationResult CreatePublication()
    {
        var correlations = CreateCorrelations();
        if (_lifecycle.Link is null)
        {
            return new ExperimentItemPublicationResult
            {
                Name = LangfuseExperimentItemScopeProvider<TCase, TOutput>.ProviderName,
                IsRequired = _isRequired,
                Status = _lifecycle.IsDisabled || _lifecycle.RecordedTraceId is null
                    ? ExperimentPublicationOperationStatus.NotAttempted
                    : ExperimentPublicationOperationStatus.Succeeded,
                Correlations = correlations,
            };
        }

        return _lifecycle.Link.Status switch
        {
            LangfuseExperimentItemLinkStatus.Linked => new ExperimentItemPublicationResult
            {
                Name = LangfuseExperimentItemScopeProvider<TCase, TOutput>.ProviderName,
                IsRequired = _isRequired,
                Status = ExperimentPublicationOperationStatus.Succeeded,
                Correlations = correlations,
            },
            LangfuseExperimentItemLinkStatus.Failed
                or LangfuseExperimentItemLinkStatus.Inconsistent =>
                new ExperimentItemPublicationResult
                {
                    Name = LangfuseExperimentItemScopeProvider<TCase, TOutput>.ProviderName,
                    IsRequired = _isRequired,
                    Status = ExperimentPublicationOperationStatus.Failed,
                    Correlations = correlations,
                    Failure = CreateFailure(_lifecycle.Link),
                },
            LangfuseExperimentItemLinkStatus.NotSampled
                or LangfuseExperimentItemLinkStatus.Disabled =>
                new ExperimentItemPublicationResult
                {
                    Name = LangfuseExperimentItemScopeProvider<TCase, TOutput>.ProviderName,
                    IsRequired = _isRequired,
                    Status = ExperimentPublicationOperationStatus.NotAttempted,
                    Correlations = correlations,
                },
            _ => throw new ArgumentOutOfRangeException(
                nameof(_lifecycle.Link.Status),
                _lifecycle.Link.Status,
                "The Langfuse experiment item link status is not defined."),
        };
    }

    private IReadOnlyList<ExperimentItemCorrelation> CreateCorrelations()
    {
        var correlations = new List<ExperimentItemCorrelation>();
        AddCorrelation(
            correlations,
            LangfuseExperimentItemScopeProvider<TCase, TOutput>.TraceIdCorrelationName,
            _lifecycle.RecordedTraceId);
        AddCorrelation(
            correlations,
            LangfuseExperimentItemScopeProvider<TCase, TOutput>.DatasetRunItemIdCorrelationName,
            _lifecycle.Link?.DatasetRunItemId);
        AddCorrelation(
            correlations,
            LangfuseExperimentItemScopeProvider<TCase, TOutput>.DatasetRunIdCorrelationName,
            _lifecycle.Link?.DatasetRunId);
        return Array.AsReadOnly(correlations.ToArray());
    }

    private static void AddCorrelation(
        ICollection<ExperimentItemCorrelation> correlations,
        string name,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        correlations.Add(new ExperimentItemCorrelation
        {
            Namespace = LangfuseExperimentItemScopeProvider<TCase, TOutput>.CorrelationNamespace,
            Name = name,
            Value = value,
        });
    }

    private static ExperimentFailure CreateFailure(
        LangfuseExperimentItemLinkResult link)
    {
        var message = link.Failure?.Message
            ?? $"Langfuse item link ended with status '{link.Status}'.";
        return new ExperimentFailure
        {
            Code = ExperimentFailureCode.ItemScopeFailed,
            Stage = ExperimentFailureStage.Publication,
            ExceptionType = typeof(LangfuseException).FullName!,
            Message = message,
        };
    }
}
