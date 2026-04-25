using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Aggregated result of a multi-agent pipeline run, providing per-stage diagnostics
/// alongside the response text from each agent.
/// </summary>
/// <remarks>
/// <para>
/// Returned by <c>RunWithDiagnosticsAsync</c> extension methods on
/// <see cref="Microsoft.Agents.AI.Workflows.Workflow"/>. Contains the full execution
/// timeline: each agent's response text, token usage, duration, and success/failure
/// state.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var result = await workflow.RunWithDiagnosticsAsync(
///     "Write an article about roses.",
///     diagnosticsAccessor,
///     cancellationToken: ct);
///
/// Console.WriteLine($"Pipeline {(result.Succeeded ? "succeeded" : "failed")} " +
///     $"in {result.TotalDuration.TotalSeconds:F1}s");
/// Console.WriteLine($"Total tokens: {result.AggregateTokenUsage?.TotalTokens ?? 0}");
/// Console.WriteLine($"Stages: {result.Stages.Count}");
/// </code>
/// </example>
public interface IPipelineRunResult
{
    /// <summary>Gets the per-stage results in execution order.</summary>
    IReadOnlyList<IAgentStageResult> Stages { get; }

    /// <summary>
    /// Gets the final responses as a dictionary (agent name →
    /// <see cref="ChatResponse"/>). When an agent ran multiple stages, the last
    /// stage's response wins. Values may be <see langword="null"/> for stages that
    /// produced no terminating text response.
    /// </summary>
    IReadOnlyDictionary<string, ChatResponse?> FinalResponses { get; }

    /// <summary>Gets the total wall-clock duration of the pipeline.</summary>
    TimeSpan TotalDuration { get; }

    /// <summary>
    /// Gets the aggregate token usage across all stages.
    /// <see langword="null"/> if diagnostics were not enabled.
    /// </summary>
    TokenUsage? AggregateTokenUsage { get; }

    /// <summary>Gets the total number of stages planned for this pipeline.</summary>
    int PlannedStageCount { get; }

    /// <summary>Gets whether all stages completed successfully.</summary>
    bool Succeeded { get; }

    /// <summary>Gets the error message if any stage failed.</summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// Gets the exception that caused the pipeline to fail, or <see langword="null"/>
    /// on success. Preserves the full exception including inner exceptions and stack
    /// trace for diagnostic purposes.
    /// </summary>
    Exception? Exception => null;
}
