using NexusLabs.Needlr.AgentFramework;

namespace DagRoutingApp.Agents;

/// <summary>
/// Entry point for the "priority-routing" graph. Uses
/// <see cref="GraphRoutingMode.FirstMatching"/> to evaluate conditions in
/// declaration order and follow only the first matching edge.
/// </summary>
[NeedlrAiAgent(
    Description = "Triages incoming requests by urgency level.",
    Instructions = """
        You are a triage coordinator. Analyze the incoming message and
        categorize it by urgency. Provide a brief assessment of the request.
        """,
    FunctionTypes = new Type[0])]
[AgentGraphEntry("priority-routing", RoutingMode = GraphRoutingMode.FirstMatching)]
[AgentGraphEdge("priority-routing", typeof(UrgentHandler), Condition = nameof(IsUrgent))]
[AgentGraphEdge("priority-routing", typeof(RoutineHandler), Condition = nameof(IsRoutine))]
[AgentGraphEdge("priority-routing", typeof(FallbackHandler))]
public partial class TriageAgent
{
    /// <summary>Returns <see langword="true"/> when the input contains "urgent".</summary>
    public static bool IsUrgent(object? input) =>
        input?.ToString()?.Contains("urgent", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>Returns <see langword="true"/> when the input contains "routine".</summary>
    public static bool IsRoutine(object? input) =>
        input?.ToString()?.Contains("routine", StringComparison.OrdinalIgnoreCase) == true;
}
