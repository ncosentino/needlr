namespace NexusLabs.Needlr.AgentFramework.Budget;

/// <summary>
/// Tracks token usage within a scoped budget, enabling pipeline-level token limits
/// for total, input, and/or output tokens independently.
/// </summary>
/// <remarks>
/// <para>
/// Each call to <see cref="BeginScope(long)"/> or
/// <see cref="BeginScope(long?, long?, long?)"/> opens a budget window in the current
/// async context. Concurrent pipeline runs each maintain their own independent token
/// counts via <see cref="System.Threading.AsyncLocal{T}"/>.
/// </para>
/// <para>
/// <see cref="ITokenBudgetTracker"/> is automatically registered in DI by
/// <c>UsingAgentFramework()</c>. Wire the chat-level middleware by calling
/// <c>UsingTokenBudget()</c> on <see cref="AgentFrameworkSyringe"/>.
/// </para>
/// </remarks>
public interface ITokenBudgetTracker
{
    /// <summary>
    /// Opens a token-budget scope with a total token limit.
    /// </summary>
    /// <param name="maxTokens">Maximum total tokens allowed.</param>
    /// <returns>A disposable handle that ends the scope when disposed.</returns>
    IDisposable BeginScope(long maxTokens);

    /// <summary>
    /// Opens a token-budget scope with granular limits for input, output,
    /// and/or total tokens. At least one limit must be specified.
    /// </summary>
    /// <param name="maxInputTokens">Maximum input tokens, or <see langword="null"/> for no limit.</param>
    /// <param name="maxOutputTokens">Maximum output tokens, or <see langword="null"/> for no limit.</param>
    /// <param name="maxTotalTokens">Maximum total tokens, or <see langword="null"/> for no limit.</param>
    /// <returns>A disposable handle that ends the scope when disposed.</returns>
    /// <exception cref="ArgumentException">All three parameters are <see langword="null"/>.</exception>
    IDisposable BeginScope(long? maxInputTokens = null, long? maxOutputTokens = null, long? maxTotalTokens = null);

    /// <summary>
    /// Gets the <see cref="CancellationToken"/> for the active scope that is cancelled
    /// when any budget limit is exceeded.
    /// </summary>
    /// <value><see cref="CancellationToken.None"/> if no scope is active.</value>
    CancellationToken BudgetCancellationToken { get; }

    /// <summary>Gets the total tokens accumulated so far in the active scope.</summary>
    /// <value>0 if no scope is active.</value>
    long CurrentTokens { get; }

    /// <summary>Gets the input tokens accumulated so far in the active scope.</summary>
    /// <value>0 if no scope is active.</value>
    long CurrentInputTokens { get; }

    /// <summary>Gets the output tokens accumulated so far in the active scope.</summary>
    /// <value>0 if no scope is active.</value>
    long CurrentOutputTokens { get; }

    /// <summary>Gets the total token budget limit of the active scope.</summary>
    /// <value><see langword="null"/> if no scope is active or no total limit set.</value>
    long? MaxTokens { get; }

    /// <summary>Gets the input token budget limit of the active scope.</summary>
    /// <value><see langword="null"/> if no scope is active or no input limit set.</value>
    long? MaxInputTokens { get; }

    /// <summary>Gets the output token budget limit of the active scope.</summary>
    /// <value><see langword="null"/> if no scope is active or no output limit set.</value>
    long? MaxOutputTokens { get; }

    /// <summary>
    /// Records <paramref name="tokenCount"/> as total tokens against the active scope's budget.
    /// Called automatically by <c>TokenBudgetChatMiddleware</c> after each LLM response.
    /// </summary>
    void Record(long tokenCount);

    /// <summary>
    /// Records input and output tokens separately against the active scope's budget.
    /// Called automatically by <c>TokenBudgetChatMiddleware</c> after each LLM response.
    /// </summary>
    void Record(long inputTokens, long outputTokens);

    /// <summary>
    /// Opens a child scope with its own budget that counts against the parent.
    /// Token usage in the child rolls up to the parent in real-time. Exceeding
    /// the child's limit cancels the child's token. If the parent scope is
    /// cancelled, all active children are also cancelled.
    /// </summary>
    /// <param name="name">Human-readable name for diagnostics (e.g., stage name).</param>
    /// <param name="maxTokens">Maximum total tokens for this child scope, or
    /// <see langword="null"/> for unlimited (still counts against parent).</param>
    /// <returns>A disposable handle that restores the parent scope when disposed.</returns>
    /// <exception cref="InvalidOperationException">No parent scope is active.</exception>
    IDisposable BeginChildScope(string name, long? maxTokens = null);
}

/// <summary>
/// Thrown when a pipeline's token budget is exceeded.
/// </summary>
public sealed class TokenBudgetExceededException : Exception
{
    /// <summary>Gets the number of tokens accumulated at the time the budget was exceeded.</summary>
    public long CurrentTokens { get; }

    /// <summary>Gets the maximum token budget that was exceeded.</summary>
    public long MaxTokens { get; }

    /// <param name="currentTokens">Accumulated token count.</param>
    /// <param name="maxTokens">The budget limit that was exceeded.</param>
    public TokenBudgetExceededException(long currentTokens, long maxTokens)
        : base($"Token budget exceeded: used {currentTokens} of {maxTokens} tokens.")
    {
        CurrentTokens = currentTokens;
        MaxTokens = maxTokens;
    }
}
