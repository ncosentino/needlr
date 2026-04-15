namespace NexusLabs.Needlr.AgentFramework.Iterative;

/// <summary>
/// Runs an iterative agent loop where each iteration builds a fresh prompt from workspace
/// state instead of accumulating conversation history.
/// </summary>
/// <remarks>
/// <para>
/// Unlike the standard <c>FunctionInvokingChatClient</c> approach (where conversation history
/// grows with every tool call, producing O(n²) token cost), the iterative loop maintains
/// O(n) cost by constructing independent prompts per iteration. The workspace (files) IS the
/// memory — not the conversation.
/// </para>
/// <para>
/// Each iteration:
/// </para>
/// <list type="number">
///   <item><description>The <see cref="IterativeLoopOptions.PromptFactory"/> builds a user message
///   from the current workspace state and (optionally) last tool results.</description></item>
///   <item><description>The loop sends <c>[system, user]</c> to the LLM with available tools.</description></item>
///   <item><description>Tool calls are executed, updating the workspace.</description></item>
///   <item><description>Depending on <see cref="IterativeLoopOptions.ToolResultMode"/>, tool results
///   may be sent back to the model within the same iteration.</description></item>
///   <item><description>The iteration ends. The next iteration starts fresh.</description></item>
/// </list>
/// <para>
/// The loop terminates when the model produces a text response (no tool calls), the maximum
/// iteration count is reached, the <see cref="IterativeLoopOptions.IsComplete"/> predicate
/// fires, or the <see cref="CancellationToken"/> is cancelled.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var workspace = new InMemoryWorkspace();
/// var options = new IterativeLoopOptions
/// {
///     LoopName = "trip-planner",
///     Instructions = "You plan multi-stop trips. Use tools to search and build itineraries.",
///     Tools = [searchTool, addLegTool, getItineraryTool],
///     PromptFactory = ctx =>
///     {
///         var itinerary = ctx.Workspace.FileExists("itinerary.json")
///             ? ctx.Workspace.ReadFile("itinerary.json")
///             : "No legs planned yet.";
///         return $"Plan a 7-day trip to Japan. Current itinerary:\n{itinerary}";
///     },
///     MaxIterations = 15,
/// };
///
/// var context = new IterativeContext { Workspace = workspace };
/// var result = await loop.RunAsync(options, context, cancellationToken);
///
/// Console.WriteLine($"Completed in {result.Iterations.Count} iterations");
/// Console.WriteLine($"Total tokens: {result.Diagnostics?.AggregateTokenUsage.TotalTokens}");
/// </code>
/// </example>
public interface IIterativeAgentLoop
{
    /// <summary>
    /// Runs the iterative loop to completion.
    /// </summary>
    /// <param name="options">
    /// Configuration for this run: instructions, tools, prompt factory, iteration limits,
    /// and tool result handling mode.
    /// </param>
    /// <param name="context">
    /// Mutable context carrying the workspace, tool results, and arbitrary state across
    /// iterations. The caller creates this and can pre-populate the workspace and state.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation token. When cancelled, the loop completes the current tool execution
    /// (if any) and returns a partial result.
    /// </param>
    /// <returns>
    /// A result containing per-iteration records, the final model response, aggregate
    /// diagnostics, and success/failure status.
    /// </returns>
    Task<IterativeLoopResult> RunAsync(
        IterativeLoopOptions options,
        IterativeContext context,
        CancellationToken cancellationToken = default);
}
