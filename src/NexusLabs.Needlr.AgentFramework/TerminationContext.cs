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

    /// <summary>Gets the complete response text emitted by the agent for this turn.</summary>
    public required string ResponseText { get; init; }

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
}
