using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Budget;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Budget;

/// <summary>
/// Lightweight <see cref="DelegatingChatClient"/> that records token usage from
/// each LLM call into <see cref="ITokenBudgetTracker"/>. Does NOT enforce budgets
/// — that is the responsibility of <see cref="TokenBudgetChatMiddleware"/>.
/// </summary>
/// <remarks>
/// Wired automatically by <c>UsingTokenTracking()</c>, <c>UsingTokenBudget()</c>,
/// and <c>UsingDiagnostics()</c>. Idempotent — only one instance is wired
/// regardless of how many extensions request it.
/// </remarks>
public sealed class TokenUsageRecordingMiddleware : DelegatingChatClient
{
    private readonly ITokenBudgetTracker _tracker;

    /// <param name="innerClient">The inner chat client to delegate to.</param>
    /// <param name="tracker">The token budget tracker to record usage into.</param>
    public TokenUsageRecordingMiddleware(
        IChatClient innerClient,
        ITokenBudgetTracker tracker)
        : base(innerClient)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        _tracker = tracker;
    }

    /// <inheritdoc />
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken)
            .ConfigureAwait(false);

        var usage = response.Usage;
        if (usage is not null)
        {
            var inputCount = usage.InputTokenCount ?? 0;
            var outputCount = usage.OutputTokenCount ?? 0;

            if (inputCount > 0 || outputCount > 0)
            {
                _tracker.Record(inputCount, outputCount);
            }
            else if (usage.TotalTokenCount is long totalOnly)
            {
                _tracker.Record(totalOnly);
            }
        }

        return response;
    }
}
