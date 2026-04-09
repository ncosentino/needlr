using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Budget;
using NexusLabs.Needlr.AgentFramework.Progress;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Budget;

/// <summary>
/// <see cref="DelegatingChatClient"/> that accumulates token usage from each LLM call
/// into an <see cref="ITokenBudgetTracker"/> and aborts when the budget is exceeded.
/// Emits <see cref="BudgetUpdatedEvent"/> and <see cref="BudgetExceededEvent"/> to the
/// progress reporter in real-time.
/// </summary>
/// <remarks>
/// <para>
/// Budget enforcement uses two mechanisms:
/// <list type="number">
///   <item>
///     <see cref="OperationCanceledException"/> wrapping <see cref="TokenBudgetExceededException"/>
///     thrown from the middleware (works for direct agent runs).
///   </item>
///   <item>
///     <see cref="ITokenBudgetTracker.BudgetCancellationToken"/> cancelled when tokens are recorded
///     past the limit (works for MAF workflow runs — pass this token to the workflow).
///   </item>
/// </list>
/// </para>
/// <para>
/// <strong>Limitation:</strong> Only <c>GetResponseAsync</c> is budget-tracked.
/// Streaming via <c>GetStreamingResponseAsync</c> passes through without enforcement.
/// </para>
/// </remarks>
public sealed class TokenBudgetChatMiddleware : DelegatingChatClient
{
    private readonly ITokenBudgetTracker _tracker;
    private readonly IProgressReporterAccessor _progressAccessor;

    /// <param name="innerClient">The inner chat client to delegate to.</param>
    /// <param name="tracker">The token budget tracker scoped to the current pipeline run.</param>
    /// <param name="progressAccessor">Progress reporter accessor for emitting budget events.</param>
    public TokenBudgetChatMiddleware(
        IChatClient innerClient,
        ITokenBudgetTracker tracker,
        IProgressReporterAccessor progressAccessor)
        : base(innerClient)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentNullException.ThrowIfNull(progressAccessor);
        _tracker = tracker;
        _progressAccessor = progressAccessor;
    }

    /// <inheritdoc />
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        // Pre-call gate: abort if any budget already exhausted.
        if (IsBudgetExceeded())
        {
            EmitBudgetExceededEvent();
            ThrowBudgetExceeded(_tracker.CurrentTokens, _tracker.MaxTokens ?? 0);
        }

        var response = await base.GetResponseAsync(messages, options, cancellationToken)
            .ConfigureAwait(false);

        // Accumulate tokens — this also cancels the BudgetCancellationToken if exceeded.
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

            EmitBudgetUpdatedEvent();

            // Post-call check: throw for direct agent runs.
            if (IsBudgetExceeded())
            {
                EmitBudgetExceededEvent();
                ThrowBudgetExceeded(_tracker.CurrentTokens, _tracker.MaxTokens ?? 0);
            }
        }

        return response;
    }

    private bool IsBudgetExceeded() =>
        (_tracker.MaxTokens.HasValue && _tracker.CurrentTokens >= _tracker.MaxTokens.Value) ||
        (_tracker.MaxInputTokens.HasValue && _tracker.CurrentInputTokens >= _tracker.MaxInputTokens.Value) ||
        (_tracker.MaxOutputTokens.HasValue && _tracker.CurrentOutputTokens >= _tracker.MaxOutputTokens.Value);

    private void EmitBudgetUpdatedEvent()
    {
        var reporter = _progressAccessor.Current;
        reporter.Report(new BudgetUpdatedEvent(
            Timestamp: DateTimeOffset.UtcNow,
            WorkflowId: reporter.WorkflowId,
            AgentId: reporter.AgentId,
            ParentAgentId: null,
            Depth: reporter.Depth,
            SequenceNumber: ProgressSequence.Next(),
            CurrentInputTokens: _tracker.CurrentInputTokens,
            CurrentOutputTokens: _tracker.CurrentOutputTokens,
            CurrentTotalTokens: _tracker.CurrentTokens,
            MaxInputTokens: _tracker.MaxInputTokens,
            MaxOutputTokens: _tracker.MaxOutputTokens,
            MaxTotalTokens: _tracker.MaxTokens));
    }

    private void EmitBudgetExceededEvent()
    {
        var reporter = _progressAccessor.Current;
        var (limitType, current, max) =
            _tracker.MaxInputTokens.HasValue && _tracker.CurrentInputTokens >= _tracker.MaxInputTokens.Value
                ? ("input", _tracker.CurrentInputTokens, _tracker.MaxInputTokens.Value)
            : _tracker.MaxOutputTokens.HasValue && _tracker.CurrentOutputTokens >= _tracker.MaxOutputTokens.Value
                ? ("output", _tracker.CurrentOutputTokens, _tracker.MaxOutputTokens.Value)
            : ("total", _tracker.CurrentTokens, _tracker.MaxTokens ?? 0);

        reporter.Report(new BudgetExceededEvent(
            Timestamp: DateTimeOffset.UtcNow,
            WorkflowId: reporter.WorkflowId,
            AgentId: reporter.AgentId,
            ParentAgentId: null,
            Depth: reporter.Depth,
            SequenceNumber: ProgressSequence.Next(),
            LimitType: limitType,
            CurrentValue: current,
            MaxValue: max));
    }

    private void ThrowBudgetExceeded(long currentTokens, long maxTokens)
    {
        var budgetException = new TokenBudgetExceededException(currentTokens, maxTokens);
        throw new OperationCanceledException(
            budgetException.Message,
            budgetException,
            _tracker.BudgetCancellationToken);
    }
}
