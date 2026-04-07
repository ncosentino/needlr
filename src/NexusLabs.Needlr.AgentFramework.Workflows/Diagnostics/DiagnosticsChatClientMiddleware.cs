using System.Diagnostics;

using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;

/// <summary>
/// Middle middleware layer: wraps each <c>IChatClient.GetResponseAsync()</c> call to capture
/// per-completion timing and token usage. Sequence is reserved BEFORE the async call so
/// parallel completions are ordered by invocation time, not completion time.
/// </summary>
internal static class DiagnosticsChatClientMiddleware
{
    internal static async Task<ChatResponse> HandleAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        IChatClient innerChatClient,
        CancellationToken cancellationToken)
    {
        var builder = AgentRunDiagnosticsBuilder.GetCurrent();
        var sequence = builder?.NextChatCompletionSequence() ?? -1;
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await innerChatClient.GetResponseAsync(messages, options, cancellationToken)
                .ConfigureAwait(false);

            stopwatch.Stop();

            var usage = response.Usage;
            var tokens = new TokenUsage(
                InputTokens: usage?.InputTokenCount ?? 0,
                OutputTokens: usage?.OutputTokenCount ?? 0,
                TotalTokens: usage?.TotalTokenCount ?? 0,
                CachedInputTokens: usage?.AdditionalCounts?.GetValueOrDefault("CachedInputTokens") ?? 0,
                ReasoningTokens: usage?.AdditionalCounts?.GetValueOrDefault("ReasoningTokens") ?? 0);

            var messageList = messages as ICollection<ChatMessage> ?? messages.ToList();

            builder?.AddChatCompletion(new ChatCompletionDiagnostics(
                Sequence: sequence,
                Model: response.ModelId ?? "unknown",
                Tokens: tokens,
                InputMessageCount: messageList.Count,
                Duration: stopwatch.Elapsed,
                Succeeded: true,
                ErrorMessage: null,
                StartedAt: startedAt,
                CompletedAt: DateTimeOffset.UtcNow));

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            builder?.AddChatCompletion(new ChatCompletionDiagnostics(
                Sequence: sequence,
                Model: "unknown",
                Tokens: new TokenUsage(0, 0, 0, 0, 0),
                InputMessageCount: 0,
                Duration: stopwatch.Elapsed,
                Succeeded: false,
                ErrorMessage: ex.Message,
                StartedAt: startedAt,
                CompletedAt: DateTimeOffset.UtcNow));

            throw;
        }
    }
}
