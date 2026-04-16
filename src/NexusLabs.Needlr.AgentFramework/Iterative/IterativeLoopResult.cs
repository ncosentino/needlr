using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Iterative;

/// <summary>
/// The result of running an <see cref="IIterativeAgentLoop"/> to completion,
/// including per-iteration records, aggregate diagnostics, and the final model response.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Iterations"/> provides a complete trace of what happened in each iteration —
/// which tools were called, what text was produced, and how many tokens were consumed.
/// This is the primary diagnostic surface for understanding loop behavior and optimizing
/// prompt factories.
/// </para>
/// </remarks>
/// <param name="Iterations">
/// Per-iteration records in execution order. Always contains at least one entry.
/// </param>
/// <param name="FinalResponse">
/// The last text response produced by the model, or <see langword="null"/> if the loop
/// was terminated by <see cref="IterativeLoopOptions.MaxIterations"/>,
/// <see cref="IterativeLoopOptions.IsComplete"/>, or cancellation before the model
/// produced a text response.
/// </param>
/// <param name="Diagnostics">
/// Aggregate diagnostics for the entire loop run, including total token usage,
/// all chat completions, and all tool calls. <see langword="null"/> if diagnostics
/// were not enabled via <c>UsingDiagnostics()</c>.
/// </param>
/// <param name="Succeeded">Whether the loop completed without errors.</param>
/// <param name="ErrorMessage">
/// The error message if the loop failed; <see langword="null"/> on success.
/// Populated when the prompt factory throws, the loop is cancelled, or an
/// unrecoverable LLM error occurs.
/// </param>
/// <param name="Termination">
/// Why the loop stopped. Use this instead of inspecting <see cref="Succeeded"/>
/// and <see cref="ErrorMessage"/> separately — it provides a single discriminator
/// for all termination paths.
/// </param>
/// <param name="Configuration">
/// Snapshot of the resolved configuration used for this run. Allows consumers
/// to inspect mode, limits, and budget settings after execution without
/// referencing the original <see cref="IterativeLoopOptions"/>.
/// </param>
public sealed record IterativeLoopResult(
    IReadOnlyList<IterationRecord> Iterations,
    string? FinalResponse,
    IAgentRunDiagnostics? Diagnostics,
    bool Succeeded,
    string? ErrorMessage,
    TerminationReason Termination,
    IterativeLoopConfiguration Configuration);
