using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Iterative;

/// <summary>
/// Configuration for a single run of an <see cref="IIterativeAgentLoop"/>.
/// </summary>
/// <remarks>
/// <para>
/// At minimum, callers must provide <see cref="Instructions"/> (the system prompt),
/// <see cref="Tools"/> (available tool functions), and <see cref="PromptFactory"/>
/// (builds the user message each iteration from workspace state).
/// </para>
/// <para>
/// The loop terminates when any of these conditions is met (checked in order):
/// </para>
/// <list type="number">
///   <item><description>The <see cref="CancellationToken"/> is cancelled.</description></item>
///   <item><description><see cref="MaxIterations"/> is reached.</description></item>
///   <item><description><see cref="IsComplete"/> returns <see langword="true"/>.</description></item>
///   <item><description>The model produces a text response without requesting tool calls
///   (natural completion).</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// var options = new IterativeLoopOptions
/// {
///     LoopName = "article-writer",
///     Instructions = "You are a travel article writer...",
///     Tools = [searchTool, writeTool, outlineTool],
///     PromptFactory = ctx =>
///     {
///         var article = ctx.Workspace.FileExists("article.md")
///             ? ctx.Workspace.ReadFile("article.md")
///             : "(empty)";
///         return $"Continue writing. Current article:\n{article}";
///     },
///     MaxIterations = 20,
///     ToolResultMode = ToolResultMode.OneRoundTrip,
/// };
///
/// var result = await iterativeLoop.RunAsync(options, cancellationToken);
/// </code>
/// </example>
public sealed class IterativeLoopOptions
{
    /// <summary>
    /// Gets or sets a human-readable name for this loop run, used in diagnostics
    /// and progress events. Defaults to <c>"iterative-loop"</c>.
    /// </summary>
    public string LoopName { get; set; } = "iterative-loop";

    /// <summary>
    /// Gets or sets the system prompt (instructions) for the agent. Sent as the
    /// system message on every LLM call. This is constant across all iterations.
    /// </summary>
    public required string Instructions { get; set; }

    /// <summary>
    /// Gets or sets the tools available to the model. The loop matches tool call
    /// requests from the model against this list by function name.
    /// </summary>
    /// <remarks>
    /// Tools are typically created via <see cref="AIFunctionFactory"/> or obtained from
    /// the agent framework's function discovery. The same tool instances are reused
    /// across all iterations.
    /// </remarks>
    public required IReadOnlyList<AITool> Tools { get; set; }

    /// <summary>
    /// Gets or sets the factory that builds the user message for each iteration.
    /// Called once at the start of every iteration with the current
    /// <see cref="IterativeContext"/> (which includes workspace state and last tool results).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the core extensibility point. The prompt factory reads workspace files
    /// to understand current state, optionally includes data from
    /// <see cref="IterativeContext.LastToolResults"/>, and returns a fresh user message.
    /// </para>
    /// <para>
    /// The returned string becomes the sole user message — there is no conversation
    /// history. The workspace IS the memory.
    /// </para>
    /// </remarks>
    public required Func<IterativeContext, string> PromptFactory { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of iterations before the loop terminates.
    /// Defaults to <c>25</c>. Set to a lower value for cost-sensitive workloads.
    /// </summary>
    public int MaxIterations { get; set; } = 25;

    /// <summary>
    /// Gets or sets an optional predicate evaluated after each iteration. When it
    /// returns <see langword="true"/>, the loop terminates. The predicate receives the
    /// <see cref="IterativeContext"/> with updated workspace and tool results.
    /// </summary>
    /// <remarks>
    /// Use this for domain-specific termination conditions. For example, checking
    /// whether a required workspace file exists, or whether a word count target
    /// has been reached.
    /// </remarks>
    public Func<IterativeContext, bool>? IsComplete { get; set; }

    /// <summary>
    /// Gets or sets how tool call results are fed back to the model within a single
    /// iteration. Defaults to <see cref="Iterative.ToolResultMode.OneRoundTrip"/>.
    /// </summary>
    /// <seealso cref="Iterative.ToolResultMode"/>
    public ToolResultMode ToolResultMode { get; set; } = ToolResultMode.OneRoundTrip;

    /// <summary>
    /// Gets or sets the maximum number of tool-calling rounds within a single iteration
    /// when <see cref="ToolResultMode"/> is <see cref="Iterative.ToolResultMode.MultiRound"/>.
    /// Ignored for other modes. Defaults to <c>5</c>.
    /// </summary>
    /// <remarks>
    /// This is a safety valve to prevent unbounded within-iteration growth. After this many
    /// rounds of tool calls within one iteration, any remaining tool call requests are
    /// executed and stored in <see cref="IterativeContext.LastToolResults"/> for the next
    /// iteration.
    /// </remarks>
    public int MaxToolRoundsPerIteration { get; set; } = 5;

    /// <summary>
    /// Gets or sets an optional async callback invoked at the start of each iteration,
    /// before the prompt factory runs. Receives the zero-based iteration number and the
    /// current <see cref="IterativeContext"/>.
    /// </summary>
    /// <remarks>
    /// Use this for progress reporting (e.g., updating a SignalR client). Hook exceptions
    /// propagate directly to the caller — they are not caught by the loop's internal
    /// error handling.
    /// </remarks>
    public Func<int, IterativeContext, Task>? OnIterationStart { get; set; }

    /// <summary>
    /// Gets or sets an optional async callback invoked after each tool call completes.
    /// Receives the zero-based iteration number and the <see cref="ToolCallResult"/>.
    /// </summary>
    /// <remarks>
    /// Fired once per tool call, in execution order. Use for real-time progress updates
    /// such as streaming tool activity to a UI. Hook exceptions propagate to the caller.
    /// </remarks>
    public Func<int, ToolCallResult, Task>? OnToolCall { get; set; }

    /// <summary>
    /// Gets or sets an optional async callback invoked after each iteration completes.
    /// Receives the <see cref="IterationRecord"/> containing tool calls, tokens, and timing.
    /// </summary>
    /// <remarks>
    /// Fired after the <see cref="IterationRecord"/> is built and context is updated.
    /// Hook exceptions propagate to the caller.
    /// </remarks>
    public Func<IterationRecord, Task>? OnIterationEnd { get; set; }

    /// <summary>
    /// An optional <see cref="Context.IAgentExecutionContext"/> to use for
    /// bridging workspace state to DI-resolved tools.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set, the loop uses this context (and its workspace) for the
    /// <see cref="Context.IAgentExecutionContextAccessor"/> scope. When
    /// <see langword="null"/> (the default), the loop auto-creates a context
    /// from the <see cref="IterativeContext.Workspace"/> if an accessor is
    /// available via DI.
    /// </para>
    /// <para>
    /// This is the <strong>bootstrap execution context only</strong> — it is
    /// NOT the same as the full application execution context. It exists
    /// solely so that DI-resolved tool classes can call
    /// <c>accessor.Current.GetRequiredWorkspace()</c> during loop execution.
    /// </para>
    /// </remarks>
    public Context.IAgentExecutionContext? ExecutionContext { get; set; }
}
