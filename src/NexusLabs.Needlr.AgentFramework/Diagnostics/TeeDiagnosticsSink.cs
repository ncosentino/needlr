namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// An <see cref="IDiagnosticsSink"/> that dispatches every record to N inner sinks.
/// Owns its own sequence counters so all sinks see consistent sequence numbers.
/// </summary>
/// <remarks>
/// <para>
/// Secondary sink failures are best-effort: if an inner sink throws, the exception
/// is swallowed and dispatch continues to remaining sinks. This ensures that a
/// broken observability sink (e.g., a file writer with a full disk) never breaks
/// production agent execution.
/// </para>
/// <para>
/// Use this when you need to fan out diagnostic records to multiple consumers —
/// for example, an in-memory builder for programmatic access plus a file-based
/// sink for post-hoc analysis.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var inMemory = AgentRunDiagnosticsBuilder.StartNew("Agent");
/// var fileSink = new MyFileDiagnosticsSink("Agent");
/// var tee = new TeeDiagnosticsSink("Agent", [inMemory, fileSink]);
/// tee.AddToolCall(toolDiag); // dispatched to both sinks
/// </code>
/// </example>
public sealed class TeeDiagnosticsSink : IDiagnosticsSink
{
    private readonly IReadOnlyList<IDiagnosticsSink> _sinks;
    private int _nextChatCompletionSequence;
    private int _nextToolCallSequence;

    /// <summary>
    /// Creates a tee-sink that dispatches to the specified inner sinks.
    /// </summary>
    /// <param name="agentName">The agent name attributed to records routed through this sink.</param>
    /// <param name="sinks">The inner sinks to dispatch to. Must not be empty.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sinks"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="sinks"/> is empty.</exception>
    public TeeDiagnosticsSink(string agentName, IReadOnlyList<IDiagnosticsSink> sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        if (sinks.Count == 0)
        {
            throw new ArgumentException("At least one inner sink is required.", nameof(sinks));
        }

        AgentName = agentName;
        _sinks = sinks;
    }

    /// <inheritdoc />
    public string? AgentName { get; }

    /// <inheritdoc />
    public int NextChatCompletionSequence() =>
        Interlocked.Increment(ref _nextChatCompletionSequence) - 1;

    /// <inheritdoc />
    public int NextToolCallSequence() =>
        Interlocked.Increment(ref _nextToolCallSequence) - 1;

    /// <inheritdoc />
    public void AddChatCompletion(ChatCompletionDiagnostics diagnostics)
    {
        for (var i = 0; i < _sinks.Count; i++)
        {
            try
            {
                _sinks[i].AddChatCompletion(diagnostics);
            }
            catch
            {
                // Best-effort: swallow failures from individual sinks so one
                // broken sink does not prevent other sinks from receiving data.
            }
        }
    }

    /// <inheritdoc />
    public void AddToolCall(ToolCallDiagnostics diagnostics)
    {
        for (var i = 0; i < _sinks.Count; i++)
        {
            try
            {
                _sinks[i].AddToolCall(diagnostics);
            }
            catch
            {
                // Best-effort: swallow failures from individual sinks.
            }
        }
    }
}
