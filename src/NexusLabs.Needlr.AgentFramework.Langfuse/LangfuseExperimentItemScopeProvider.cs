using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Creates one Langfuse scenario trace for each statistical trial and reactivates it around every
/// retry attempt and item evaluation.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
[DoNotAutoRegister]
public sealed class LangfuseExperimentItemScopeProvider<TCase, TOutput> :
    IExperimentItemScopeProvider<TCase, TOutput>
{
    /// <summary>Gets the provider name used in item publication results.</summary>
    public const string ProviderName = "langfuse";

    /// <summary>Gets the namespace used for Langfuse item correlations.</summary>
    public const string CorrelationNamespace = "langfuse";

    /// <summary>Gets the trace-id correlation name.</summary>
    public const string TraceIdCorrelationName = "trace.id";

    /// <summary>Gets the dataset-run-item-id correlation name.</summary>
    public const string DatasetRunItemIdCorrelationName = "dataset.run.item.id";

    /// <summary>Gets the dataset-run-id correlation name.</summary>
    public const string DatasetRunIdCorrelationName = "dataset.run.id";

    /// <summary>Gets the default low-cardinality scenario trace name.</summary>
    public const string DefaultScenarioName = "evaluate-experiment-item";

    private readonly ILangfuseExperimentTrialLifecycleFactory _lifecycleFactory;
    private readonly bool _linkHostedItem;
    private readonly Func<ExperimentItemScopeContext<TCase>, string?>? _scenarioNameFactory;
    private readonly string[] _tags;
    private readonly IReadOnlyDictionary<string, string> _metadata;

    internal LangfuseExperimentItemScopeProvider(
        ILangfuseExperimentTrialLifecycleFactory lifecycleFactory,
        bool linkHostedItem,
        LangfuseExperimentItemScopeOptions<TCase>? options)
    {
        ArgumentNullException.ThrowIfNull(lifecycleFactory);
        options ??= new LangfuseExperimentItemScopeOptions<TCase>();
        options.Validate();

        _lifecycleFactory = lifecycleFactory;
        _linkHostedItem = linkHostedItem;
        _scenarioNameFactory = options.ScenarioNameFactory;
        _tags = options.Tags?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToArray() ?? [];
        _metadata = options.Metadata?
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        IsRequired = options.IsRequired;
        FailureMode = options.FailureMode;
    }

    /// <inheritdoc />
    public string Name => ProviderName;

    /// <inheritdoc />
    public bool IsRequired { get; }

    /// <inheritdoc />
    public ExperimentItemScopeFailureMode FailureMode { get; }

    /// <inheritdoc />
    public async ValueTask<IExperimentItemScope<TCase, TOutput>> EnterAsync(
        ExperimentItemScopeContext<TCase> context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var scenarioName = _scenarioNameFactory?.Invoke(context);
        if (string.IsNullOrWhiteSpace(scenarioName))
        {
            scenarioName = DefaultScenarioName;
        }

        var lifecycle = await _lifecycleFactory
            .EnterAsync(
                new LangfuseExperimentTrialLifecycleRequest(
                    scenarioName,
                    _linkHostedItem ? context.Case.Id : null,
                    _tags,
                    _metadata,
                    FailureMode == ExperimentItemScopeFailureMode.ExecutionPrerequisite
                        ? LangfuseExperimentItemLinkFailureMode.Strict
                        : LangfuseExperimentItemLinkFailureMode.BestEffort),
                cancellationToken)
            .ConfigureAwait(false);
        return new LangfuseExperimentItemScope<TCase, TOutput>(
            lifecycle,
            IsRequired);
    }
}
