using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

/// <summary>
/// Creates callback-driven item scopes for runner boundary tests.
/// </summary>
internal sealed class CallbackExperimentItemScopeProvider<TCase, TOutput>(
    string name,
    bool isRequired,
    ExperimentItemScopeFailureMode failureMode,
    Func<
        ExperimentItemScopeContext<TCase>,
        CancellationToken,
        ValueTask<IExperimentItemScope<TCase, TOutput>>> enterAsync) :
    IExperimentItemScopeProvider<TCase, TOutput>
{
    public string Name => name;

    public bool IsRequired => isRequired;

    public ExperimentItemScopeFailureMode FailureMode => failureMode;

    public ValueTask<IExperimentItemScope<TCase, TOutput>> EnterAsync(
        ExperimentItemScopeContext<TCase> context,
        CancellationToken cancellationToken) =>
        enterAsync(context, cancellationToken);
}
