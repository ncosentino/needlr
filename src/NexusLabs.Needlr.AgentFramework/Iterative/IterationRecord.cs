using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Iterative;

/// <summary>
/// Captures what happened during a single iteration of an <see cref="IIterativeAgentLoop"/>,
/// including tool calls made, any text response produced, token usage, and wall-clock duration.
/// </summary>
/// <param name="Iteration">Zero-based iteration index.</param>
/// <param name="ToolCalls">
/// All tool calls executed during this iteration, in execution order.
/// Empty if the model produced a text response without requesting tools.
/// </param>
/// <param name="FinalResponse">
/// The final <see cref="ChatResponse"/> produced by the model during this iteration
/// (preserving full message content, role, usage, and metadata), or
/// <see langword="null"/> if the model only made tool calls without a terminating
/// text response. In <see cref="ToolResultMode.OneRoundTrip"/> and
/// <see cref="ToolResultMode.MultiRound"/> modes, this is the response emitted after
/// tool results were sent back. Call <c>.Text</c> for a flat text view when evaluating.
/// </param>
/// <param name="Tokens">Aggregate token usage across all LLM calls in this iteration.</param>
/// <param name="Duration">Wall-clock time for the entire iteration (LLM calls + tool execution).</param>
/// <param name="LlmCallCount">
/// Number of LLM calls made during this iteration. Always 1 for
/// <see cref="ToolResultMode.SingleCall"/>, at most 2 for
/// <see cref="ToolResultMode.OneRoundTrip"/>.
/// </param>
/// <param name="ToolCallCount">
/// Number of tool calls executed during this iteration.
/// </param>
public sealed record IterationRecord(
    int Iteration,
    IReadOnlyList<ToolCallResult> ToolCalls,
    ChatResponse? FinalResponse,
    TokenUsage Tokens,
    TimeSpan Duration,
    int LlmCallCount,
    int ToolCallCount);
