namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Accepts diagnostic records produced during an agent run. Writers (middleware,
/// orchestration loops) call these methods to record individual chat completions
/// and tool calls. The sink is responsible for storage and aggregation.
/// </summary>
/// <remarks>
/// <para>
/// Each event class (chat completion, tool call) should have exactly one writer.
/// Two writers recording the same logical event is a bug — the sink does not
/// deduplicate. Use the single-writer middleware pattern to prevent this.
/// </para>
/// <para>
/// Today's concrete implementation is <see cref="AgentRunDiagnosticsBuilder"/>.
/// Future implementations may fan out to multiple storage backends (in-memory,
/// file, progress reporter).
/// </para>
/// </remarks>
public interface IDiagnosticsSink
{
    /// <summary>Reserves a sequence number for a chat completion (thread-safe).</summary>
    int NextChatCompletionSequence();

    /// <summary>Reserves a sequence number for a tool call (thread-safe).</summary>
    int NextToolCallSequence();

    /// <summary>Records a completed chat completion call.</summary>
    void AddChatCompletion(ChatCompletionDiagnostics diagnostics);

    /// <summary>Records a completed tool call.</summary>
    void AddToolCall(ToolCallDiagnostics diagnostics);

    /// <summary>
    /// Gets the name of the agent associated with this sink, or
    /// <see langword="null"/> if unknown. Writers use this to attribute
    /// diagnostic records to the correct agent.
    /// </summary>
    string? AgentName { get; }
}
