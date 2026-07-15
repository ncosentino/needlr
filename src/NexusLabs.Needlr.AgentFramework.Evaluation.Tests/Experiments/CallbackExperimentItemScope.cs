using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

/// <summary>
/// Provides callback-driven item-scope lifecycle behavior for runner boundary tests.
/// </summary>
internal sealed class CallbackExperimentItemScope<TCase, TOutput>(
    IReadOnlyDictionary<Type, object> features,
    Func<IDisposable?> activate,
    Func<
        ExperimentItemResult<TCase, TOutput>,
        CancellationToken,
        ValueTask<ExperimentItemPublicationResult>> completeAsync,
    Func<CancellationToken, ValueTask> abortAsync,
    Func<ValueTask> disposeAsync) :
    IExperimentItemScope<TCase, TOutput>
{
    public IReadOnlyDictionary<Type, object> Features => features;

    public IDisposable? Activate() => activate();

    public ValueTask<ExperimentItemPublicationResult> CompleteAsync(
        ExperimentItemResult<TCase, TOutput> result,
        CancellationToken cancellationToken) =>
        completeAsync(result, cancellationToken);

    public ValueTask AbortAsync(CancellationToken cancellationToken) =>
        abortAsync(cancellationToken);

    public ValueTask DisposeAsync() => disposeAsync();
}
