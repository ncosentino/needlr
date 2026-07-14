namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Executes one operational attempt for one experiment case trial.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
/// <param name="context">The stable run, case, trial, sequence, and attempt context.</param>
/// <param name="cancellationToken">The caller and optional attempt-timeout token.</param>
/// <returns>The attempt output.</returns>
public delegate ValueTask<TOutput> ExperimentTask<TCase, TOutput>(
    ExperimentTaskContext<TCase> context,
    CancellationToken cancellationToken);
