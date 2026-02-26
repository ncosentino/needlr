namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Evaluates whether a workflow should terminate early after an agent response.
/// </summary>
/// <remarks>
/// Implement this interface to provide custom termination logic. Built-in implementations are
/// available in the <c>NexusLabs.Needlr.AgentFramework.Workflows</c> package:
/// <list type="bullet">
/// <item><c>KeywordTerminationCondition</c> — stops when a response contains a keyword.</item>
/// <item><c>RegexTerminationCondition</c> — stops when a response matches a regex.</item>
/// </list>
/// <para>
/// Conditions are declared on agent classes via
/// <see cref="AgentTerminationConditionAttribute"/> (Layer 1: group chat, fires before the next
/// turn) or <see cref="WorkflowRunTerminationConditionAttribute"/> (Layer 2: any workflow type,
/// fires via <c>RunAsync</c>).
/// </para>
/// </remarks>
public interface IWorkflowTerminationCondition
{
    /// <summary>
    /// Determines whether the workflow should terminate after the given agent response.
    /// </summary>
    /// <param name="context">Context for the completed agent turn.</param>
    /// <returns>
    /// <see langword="true"/> to stop the workflow; <see langword="false"/> to continue.
    /// </returns>
    bool ShouldTerminate(TerminationContext context);
}
