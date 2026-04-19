using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework;

using NexusLabs.Needlr.AgentFramework.Progress;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Budget;

/// <summary>
/// <see cref="DelegatingChatClient"/> safety net that estimates cumulative context
/// size across LLM calls and emits a warning when approaching a configurable limit.
/// Optionally prunes oldest non-system messages to keep context under the limit.
/// </summary>
/// <remarks>
/// <para>
/// This middleware is a safety net for <c>FunctionInvokingChatClient</c> (FIC) usage
/// where conversation history accumulates. It does NOT replace the iterative loop
/// pattern — prefer <see cref="NexusLabs.Needlr.AgentFramework.Iterative.IIterativeAgentLoop"/>
/// for tool-heavy stages. Use this middleware on stages that remain FIC-based as a
/// guard against context window overflow.
/// </para>
/// <para>
/// Token estimation is approximate: each message's text content length is divided by
/// <see cref="CharsPerToken"/> (default 4) since exact tokenization requires a
/// model-specific tokenizer. This is conservative — it may trigger warnings earlier
/// than necessary, but never later.
/// </para>
/// </remarks>
public sealed class ContextWindowGuardMiddleware : DelegatingChatClient
{
    private readonly int _maxContextTokens;
    private readonly double _warningThreshold;
    private readonly bool _pruneOnOverflow;
    private readonly IProgressReporterAccessor _progressAccessor;

    /// <summary>
    /// Approximate characters per token for estimation. Defaults to 4.
    /// </summary>
    public int CharsPerToken { get; set; } = 4;

    /// <param name="innerClient">The inner chat client to delegate to.</param>
    /// <param name="maxContextTokens">
    /// Estimated maximum context window size in tokens. When the message list
    /// exceeds this, a warning is emitted and optionally oldest messages are pruned.
    /// </param>
    /// <param name="progressAccessor">Progress reporter for emitting warning events.</param>
    /// <param name="warningThreshold">
    /// Fraction of <paramref name="maxContextTokens"/> at which to emit a warning.
    /// Defaults to <c>0.8</c> (80%).
    /// </param>
    /// <param name="pruneOnOverflow">
    /// When <see langword="true"/>, automatically removes oldest non-system messages
    /// to keep estimated context under <paramref name="maxContextTokens"/>.
    /// Defaults to <see langword="false"/> (warn only).
    /// </param>
    public ContextWindowGuardMiddleware(
        IChatClient innerClient,
        int maxContextTokens,
        IProgressReporterAccessor progressAccessor,
        double warningThreshold = 0.8,
        bool pruneOnOverflow = false)
        : base(innerClient)
    {
        ArgumentNullException.ThrowIfNull(progressAccessor);
        _maxContextTokens = maxContextTokens;
        _warningThreshold = warningThreshold;
        _pruneOnOverflow = pruneOnOverflow;
        _progressAccessor = progressAccessor;
    }

    /// <inheritdoc />
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var messageList = messages as List<ChatMessage> ?? [.. messages];
        var estimatedTokens = EstimateTokenCount(messageList);
        var reporter = _progressAccessor.Current;

        var warningLimit = (long)(_maxContextTokens * _warningThreshold);

        if (estimatedTokens > _maxContextTokens)
        {
            reporter.Report(new BudgetExceededEvent(
                Timestamp: DateTimeOffset.UtcNow,
                WorkflowId: reporter.WorkflowId,
                AgentId: reporter.AgentId,
                ParentAgentId: null,
                Depth: reporter.Depth,
                SequenceNumber: reporter.NextSequence(),
                LimitType: "context_window",
                CurrentValue: estimatedTokens,
                MaxValue: _maxContextTokens));

            if (_pruneOnOverflow)
            {
                PruneMessages(messageList, estimatedTokens);
            }
        }
        else if (estimatedTokens >= warningLimit)
        {
            reporter.Report(new BudgetUpdatedEvent(
                Timestamp: DateTimeOffset.UtcNow,
                WorkflowId: reporter.WorkflowId,
                AgentId: reporter.AgentId,
                ParentAgentId: null,
                Depth: reporter.Depth,
                SequenceNumber: reporter.NextSequence(),
                CurrentInputTokens: estimatedTokens,
                CurrentOutputTokens: 0,
                CurrentTotalTokens: estimatedTokens,
                MaxInputTokens: _maxContextTokens,
                MaxOutputTokens: null,
                MaxTotalTokens: _maxContextTokens));
        }

        return await base.GetResponseAsync(messageList, options, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messageList = messages as List<ChatMessage> ?? [.. messages];
        var estimatedTokens = EstimateTokenCount(messageList);
        var reporter = _progressAccessor.Current;

        if (estimatedTokens > _maxContextTokens)
        {
            reporter.Report(new BudgetExceededEvent(
                Timestamp: DateTimeOffset.UtcNow,
                WorkflowId: reporter.WorkflowId,
                AgentId: reporter.AgentId,
                ParentAgentId: null,
                Depth: reporter.Depth,
                SequenceNumber: reporter.NextSequence(),
                LimitType: "context_window",
                CurrentValue: estimatedTokens,
                MaxValue: _maxContextTokens));

            if (_pruneOnOverflow)
            {
                PruneMessages(messageList, estimatedTokens);
            }
        }

        await foreach (var update in base.GetStreamingResponseAsync(messageList, options, cancellationToken)
            .ConfigureAwait(false))
        {
            yield return update;
        }
    }

    private long EstimateTokenCount(IEnumerable<ChatMessage> messages)
    {
        long totalChars = 0;
        foreach (var msg in messages)
        {
            foreach (var content in msg.Contents)
            {
                if (content is TextContent tc && tc.Text is { } text)
                {
                    totalChars += text.Length;
                }
                else if (content is FunctionCallContent fc)
                {
                    totalChars += fc.Name.Length + 50;
                    if (fc.Arguments is { } args)
                    {
                        foreach (var (_, value) in args)
                        {
                            totalChars += value?.ToString()?.Length ?? 0;
                        }
                    }
                }
                else if (content is FunctionResultContent fr)
                {
                    totalChars += ToolResultSerializer.Serialize(fr.Result).Length;
                }
            }
        }

        return totalChars / CharsPerToken;
    }

    private void PruneMessages(List<ChatMessage> messages, long currentTokens)
    {
        // Remove oldest non-system messages until under the limit.
        // Never remove the system message (index 0) or the last user message.
        while (currentTokens > _maxContextTokens && messages.Count > 2)
        {
            var idx = messages[0].Role == ChatRole.System ? 1 : 0;
            if (idx >= messages.Count - 1) break;

            var removed = messages[idx];
            long removedTokens = 0;
            foreach (var content in removed.Contents)
            {
                if (content is TextContent tc && tc.Text is { } text)
                    removedTokens += text.Length / CharsPerToken;
                else if (content is FunctionCallContent fc)
                {
                    removedTokens += (fc.Name.Length + 50) / CharsPerToken;
                    if (fc.Arguments is { } args)
                    {
                        foreach (var (_, value) in args)
                            removedTokens += (value?.ToString()?.Length ?? 0) / CharsPerToken;
                    }
                }
                else if (content is FunctionResultContent fr)
                    removedTokens += ToolResultSerializer.Serialize(fr.Result).Length / CharsPerToken;
            }

            messages.RemoveAt(idx);
            currentTokens -= removedTokens;
        }
    }
}
