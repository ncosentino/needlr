using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Reporting;

/// <summary>
/// Commits one completed MEAI Reporting scenario through the provider-neutral item lifecycle.
/// </summary>
[DoNotAutoRegister]
internal sealed class MeaiReportingExperimentItemScope<TCase, TOutput> :
    IExperimentItemScope<TCase, TOutput>
{
    private readonly MeaiReportingExperimentItem _item;
    private readonly IReadOnlyList<ExperimentItemCorrelation> _correlations;
    private CancellationToken _completionCancellationToken;
    private bool _persistOnDispose;
    private bool _disposed;

    public MeaiReportingExperimentItemScope(MeaiReportingExperimentItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _item = item;
        Features = new Dictionary<Type, object>
        {
            [typeof(MeaiReportingExperimentItem)] = item,
        };
        _correlations = Array.AsReadOnly(
        [
            new ExperimentItemCorrelation
            {
                Namespace =
                    MeaiReportingExperimentSchema.CorrelationNamespace,
                Name =
                    MeaiReportingExperimentSchema.ExecutionNameCorrelationName,
                Value = item.ExecutionName,
            },
            new ExperimentItemCorrelation
            {
                Namespace =
                    MeaiReportingExperimentSchema.CorrelationNamespace,
                Name =
                    MeaiReportingExperimentSchema.ScenarioNameCorrelationName,
                Value = item.ScenarioName,
            },
            new ExperimentItemCorrelation
            {
                Namespace =
                    MeaiReportingExperimentSchema.CorrelationNamespace,
                Name =
                    MeaiReportingExperimentSchema.IterationNameCorrelationName,
                Value = item.IterationName,
            },
        ]);
    }

    public IReadOnlyDictionary<Type, object> Features { get; }

    public IDisposable? Activate() => null;

    public ValueTask<ExperimentItemPublicationOperationResult> CompleteAsync(
        ExperimentItemResult<TCase, TOutput> result,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        cancellationToken.ThrowIfCancellationRequested();
        _completionCancellationToken = cancellationToken;

        if (_item.EvaluationCompleted)
        {
            _persistOnDispose = true;
            return ValueTask.FromResult(
                ExperimentItemPublicationOperationResult.Succeeded(_correlations));
        }

        if (result.Status == ExperimentItemStatus.Succeeded)
        {
            var exception = new InvalidOperationException(
                "The MEAI Reporting item scope completed a successful experiment item without " +
                "the paired MEAI Reporting item evaluator.");
            return ValueTask.FromResult(
                ExperimentItemPublicationOperationResult.Failed(
                    _correlations,
                    new ExperimentFailure
                    {
                        Code = ExperimentFailureCode.ItemScopeFailed,
                        Stage = ExperimentFailureStage.Publication,
                        ExceptionType = exception.GetType().FullName!,
                        Message = exception.Message,
                    }));
        }

        return ValueTask.FromResult(
            ExperimentItemPublicationOperationResult.NotAttempted(_correlations));
    }

    public ValueTask AbortAsync(CancellationToken cancellationToken)
    {
        _persistOnDispose = false;
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (!_persistOnDispose)
        {
            return;
        }

        _completionCancellationToken.ThrowIfCancellationRequested();
        await _item.PersistAsync().ConfigureAwait(false);
    }
}
