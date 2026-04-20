namespace NexusLabs.Needlr.AgentFramework.Iterative;

/// <summary>
/// Controls when the <see cref="IterativeLoopOptions.IsComplete"/> predicate is
/// evaluated relative to tool call execution within an iteration.
/// </summary>
/// <remarks>
/// <para>
/// By default (<see cref="None"/>), <see cref="IterativeLoopOptions.IsComplete"/>
/// is checked only between iterations — after the iteration's round loop finishes
/// and the <see cref="IterationRecord"/> is built. This means that even when a tool
/// call satisfies the completion condition mid-iteration, the loop makes one more
/// <c>ChatCompletion</c> API call before noticing.
/// </para>
/// <para>
/// The <see cref="AfterToolRounds"/> and <see cref="AfterEachToolCall"/> modes add
/// earlier check points within an iteration to enable cost-saving early exit.
/// </para>
/// </remarks>
public enum ToolCompletionCheckMode
{
    /// <summary>
    /// No additional <see cref="IterativeLoopOptions.IsComplete"/> checks beyond
    /// the standard between-iteration check. This is the default and preserves
    /// backward-compatible behavior.
    /// </summary>
    None,

    /// <summary>
    /// Check <see cref="IterativeLoopOptions.IsComplete"/> after each round's batch
    /// of tool calls completes (i.e., after <c>ExecuteToolCallsAsync</c> returns).
    /// If the predicate returns <see langword="true"/>, the loop exits immediately
    /// without making the next <c>ChatCompletion</c> call, saving the input token
    /// cost of the wasted round-trip.
    /// </summary>
    /// <remarks>
    /// All tool calls requested by the model in a single round are still executed.
    /// Only the subsequent <c>ChatCompletion</c> call is avoided.
    /// </remarks>
    AfterToolRounds,

    /// <summary>
    /// Check <see cref="IterativeLoopOptions.IsComplete"/> after each individual
    /// tool call completes within a round. If the predicate returns
    /// <see langword="true"/>, remaining tool calls in the batch are skipped AND
    /// the next <c>ChatCompletion</c> call is avoided.
    /// </summary>
    /// <remarks>
    /// This is the most aggressive early-exit mode. It saves both tool execution
    /// cost (for skipped tool calls) and <c>ChatCompletion</c> input token cost.
    /// </remarks>
    AfterEachToolCall,
}
