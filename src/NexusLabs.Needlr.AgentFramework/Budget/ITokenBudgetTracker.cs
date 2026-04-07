namespace NexusLabs.Needlr.AgentFramework.Budget;

/// <summary>
/// Tracks token usage within a scoped budget, enabling pipeline-level token limits.
/// </summary>
/// <remarks>
/// <para>
/// Each call to <see cref="BeginScope"/> opens a budget window in the current async context.
/// Concurrent pipeline runs each maintain their own independent token count via
/// <see cref="System.Threading.AsyncLocal{T}"/>.
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
    /// Opens a token-budget scope for the current async context.
    /// Disposing the returned handle ends the scope.
    /// </summary>
    /// <param name="maxTokens">
    /// Maximum tokens allowed within this scope. Once the budget is reached or exceeded
    /// the next LLM call throws <see cref="TokenBudgetExceededException"/>.
    /// </param>
    /// <returns>A disposable handle that ends the scope when disposed.</returns>
    IDisposable BeginScope(long maxTokens);

    /// <summary>Gets the number of tokens accumulated so far in the active scope.</summary>
    /// <value>0 if no scope is active.</value>
    long CurrentTokens { get; }

    /// <summary>Gets the token budget limit of the active scope.</summary>
    /// <value><see langword="null"/> if no scope is active.</value>
    long? MaxTokens { get; }

    /// <summary>
    /// Records <paramref name="tokenCount"/> against the active scope's budget.
    /// Called automatically by <c>TokenBudgetChatMiddleware</c> after each LLM response.
    /// </summary>
    void Record(long tokenCount);
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
