using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

/// <summary>
/// Describes the outcome of executing a single pipeline stage, including
/// diagnostics, response text, success/failure state, and a typed
/// <see cref="StageTermination"/> describing why the stage terminated.
/// </summary>
/// <param name="StageName">The name of the stage that was executed.</param>
/// <param name="Diagnostics">Captured diagnostics for the stage, or <see langword="null"/> if not available.</param>
/// <param name="ResponseText">The text response produced by the stage, or <see langword="null"/> for non-agent stages.</param>
/// <param name="Succeeded">Whether the stage completed successfully.</param>
/// <param name="Exception">The exception that caused failure, or <see langword="null"/> on success.</param>
/// <param name="FailureDisposition">Controls how the pipeline runner handles a failed result.
/// Only meaningful when <paramref name="Succeeded"/> is <see langword="false"/>.</param>
/// <param name="Termination">
/// Typed termination cause for the stage. <see langword="null"/> when the executor
/// did not specify one (legacy executors, custom executors that do not populate it).
/// Populated automatically by the static factories: <see cref="Failed"/> wraps the
/// exception, <see cref="Skipped"/> wraps the optional reason. Callers can supply
/// an explicit value to override the defaults.
/// </param>
/// <example>
/// <code>
/// var result = StageExecutionResult.Success("Writer", diagnostics, "Draft text");
/// if (result.Succeeded)
///     Console.WriteLine($"Stage '{result.StageName}' produced: {result.ResponseText}");
/// </code>
/// </example>
public sealed record StageExecutionResult(
    string StageName,
    IAgentRunDiagnostics? Diagnostics,
    string? ResponseText,
    bool Succeeded,
    Exception? Exception = null,
    FailureDisposition FailureDisposition = FailureDisposition.AbortPipeline,
    IStageTermination? Termination = null)
{
    /// <summary>
    /// Creates a successful result for a stage.
    /// </summary>
    /// <param name="stageName">The name of the completed stage.</param>
    /// <param name="diagnostics">Captured diagnostics, or <see langword="null"/>.</param>
    /// <param name="responseText">The text response, or <see langword="null"/>.</param>
    /// <param name="termination">Optional typed termination cause.</param>
    /// <returns>A successful <see cref="StageExecutionResult"/>.</returns>
    public static StageExecutionResult Success(
        string stageName,
        IAgentRunDiagnostics? diagnostics,
        string? responseText,
        IStageTermination? termination = null) =>
        new(stageName, diagnostics, responseText, Succeeded: true, Termination: termination);

    /// <summary>
    /// Creates a failed result for a stage.
    /// </summary>
    /// <param name="stageName">The name of the failed stage.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <param name="diagnostics">Captured diagnostics, or <see langword="null"/>.</param>
    /// <param name="disposition">How the pipeline runner should handle this failure.
    /// Defaults to <see cref="FailureDisposition.AbortPipeline"/>.</param>
    /// <param name="termination">
    /// Optional typed termination cause. When <see langword="null"/>, defaults to
    /// <see cref="StageTermination.Failed"/> wrapping <paramref name="exception"/>.
    /// </param>
    /// <returns>A failed <see cref="StageExecutionResult"/>.</returns>
    public static StageExecutionResult Failed(
        string stageName,
        Exception exception,
        IAgentRunDiagnostics? diagnostics = null,
        FailureDisposition disposition = FailureDisposition.AbortPipeline,
        IStageTermination? termination = null) =>
        new(stageName, diagnostics, ResponseText: null, Succeeded: false,
            Exception: exception, FailureDisposition: disposition,
            Termination: termination ?? new StageTermination.Failed(exception));

    /// <summary>
    /// Creates a result indicating the stage was skipped.
    /// </summary>
    /// <param name="stageName">The name of the skipped stage.</param>
    /// <param name="reason">
    /// Optional free-form reason describing why the stage was skipped. Carried into
    /// the populated <see cref="StageTermination.Skipped"/> termination value.
    /// </param>
    /// <returns>A successful <see cref="StageExecutionResult"/> with no diagnostics or response.</returns>
    public static StageExecutionResult Skipped(string stageName, string? reason = null) =>
        new(stageName, Diagnostics: null, ResponseText: null, Succeeded: true,
            Termination: new StageTermination.Skipped(reason));
}
