namespace NexusLabs.Needlr.AgentFramework.Iterative;

/// <summary>
/// The result of executing a single tool call within an iteration of an
/// <see cref="IIterativeAgentLoop"/>.
/// </summary>
/// <remarks>
/// <para>
/// Tool call results are ephemeral — they are available via
/// <see cref="IterativeContext.LastToolResults"/> for exactly one iteration (the next one).
/// The prompt factory decides whether to embed them in the next prompt.
/// </para>
/// <para>
/// When a tool call fails (exception or unknown function), <see cref="Succeeded"/> is
/// <see langword="false"/> and <see cref="ErrorMessage"/> contains the failure reason.
/// The loop does not abort on tool failure — subsequent tool calls in the same response
/// are still executed.
/// </para>
/// </remarks>
/// <param name="FunctionName">The name of the tool/function that was called.</param>
/// <param name="Arguments">
/// The arguments the model provided for the tool call. Keys are parameter names;
/// values are the deserialized argument values.
/// </param>
/// <param name="Result">
/// The return value from the tool execution, or <see langword="null"/> if the tool
/// returned void or the call failed.
/// </param>
/// <param name="Duration">Wall-clock time for the tool execution.</param>
/// <param name="Succeeded">Whether the tool call completed without throwing.</param>
/// <param name="ErrorMessage">
/// The exception message if the tool call failed; <see langword="null"/> on success.
/// </param>
public sealed record ToolCallResult(
    string FunctionName,
    IReadOnlyDictionary<string, object?> Arguments,
    object? Result,
    TimeSpan Duration,
    bool Succeeded,
    string? ErrorMessage);
