namespace NexusLabs.Needlr.AgentFramework.Iterative;

/// <summary>
/// Controls how tool call results are fed back to the model within a single iteration
/// of an <see cref="IIterativeAgentLoop"/>.
/// </summary>
/// <remarks>
/// <para>
/// The choice of mode determines the cost profile and interaction pattern of each iteration:
/// </para>
/// <list type="bullet">
///   <item>
///     <term><see cref="SingleCall"/></term>
///     <description>Maximum cost control — one LLM call per iteration, always.</description>
///   </item>
///   <item>
///     <term><see cref="OneRoundTrip"/></term>
///     <description>Balanced default — model sees its results once, bounded to two LLM calls.</description>
///   </item>
///   <item>
///     <term><see cref="MultiRound"/></term>
///     <description>Natural tool chaining within an iteration, bounded by
///     <see cref="IterativeLoopOptions.MaxToolRoundsPerIteration"/>.</description>
///   </item>
/// </list>
/// <para>
/// Regardless of mode, tool results from the final round of an iteration are always
/// available via <see cref="IterativeContext.LastToolResults"/> for the next iteration's
/// prompt factory.
/// </para>
/// </remarks>
public enum ToolResultMode
{
    /// <summary>
    /// Tool results are sent back to the model, which may request additional tool calls.
    /// This continues until the model produces a text response or
    /// <see cref="IterativeLoopOptions.MaxToolRoundsPerIteration"/> is reached.
    /// </summary>
    /// <remarks>
    /// Best for simple agents where tool chaining is natural and the per-iteration
    /// conversation is expected to be short. Set
    /// <see cref="IterativeLoopOptions.MaxToolRoundsPerIteration"/> to prevent unbounded growth.
    /// </remarks>
    MultiRound,

    /// <summary>
    /// Tool results are NOT sent back to the model. They are stored in
    /// <see cref="IterativeContext.LastToolResults"/> and the iteration ends immediately.
    /// Exactly one LLM call per iteration.
    /// </summary>
    /// <remarks>
    /// Maximum cost control. The prompt factory has full authority over what the model
    /// sees in the next iteration — it can selectively embed tool results, summarize them,
    /// or omit them entirely.
    /// </remarks>
    SingleCall,

    /// <summary>
    /// Tool results are sent back to the model exactly once. The model gets one chance
    /// to produce a text response or request more tool calls. Any additional tool call
    /// requests are executed and stored in <see cref="IterativeContext.LastToolResults"/>
    /// for the next iteration. Maximum two LLM calls per iteration.
    /// </summary>
    /// <remarks>
    /// The recommended default. The model sees its results (matching how models are
    /// trained to interact with tools), but accumulation is bounded to at most one
    /// round-trip of tool results within a single iteration.
    /// </remarks>
    OneRoundTrip,
}
