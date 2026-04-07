namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Aggregated result of a multi-agent pipeline run, providing per-stage diagnostics
/// alongside the response text from each agent.
/// </summary>
public interface IPipelineRunResult
{
    /// <summary>Gets the per-stage results in execution order.</summary>
    IReadOnlyList<IAgentStageResult> Stages { get; }

    /// <summary>Gets the responses as a dictionary (agent name → response text).</summary>
    IReadOnlyDictionary<string, string> Responses { get; }

    /// <summary>Gets the total wall-clock duration of the pipeline.</summary>
    TimeSpan TotalDuration { get; }

    /// <summary>
    /// Gets the aggregate token usage across all stages.
    /// <see langword="null"/> if diagnostics were not enabled.
    /// </summary>
    TokenUsage? AggregateTokenUsage { get; }

    /// <summary>Gets whether all stages completed successfully.</summary>
    bool Succeeded { get; }

    /// <summary>Gets the error message if any stage failed.</summary>
    string? ErrorMessage { get; }
}
