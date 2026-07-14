namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Computes an explicit retry delay from a completed execution attempt.
/// </summary>
/// <param name="context">The completed attempt and stable item identity.</param>
/// <returns>The delay before the next attempt becomes ready.</returns>
public delegate TimeSpan ExperimentRetryDelayProvider(ExperimentRetryContext context);
