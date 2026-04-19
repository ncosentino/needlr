using System.Collections.Concurrent;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Thread-safe accumulator for diagnostics captured during a single agent run.
/// Implements <see cref="IDiagnosticsSink"/> so writers (middleware, loops) record
/// through a stable interface rather than coupling to the concrete builder.
/// </summary>
/// <remarks>
/// <para>
/// Stored in an <see cref="AsyncLocal{T}"/> so middleware layers access the same
/// builder instance within an async flow. Call <see cref="StartNew(string)"/> at the
/// beginning of an agent run and dispose the returned builder when the run completes.
/// </para>
/// <para>
/// Each event class has a single designated writer. Chat completions are written
/// exclusively by <see cref="DiagnosticsChatClientMiddleware"/>. Tool calls are
/// written exclusively by the <c>IterativeAgentLoop</c> (MEAI path) or
/// <c>DiagnosticsFunctionCallingMiddleware</c> (MAF path). Two writers for the same
/// event class is a bug.
/// </para>
/// <para>
/// Sequence numbers are reserved BEFORE async work begins (via
/// <see cref="Interlocked.Increment(ref int)"/>), ensuring parallel tool calls are
/// ordered by invocation time, not completion time.
/// </para>
/// </remarks>
public sealed class AgentRunDiagnosticsBuilder : IDiagnosticsSink, IDisposable
{
    private static readonly AsyncLocal<AgentRunDiagnosticsBuilder?> CurrentBuilder = new();

    private readonly ConcurrentQueue<ChatCompletionDiagnostics> _chatCompletions = new();
    private readonly ConcurrentQueue<ToolCallDiagnostics> _toolCalls = new();
    private readonly AgentRunDiagnosticsBuilder? _previousBuilder;
    private readonly IReadOnlyList<IDiagnosticsSink>? _secondarySinks;

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

    private AgentRunDiagnosticsBuilder(
        string agentName,
        AgentRunDiagnosticsBuilder? previous,
        IReadOnlyList<IDiagnosticsSink>? secondarySinks)
    {
        AgentName = agentName;
        StartedAt = DateTimeOffset.UtcNow;
        _previousBuilder = previous;
        _secondarySinks = secondarySinks;
    }

    /// <summary>
    /// Creates a new builder and stores it in the current async flow so middleware can access it.
    /// If a builder already exists (nested sub-agent scenario), the previous builder is saved
    /// and restored when this builder is disposed.
    /// </summary>
    public static AgentRunDiagnosticsBuilder StartNew(string agentName)
    {
        var previous = CurrentBuilder.Value;
        var builder = new AgentRunDiagnosticsBuilder(agentName, previous, secondarySinks: null);
        CurrentBuilder.Value = builder;
        return builder;
    }

    /// <summary>
    /// Creates a new builder with secondary sinks that receive forwarded diagnostic
    /// records. The builder remains the primary accumulator; secondary sinks receive
    /// copies of each <see cref="AddChatCompletion"/> and <see cref="AddToolCall"/>
    /// call on a best-effort basis (sink failures are swallowed).
    /// </summary>
    /// <param name="agentName">The name of the agent being traced.</param>
    /// <param name="secondarySinks">Additional sinks to fan out to, or <see langword="null"/>.</param>
    public static AgentRunDiagnosticsBuilder StartNew(
        string agentName,
        IReadOnlyList<IDiagnosticsSink>? secondarySinks)
    {
        var previous = CurrentBuilder.Value;
        var builder = new AgentRunDiagnosticsBuilder(agentName, previous, secondarySinks);
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

        ForwardToSecondarySinks(diagnostics);
    }

    public void AddToolCall(ToolCallDiagnostics diagnostics)
    {
        _toolCalls.Enqueue(diagnostics);
        ForwardToSecondarySinks(diagnostics);
    }

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

    private void ForwardToSecondarySinks(ChatCompletionDiagnostics diagnostics)
    {
        if (_secondarySinks is not { Count: > 0 })
        {
            return;
        }

        for (var i = 0; i < _secondarySinks.Count; i++)
        {
            try
            {
                _secondarySinks[i].AddChatCompletion(diagnostics);
            }
            catch
            {
                // Best-effort: secondary sink failures must not break agent execution.
            }
        }
    }

    private void ForwardToSecondarySinks(ToolCallDiagnostics diagnostics)
    {
        if (_secondarySinks is not { Count: > 0 })
        {
            return;
        }

        for (var i = 0; i < _secondarySinks.Count; i++)
        {
            try
            {
                _secondarySinks[i].AddToolCall(diagnostics);
            }
            catch
            {
                // Best-effort: secondary sink failures must not break agent execution.
            }
        }
    }
}
