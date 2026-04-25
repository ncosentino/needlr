using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

/// <summary>
/// Describes the outcome of executing a single pipeline stage, including
/// diagnostics, response text, and success/failure state.
/// </summary>
/// <param name="StageName">The name of the stage that was executed.</param>
/// <param name="Diagnostics">Captured diagnostics for the stage, or <see langword="null"/> if not available.</param>
/// <param name="ResponseText">The text response produced by the stage, or <see langword="null"/> for non-agent stages.</param>
/// <param name="Succeeded">Whether the stage completed successfully.</param>
/// <param name="Exception">The exception that caused failure, or <see langword="null"/> on success.</param>
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
    Exception? Exception = null)
{
    /// <summary>
    /// Creates a successful result for a stage.
    /// </summary>
    /// <param name="stageName">The name of the completed stage.</param>
    /// <param name="diagnostics">Captured diagnostics, or <see langword="null"/>.</param>
    /// <param name="responseText">The text response, or <see langword="null"/>.</param>
    /// <returns>A successful <see cref="StageExecutionResult"/>.</returns>
    public static StageExecutionResult Success(
        string stageName,
        IAgentRunDiagnostics? diagnostics,
        string? responseText) =>
        new(stageName, diagnostics, responseText, Succeeded: true);

    /// <summary>
    /// Creates a failed result for a stage.
    /// </summary>
    /// <param name="stageName">The name of the failed stage.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <param name="diagnostics">Captured diagnostics, or <see langword="null"/>.</param>
    /// <returns>A failed <see cref="StageExecutionResult"/>.</returns>
    public static StageExecutionResult Failed(
        string stageName,
        Exception exception,
        IAgentRunDiagnostics? diagnostics = null) =>
        new(stageName, diagnostics, ResponseText: null, Succeeded: false, Exception: exception);

    /// <summary>
    /// Creates a result indicating the stage was skipped.
    /// </summary>
    /// <param name="stageName">The name of the skipped stage.</param>
    /// <returns>A successful <see cref="StageExecutionResult"/> with no diagnostics or response.</returns>
    public static StageExecutionResult Skipped(string stageName) =>
        new(stageName, Diagnostics: null, ResponseText: null, Succeeded: true);
}
