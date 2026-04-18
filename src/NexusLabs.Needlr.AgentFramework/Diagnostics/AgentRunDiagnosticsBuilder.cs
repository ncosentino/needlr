using System.Collections.Concurrent;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Thread-safe accumulator for diagnostics captured during a single agent run.
/// Both Needlr's built-in diagnostics middleware and consumer-provided middleware
/// use this class to record tool calls, chat completions, and message counts as
/// an agent executes.
/// </summary>
/// <remarks>
/// <para>
/// Stored in an <see cref="AsyncLocal{T}"/> so the agent-run, chat-completion, and
/// function-calling middleware all access the same builder instance within an async flow.
/// Call <see cref="StartNew"/> at the beginning of an agent run and dispose the returned
/// builder when the run completes.
/// </para>
/// <para>
/// Sequence numbers are reserved BEFORE async work begins (via <see cref="Interlocked.Increment(ref int)"/>),
/// ensuring parallel tool calls are ordered by invocation time, not completion time.
/// </para>
/// </remarks>
public sealed class AgentRunDiagnosticsBuilder : IDisposable
{
    private static readonly AsyncLocal<AgentRunDiagnosticsBuilder?> CurrentBuilder = new();

    private readonly ConcurrentQueue<ChatCompletionDiagnostics> _chatCompletions = new();
    private readonly ConcurrentQueue<ToolCallDiagnostics> _toolCalls = new();
    private readonly AgentRunDiagnosticsBuilder? _previousBuilder;

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
    private string? _executionMode;
    private IReadOnlyList<ChatMessage>? _inputMessages;
    private AgentResponse? _outputResponse;

    public string AgentName { get; }

    /// <summary>
    /// Gets the name of the parent (outer) agent when this builder was created
    /// inside a nested sub-agent run, or <see langword="null"/> if this is a
    /// top-level agent.
    /// </summary>
    public string? ParentAgentName => _previousBuilder?.AgentName;

    public DateTimeOffset StartedAt { get; }

    private AgentRunDiagnosticsBuilder(string agentName, AgentRunDiagnosticsBuilder? previous)
    {
        AgentName = agentName;
        StartedAt = DateTimeOffset.UtcNow;
        _previousBuilder = previous;
    }

    /// <summary>
    /// Creates a new builder and stores it in the current async flow so middleware can access it.
    /// If a builder already exists (nested sub-agent scenario), the previous builder is saved
    /// and restored when this builder is disposed.
    /// </summary>
    public static AgentRunDiagnosticsBuilder StartNew(string agentName)
    {
        var previous = CurrentBuilder.Value;
        var builder = new AgentRunDiagnosticsBuilder(agentName, previous);
        CurrentBuilder.Value = builder;
        return builder;
    }

    /// <summary>Gets the builder for the current async flow, or <see langword="null"/> if outside a run.</summary>
    public static AgentRunDiagnosticsBuilder? GetCurrent() => CurrentBuilder.Value;

    /// <summary>Reserves a sequence number for a tool call (thread-safe).</summary>
    public int NextToolCallSequence() =>
        Interlocked.Increment(ref _nextToolCallSequence) - 1;

    /// <summary>Reserves a sequence number for a chat completion (thread-safe).</summary>
    public int NextChatCompletionSequence() =>
        Interlocked.Increment(ref _nextChatCompletionSequence) - 1;

    public void AddChatCompletion(ChatCompletionDiagnostics diagnostics)
    {
        _chatCompletions.Enqueue(diagnostics);

        Interlocked.Add(ref _totalInputTokens, diagnostics.Tokens.InputTokens);
        Interlocked.Add(ref _totalOutputTokens, diagnostics.Tokens.OutputTokens);
        Interlocked.Add(ref _totalTokens, diagnostics.Tokens.TotalTokens);
        Interlocked.Add(ref _cachedInputTokens, diagnostics.Tokens.CachedInputTokens);
        Interlocked.Add(ref _reasoningTokens, diagnostics.Tokens.ReasoningTokens);
    }

    public void AddToolCall(ToolCallDiagnostics diagnostics) =>
        _toolCalls.Enqueue(diagnostics);

    public void RecordInputMessageCount(int count) =>
        Interlocked.Add(ref _totalInputMessages, count);

    public void RecordOutputMessageCount(int count) =>
        Interlocked.Add(ref _totalOutputMessages, count);

    /// <summary>
    /// Records the full input messages supplied to the agent for this run. Captured
    /// once at the agent-run boundary; calling more than once replaces the snapshot.
    /// </summary>
    public void RecordInputMessages(IReadOnlyList<ChatMessage> messages) =>
        Volatile.Write(ref _inputMessages, messages);

    /// <summary>
    /// Records the aggregated output response produced by the agent for this run.
    /// Pass <see langword="null"/> when no response was produced.
    /// </summary>
    public void RecordOutputResponse(AgentResponse? response) =>
        Volatile.Write(ref _outputResponse, response);

    public void RecordFailure(string? errorMessage)
    {
        _succeeded = false;
        _errorMessage = errorMessage;
    }

    /// <summary>
    /// Sets the execution mode label for these diagnostics.
    /// Known values: <c>"FunctionInvokingChatClient"</c>, <c>"IterativeLoop"</c>.
    /// </summary>
    public void SetExecutionMode(string executionMode) =>
        _executionMode = executionMode;

    public IAgentRunDiagnostics Build()
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
            InputMessages: Volatile.Read(ref _inputMessages) ?? Array.Empty<ChatMessage>(),
            OutputResponse: Volatile.Read(ref _outputResponse),
            Succeeded: _succeeded,
            ErrorMessage: _errorMessage,
            StartedAt: StartedAt,
            CompletedAt: completedAt,
            ExecutionMode: _executionMode);
    }

    /// <summary>Clears the builder from the current async flow.</summary>
    public static void ClearCurrent() => CurrentBuilder.Value = null;

    /// <summary>
    /// Restores the previous builder (if any) to the current async flow. If this builder
    /// was created inside a nested sub-agent run, the outer agent's builder is restored.
    /// Otherwise equivalent to <see cref="ClearCurrent"/>.
    /// </summary>
    public void Dispose() => CurrentBuilder.Value = _previousBuilder;
}
