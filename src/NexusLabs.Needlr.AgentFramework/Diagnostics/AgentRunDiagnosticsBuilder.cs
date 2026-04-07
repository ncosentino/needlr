using System.Collections.Concurrent;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Thread-safe accumulator for diagnostics captured during a single agent run.
/// Used internally by the diagnostics middleware layers.
/// </summary>
/// <remarks>
/// <para>
/// Stored in an <see cref="AsyncLocal{T}"/> so the agent-run, chat-completion, and
/// function-calling middleware all access the same builder instance within an async flow.
/// </para>
/// <para>
/// Sequence numbers are reserved BEFORE async work begins (via <see cref="Interlocked.Increment(ref int)"/>),
/// ensuring parallel tool calls are ordered by invocation time, not completion time.
/// </para>
/// </remarks>
internal sealed class AgentRunDiagnosticsBuilder
{
    private static readonly AsyncLocal<AgentRunDiagnosticsBuilder?> CurrentBuilder = new();

    private readonly ConcurrentQueue<ChatCompletionDiagnostics> _chatCompletions = new();
    private readonly ConcurrentQueue<ToolCallDiagnostics> _toolCalls = new();

    private int _nextChatCompletionSequence;
    private int _nextToolCallSequence;

    private long _totalInputTokens;
    private long _totalOutputTokens;
    private long _totalTokens;
    private long _cachedInputTokens;
    private long _reasoningTokens;

    private int _totalInputMessages;
    private int _totalOutputMessages;
    private bool _succeeded = true;
    private string? _errorMessage;

    public string AgentName { get; }

    public DateTimeOffset StartedAt { get; }

    private AgentRunDiagnosticsBuilder(string agentName)
    {
        AgentName = agentName;
        StartedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Creates a new builder and stores it in the current async flow so middleware can access it.
    /// </summary>
    internal static AgentRunDiagnosticsBuilder StartNew(string agentName)
    {
        var builder = new AgentRunDiagnosticsBuilder(agentName);
        CurrentBuilder.Value = builder;
        return builder;
    }

    /// <summary>Gets the builder for the current async flow, or <see langword="null"/> if outside a run.</summary>
    internal static AgentRunDiagnosticsBuilder? GetCurrent() => CurrentBuilder.Value;

    /// <summary>Reserves a sequence number for a tool call (thread-safe).</summary>
    internal int NextToolCallSequence() =>
        Interlocked.Increment(ref _nextToolCallSequence) - 1;

    /// <summary>Reserves a sequence number for a chat completion (thread-safe).</summary>
    internal int NextChatCompletionSequence() =>
        Interlocked.Increment(ref _nextChatCompletionSequence) - 1;

    internal void AddChatCompletion(ChatCompletionDiagnostics diagnostics)
    {
        _chatCompletions.Enqueue(diagnostics);

        Interlocked.Add(ref _totalInputTokens, diagnostics.Tokens.InputTokens);
        Interlocked.Add(ref _totalOutputTokens, diagnostics.Tokens.OutputTokens);
        Interlocked.Add(ref _totalTokens, diagnostics.Tokens.TotalTokens);
        Interlocked.Add(ref _cachedInputTokens, diagnostics.Tokens.CachedInputTokens);
        Interlocked.Add(ref _reasoningTokens, diagnostics.Tokens.ReasoningTokens);
    }

    internal void AddToolCall(ToolCallDiagnostics diagnostics) =>
        _toolCalls.Enqueue(diagnostics);

    internal void RecordInputMessageCount(int count) =>
        Interlocked.Add(ref _totalInputMessages, count);

    internal void RecordOutputMessageCount(int count) =>
        Interlocked.Add(ref _totalOutputMessages, count);

    internal void RecordFailure(string? errorMessage)
    {
        _succeeded = false;
        _errorMessage = errorMessage;
    }

    internal IAgentRunDiagnostics Build()
    {
        var completedAt = DateTimeOffset.UtcNow;

        return new AgentRunDiagnostics(
            AgentName: AgentName,
            TotalDuration: completedAt - StartedAt,
            AggregateTokenUsage: new TokenUsage(
                InputTokens: Volatile.Read(ref _totalInputTokens),
                OutputTokens: Volatile.Read(ref _totalOutputTokens),
                TotalTokens: Volatile.Read(ref _totalTokens),
                CachedInputTokens: Volatile.Read(ref _cachedInputTokens),
                ReasoningTokens: Volatile.Read(ref _reasoningTokens)),
            ChatCompletions: _chatCompletions.OrderBy(c => c.Sequence).ToArray(),
            ToolCalls: _toolCalls.OrderBy(t => t.Sequence).ToArray(),
            TotalInputMessages: Volatile.Read(ref _totalInputMessages),
            TotalOutputMessages: Volatile.Read(ref _totalOutputMessages),
            Succeeded: _succeeded,
            ErrorMessage: _errorMessage,
            StartedAt: StartedAt,
            CompletedAt: completedAt);
    }

    /// <summary>Clears the builder from the current async flow.</summary>
    internal static void ClearCurrent() => CurrentBuilder.Value = null;
}
