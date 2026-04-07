namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Result of a single agent stage within a pipeline, combining the response text
/// with the captured diagnostics for that stage.
/// </summary>
public interface IAgentStageResult
{
    /// <summary>Gets the agent's executor ID (agent name or unique identifier).</summary>
    string AgentName { get; }

    /// <summary>Gets the agent's response text.</summary>
    string ResponseText { get; }

    /// <summary>
    /// Gets the diagnostics captured during this agent's execution, or <see langword="null"/>
    /// if diagnostics were not enabled.
    /// </summary>
    IAgentRunDiagnostics? Diagnostics { get; }
}
