using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Provides context to an <see cref="IWorkflowTerminationCondition"/> when evaluating whether a
/// workflow should stop after an agent's response.
/// </summary>
public sealed class TerminationContext
{
    /// <summary>Gets the executor ID of the agent that produced this response.</summary>
    public required string AgentId { get; init; }

    /// <summary>
    /// Gets the last <see cref="ChatMessage"/> emitted by the agent for this turn
    /// (preserving full content including function calls, role, and metadata), or
    /// <see langword="null"/> if the agent produced no message. Use the <c>.Text</c>
    /// property on the message for a flat text view.
    /// </summary>
    public ChatMessage? LastMessage { get; init; }

    /// <summary>Gets the number of agent turns completed so far (1-based).</summary>
    public required int TurnCount { get; init; }

    /// <summary>
    /// Gets the accumulated conversation history up to and including this turn.
    /// Each entry corresponds to one completed agent response.
    /// </summary>
    public required IReadOnlyList<ChatMessage> ConversationHistory { get; init; }

    /// <summary>
    /// Gets token usage for this turn, if reported by the model. May be <see langword="null"/>
    /// when the model does not return usage metadata.
    /// </summary>
    public UsageDetails? Usage { get; init; }

    /// <summary>
    /// Gets the names of tools/functions called by the agent during this turn.
    /// Extracted from <see cref="FunctionCallContent"/> entries in the last message's
    /// <see cref="ChatMessage.Contents"/>. Empty if no tool calls were made.
    /// </summary>
    public IReadOnlyList<string> ToolCallNames { get; init; } = [];
}
