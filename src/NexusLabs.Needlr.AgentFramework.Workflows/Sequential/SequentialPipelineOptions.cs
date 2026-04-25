using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

/// <summary>
/// Configuration options for a <see cref="SequentialPipelineRunner"/> execution,
/// including an optional completion gate and overall token budget.
/// </summary>
/// <example>
/// <code>
/// var options = new SequentialPipelineOptions
/// {
///     CompletionGate = result => result.Succeeded ? null : "Pipeline did not succeed",
///     TotalTokenBudget = 50_000,
/// };
/// </code>
/// </example>
public sealed record SequentialPipelineOptions
{
    /// <summary>
    /// Optional completion gate evaluated after all stages succeed.
    /// Returns <see langword="null"/> on success, or an error message
    /// if the pipeline output is unacceptable.
    /// </summary>
    public Func<IPipelineRunResult, string?>? CompletionGate { get; init; }

    /// <summary>
    /// Optional overall pipeline token budget.
    /// </summary>
    public long? TotalTokenBudget { get; init; }
}
