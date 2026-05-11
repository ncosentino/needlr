using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Result of a single agent stage within a multi-agent pipeline or group chat workflow,
/// combining the response text with the captured diagnostics for that stage.
/// </summary>
/// <remarks>
/// <para>
/// Access stage results via <see cref="IPipelineRunResult.Stages"/> after calling
/// <c>RunWithDiagnosticsAsync</c>. Each stage corresponds to one agent turn in the
/// workflow.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var result = await workflow.RunWithDiagnosticsAsync(prompt, accessor);
/// foreach (var stage in result.Stages)
/// {
///     var tokens = stage.Diagnostics?.AggregateTokenUsage;
///     Console.WriteLine($"[{stage.AgentName}] {tokens?.TotalTokens ?? 0} tokens, " +
///         $"{stage.Diagnostics?.TotalDuration.TotalSeconds:F1}s");
///     var text = stage.FinalResponse?.Text ?? string.Empty;
///     Console.WriteLine($"  Response: {text[..Math.Min(100, text.Length)]}...");
/// }
/// </code>
/// </example>
public interface IAgentStageResult
{
    /// <summary>Gets the agent's executor ID (agent name, possibly with a MAF-assigned GUID suffix).</summary>
    string AgentName { get; }

    /// <summary>
    /// Gets the final <see cref="ChatResponse"/> the agent produced during this stage
    /// (preserving full message content, role, usage, and metadata), or
    /// <see langword="null"/> if the agent responded only via tool calls with no
    /// terminating text response. Call <c>.Text</c> for a flat text view when evaluating.
    /// </summary>
    ChatResponse? FinalResponse { get; }

    /// <summary>
    /// Gets the diagnostics captured during this agent's execution, including per-call
    /// token usage, tool call details, and timing. <see langword="null"/> if diagnostics
    /// were not enabled via <c>UsingDiagnostics()</c>.
    /// </summary>
    IAgentRunDiagnostics? Diagnostics { get; }

    /// <summary>
    /// Gets the outcome of this stage's execution. Defaults to
    /// <see cref="StageOutcome.Succeeded"/> for backward compatibility with
    /// implementations that do not track outcomes.
    /// </summary>
    StageOutcome Outcome => StageOutcome.Succeeded;

    /// <summary>
    /// Gets the name of the pipeline phase this stage belongs to, or
    /// <see langword="null"/> when running a flat (non-phased) pipeline.
    /// </summary>
    string? PhaseName => null;

    /// <summary>
    /// Gets the typed termination cause for this stage. <see langword="null"/> when
    /// the executor or runner did not specify one (legacy implementations / executors
    /// with no termination metadata).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use pattern matching to inspect specific cases:
    /// </para>
    /// <code>
    /// if (stage.Termination is StageTermination.MaxToolCallsReached { Limit: var limit })
    /// {
    ///     _logger.LogWarning("Stage {Name} exceeded {Limit} tool calls",
    ///         stage.AgentName, limit);
    /// }
    /// </code>
    /// <para>
    /// For OpenTelemetry / Prometheus tag values, use
    /// <see cref="IStageTermination.ToTagValue"/> — it returns a stable,
    /// low-cardinality string suitable for use as a metric dimension.
    /// </para>
    /// <para>
    /// Distinct from <see cref="Outcome"/>: <see cref="Outcome"/> is the rollup
    /// 3-value enum the runner decided about the stage; <see cref="Termination"/>
    /// is the detailed reason. For example, an executor that uses
    /// <c>shouldTreatAsSuccess</c> to flip a <c>MaxIterationsReached</c> result to
    /// <see cref="StageOutcome.Succeeded"/> still reports
    /// <see cref="StageTermination.MaxIterationsReached"/> here.
    /// </para>
    /// <para>
    /// Typed as <see cref="IStageTermination"/> so consumers can supply their own
    /// typed extension cases by implementing the interface directly. The framework's
    /// closed <see cref="StageTermination"/> hierarchy (with the
    /// <see cref="StageTermination.Custom"/> escape hatch) covers the canonical
    /// cases; see <see cref="IStageTermination"/> for the contract on extending.
    /// </para>
    /// </remarks>
    IStageTermination? Termination => null;
}
