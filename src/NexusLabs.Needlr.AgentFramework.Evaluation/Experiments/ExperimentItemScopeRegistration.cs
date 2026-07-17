namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Snapshots one registered item-scope provider and the identity used for canonical publication.
/// </summary>
[DoNotAutoRegister]
internal sealed record ExperimentItemScopeRegistration<TCase, TOutput>(
    string Name,
    bool IsRequired,
    ExperimentItemScopeFailureMode FailureMode,
    IExperimentItemScopeProvider<TCase, TOutput> Provider);
