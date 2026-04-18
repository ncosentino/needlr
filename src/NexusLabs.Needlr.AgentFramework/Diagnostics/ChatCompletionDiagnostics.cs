using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Diagnostics for a single LLM chat completion call within an agent run.
/// </summary>
/// <remarks>
/// <para>
/// Each time the agent calls the underlying <c>IChatClient.GetResponseAsync</c>, the
/// diagnostics chat client middleware captures timing, token usage, and model metadata
/// into one of these records. An agent that makes multiple LLM calls (e.g., a tool-call
/// loop) produces multiple <see cref="ChatCompletionDiagnostics"/> entries within a
/// single <see cref="IAgentRunDiagnostics"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// foreach (var call in diagnostics.ChatCompletions)
/// {
///     var fresh = call.Tokens.InputTokens - call.Tokens.CachedInputTokens;
///     Console.WriteLine($"[{call.Model}] in:{call.Tokens.InputTokens} " +
///         $"(cached:{call.Tokens.CachedInputTokens} fresh:{fresh}) " +
///         $"out:{call.Tokens.OutputTokens} | {call.Duration.TotalMilliseconds}ms");
/// }
/// </code>
/// </example>
/// <param name="Sequence">Zero-based invocation order within the agent run.</param>
/// <param name="Model">The model identifier returned by the LLM provider (e.g., <c>"gpt-4o"</c>, <c>"claude-sonnet-4-20250514"</c>).</param>
/// <param name="Tokens">Token usage breakdown for this single call.</param>
/// <param name="InputMessageCount">Number of <c>ChatMessage</c> entries sent to the model.</param>
/// <param name="Duration">Wall-clock time for the API call.</param>
/// <param name="Succeeded">Whether the call returned without throwing.</param>
/// <param name="ErrorMessage">The exception message if the call failed; <see langword="null"/> on success.</param>
/// <param name="StartedAt">UTC timestamp when the API call began.</param>
/// <param name="CompletedAt">UTC timestamp when the API call finished.</param>
public sealed record ChatCompletionDiagnostics(
    int Sequence,
    string Model,
    TokenUsage Tokens,
    int InputMessageCount,
    TimeSpan Duration,
    bool Succeeded,
    string? ErrorMessage,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt)
{
    /// <summary>
    /// The name of the agent that triggered this completion, or <see langword="null"/>
    /// if the agent name was not available. Used to attribute completions to the
    /// correct stage in group chat workflows where multiple agents share a single
    /// chat client.
    /// </summary>
    public string? AgentName { get; init; }

    /// <summary>
    /// The full list of <see cref="ChatMessage"/> instances sent to the model for this
    /// completion. Captured losslessly to enable post-hoc replay and evaluation
    /// without re-invoking the agent. <see langword="null"/> if capture was unavailable
    /// (e.g., on call failure before messages were materialized).
    /// </summary>
    /// <remarks>
    /// Populated on success. This is the input side of the call and is directly
    /// consumable by <c>Microsoft.Extensions.AI.Evaluation</c> evaluators.
    /// </remarks>
    public IReadOnlyList<ChatMessage>? RequestMessages { get; init; }

    /// <summary>
    /// The full <see cref="ChatResponse"/> returned by the model for this completion,
    /// or <see langword="null"/> if the call failed or the response was not captured.
    /// Captured losslessly to enable post-hoc replay and evaluation.
    /// </summary>
    public ChatResponse? Response { get; init; }

    /// <summary>
    /// Total character count of the text content across every message in
    /// <see cref="RequestMessages"/>. Complements token-based metrics with a
    /// direct programmatic measure of payload size. Defaults to <c>0</c>
    /// when not populated by the capture middleware.
    /// </summary>
    public long RequestCharCount { get; init; }

    /// <summary>
    /// Total character count of the text content across every message in
    /// <see cref="Response"/>. Defaults to <c>0</c> when not populated by
    /// the capture middleware.
    /// </summary>
    public long ResponseCharCount { get; init; }
}
