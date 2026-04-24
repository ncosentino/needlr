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
///             ? ctx.Workspace.TryReadFile("article.md").Value.Content
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
    /// Gets or sets the maximum cumulative tool call count across all iterations.
    /// When exceeded, the loop terminates with
    /// <see cref="TerminationReason.MaxToolCallsReached"/>.
    /// Defaults to <see langword="null"/> (unlimited).
    /// </summary>
    /// <remarks>
    /// This is a safety guard against runaway tool-calling loops that stay under
    /// <see cref="MaxIterations"/> but make an excessive number of tool calls per
    /// iteration. Set this when token cost is proportional to tool call volume
    /// (e.g., web search or fetch tools).
    /// </remarks>
    public int? MaxTotalToolCalls { get; set; }

    /// <summary>
    /// Gets or sets the stall detection configuration. When set, the loop
    /// compares consecutive iterations and terminates with
    /// <see cref="TerminationReason.StallDetected"/> if
    /// <see cref="StallDetectionOptions.ConsecutiveThreshold"/> iterations
    /// in a row have total token counts within
    /// <see cref="StallDetectionOptions.TolerancePercent"/> of each other.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stall detection catches loops where the LLM repeats identical work
    /// every iteration because it has no cross-iteration memory. Without stall
    /// detection, these loops burn through <see cref="MaxIterations"/> or
    /// <see cref="MaxTotalToolCalls"/> with zero useful output.
    /// </para>
    /// <para>
    /// When <see langword="null"/> (the default), no stall detection is
    /// performed — the loop relies on existing guards.
    /// </para>
    /// </remarks>
    public StallDetectionOptions? StallDetection { get; set; }

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
    /// Gets or sets an optional filter that narrows the tool list on each iteration.
    /// Receives the zero-based iteration number, the current <see cref="IterativeContext"/>,
    /// and the full <see cref="Tools"/> list. Returns the subset of tools the model should
    /// see for that iteration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this for <strong>phase-gating</strong> — restricting which tools are available
    /// based on the current workspace state. For example, a trip planner might offer only
    /// <c>search</c> during the research phase and only <c>add_leg</c>/<c>book_hotel</c>
    /// during the build phase.
    /// </para>
    /// <para>
    /// When <see langword="null"/> (the default), all <see cref="Tools"/> are available on
    /// every iteration.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// ToolFilter = (iteration, ctx, allTools) =>
    /// {
    ///     var phase = ctx.Workspace.TryReadFile("status.json").Value.Content;
    ///     var allowed = phase.Contains("research")
    ///         ? new[] { "search" }
    ///         : new[] { "add_leg", "book_hotel", "validate_trip" };
    ///     return allTools.Where(t => allowed.Contains(t.Name)).ToList();
    /// };
    /// </code>
    /// </example>
    public Func<int, IterativeContext, IReadOnlyList<AITool>, IReadOnlyList<AITool>>? ToolFilter { get; set; }

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

    private double? _budgetPressureThreshold;

    /// <summary>
    /// Gets or sets the fraction of the token budget at which the loop injects
    /// a finalization instruction. When
    /// <see cref="Budget.ITokenBudgetTracker.CurrentTokens"/> divided by
    /// <see cref="Budget.ITokenBudgetTracker.MaxTokens"/> reaches this value,
    /// the loop prepends <see cref="BudgetPressureInstruction"/> to the next
    /// iteration's prompt and runs one final iteration before terminating with
    /// <see cref="TerminationReason.BudgetPressure"/>.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="null"/> (disabled). Set to e.g. <c>0.8</c>
    /// (80%) to give the agent one iteration to finalize cleanly before the
    /// hard budget limit cancels the chat client. Must be between 0.0 and 1.0
    /// (exclusive) when set.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Value is not between 0 and 1.</exception>
    public double? BudgetPressureThreshold
    {
        get => _budgetPressureThreshold;
        set
        {
            if (value is < 0.0 or >= 1.0)
                throw new ArgumentOutOfRangeException(nameof(value), "BudgetPressureThreshold must be between 0.0 (inclusive) and 1.0 (exclusive).");
            _budgetPressureThreshold = value;
        }
    }

    /// <summary>
    /// Gets or sets the instruction prepended to the user message on the
    /// budget-pressure finalization iteration.
    /// </summary>
    public string BudgetPressureInstruction { get; set; } =
        "⚠️ TOKEN BUDGET PRESSURE: You are approaching the token budget limit. " +
        "Finalize your work NOW. Write any remaining output and stop. " +
        "Do not start new research or tool-heavy operations.";

    /// <summary>
    /// Gets or sets when to check <see cref="IsComplete"/> relative to tool call
    /// execution within an iteration. Defaults to
    /// <see cref="ToolCompletionCheckMode.None"/> (check only between iterations).
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set to <see cref="ToolCompletionCheckMode.AfterToolRounds"/>, the loop
    /// checks <see cref="IsComplete"/> after each round's batch of tool calls
    /// completes. When set to <see cref="ToolCompletionCheckMode.AfterEachToolCall"/>,
    /// the check runs after each individual tool call, and remaining tool calls in
    /// the batch are skipped if the predicate returns <see langword="true"/>.
    /// </para>
    /// <para>
    /// Both modes terminate the loop with
    /// <see cref="TerminationReason.CompletedEarlyAfterToolCall"/> when the check
    /// fires, allowing callers to distinguish early completion from the standard
    /// between-iteration completion (<see cref="TerminationReason.Completed"/>).
    /// </para>
    /// </remarks>
    public ToolCompletionCheckMode CheckCompletionAfterToolCalls { get; set; }

    /// <summary>
    /// Optional factory that wraps the <see cref="Microsoft.Extensions.AI.IChatClient"/>
    /// used for LLM calls within this loop run. Use this to inject per-loop middleware
    /// such as <c>ReducingChatClient</c> to cap within-iteration conversation growth.
    /// When <see langword="null"/>, the global chat client from
    /// <see cref="IChatClientAccessor"/> is used unmodified.
    /// </summary>
    /// <remarks>
    /// When both <see cref="ChatReducer"/> and <see cref="ChatClientFactory"/> are set,
    /// the reducer is applied first (innermost) and the factory wraps the result.
    /// </remarks>
    /// <example>
    /// <code>
    /// var loopOptions = new IterativeLoopOptions
    /// {
    ///     ChatClientFactory = inner => new ChatClientBuilder(inner)
    ///         .UseChatReducer(new MessageCountingChatReducer(maxNonSystemMessages: 20))
    ///         .Build(),
    /// };
    /// </code>
    /// </example>
    public Func<Microsoft.Extensions.AI.IChatClient, Microsoft.Extensions.AI.IChatClient>? ChatClientFactory { get; set; }

    /// <summary>
    /// Optional <see cref="Microsoft.Extensions.AI.IChatReducer"/> that automatically
    /// wraps the per-loop <see cref="Microsoft.Extensions.AI.IChatClient"/> with a
    /// <c>ReducingChatClient</c>. This is a convenience alternative to composing a
    /// <see cref="ChatClientFactory"/> manually.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set, the loop wraps the chat client with a <c>ReducingChatClient</c> using
    /// this reducer. If <see cref="ChatClientFactory"/> is also set, the reducer is
    /// applied first (innermost) and the factory wraps the result.
    /// </para>
    /// <para>
    /// Typical usage is to set this to a <c>MessageCountingChatReducer</c> or
    /// <c>SummarizingChatReducer</c> to prevent context window exhaustion during
    /// long-running iterative loops.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var loopOptions = new IterativeLoopOptions
    /// {
    ///     ChatReducer = new MessageCountingChatReducer(maxNonSystemMessages: 30),
    /// };
    /// </code>
    /// </example>
    public Microsoft.Extensions.AI.IChatReducer? ChatReducer { get; set; }
}
