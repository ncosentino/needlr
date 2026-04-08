using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Budget;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Budget;

/// <summary>
/// <see cref="DelegatingChatClient"/> that accumulates token usage from each LLM call
/// into an <see cref="ITokenBudgetTracker"/> and aborts when the budget is exceeded.
/// </summary>
/// <remarks>
/// <para>
/// The pre-call gate checks the current token total <em>before</em> forwarding the request.
/// If the budget is already exhausted, <see cref="TokenBudgetExceededException"/> is thrown
/// immediately without hitting the LLM.
/// </para>
/// <para>
/// After a successful LLM call, <c>ChatResponse.Usage.TotalTokenCount</c> is added to
/// the tracker. If no usage data is available the count is not updated.
/// </para>
/// <para>
/// Wired automatically when <c>UsingTokenBudget()</c> is called on
/// <see cref="NexusLabs.Needlr.AgentFramework.AgentFrameworkSyringe"/>.
/// </para>
/// <para>
/// <strong>Limitation:</strong> Only <c>GetResponseAsync</c> is budget-tracked.
/// Streaming via <c>GetStreamingResponseAsync</c> passes through to the inner client
/// without token tracking or budget enforcement. If your agents use streaming completions,
/// token budgets will not be enforced for those calls.
/// </para>
/// </remarks>
public sealed class TokenBudgetChatMiddleware : DelegatingChatClient
{
    private readonly ITokenBudgetTracker _tracker;

    /// <param name="innerClient">The inner chat client to delegate to.</param>
    /// <param name="tracker">The token budget tracker scoped to the current pipeline run.</param>
    public TokenBudgetChatMiddleware(IChatClient innerClient, ITokenBudgetTracker tracker)
        : base(innerClient)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        _tracker = tracker;
    }

    /// <inheritdoc />
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        // Pre-call gate: abort if budget already exhausted.
        if (_tracker.MaxTokens.HasValue && _tracker.CurrentTokens >= _tracker.MaxTokens.Value)
        {
            ThrowBudgetExceeded(_tracker.CurrentTokens, _tracker.MaxTokens.Value);
        }

        var response = await base.GetResponseAsync(messages, options, cancellationToken)
            .ConfigureAwait(false);

        // Accumulate tokens from this call.
        if (response.Usage?.TotalTokenCount is long tokens)
        {
            _tracker.Record(tokens);

            // Post-call check: throw if this call pushed us over the limit.
            if (_tracker.MaxTokens.HasValue && _tracker.CurrentTokens >= _tracker.MaxTokens.Value)
            {
                ThrowBudgetExceeded(_tracker.CurrentTokens, _tracker.MaxTokens.Value);
            }
        }

        return response;
    }

    /// <summary>
    /// Throws <see cref="OperationCanceledException"/> wrapping <see cref="TokenBudgetExceededException"/>.
    /// <see cref="OperationCanceledException"/> is respected by MAF's workflow orchestration (which
    /// swallows <see cref="TokenBudgetExceededException"/> but stops on cancellation).
    /// Callers can inspect <see cref="Exception.InnerException"/> for budget details.
    /// </summary>
    private static void ThrowBudgetExceeded(long currentTokens, long maxTokens)
    {
        var budgetException = new TokenBudgetExceededException(currentTokens, maxTokens);
        throw new OperationCanceledException(budgetException.Message, budgetException);
    }
}
