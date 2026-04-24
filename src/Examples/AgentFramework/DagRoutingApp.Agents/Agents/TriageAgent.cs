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
        You are a triage coordinator. Analyze the incoming request and
        provide a brief urgency assessment in one or two sentences.
        """,
    FunctionTypes = new Type[0])]
[AgentGraphEntry("priority-routing", RoutingMode = GraphRoutingMode.FirstMatching)]
[AgentGraphEdge("priority-routing", typeof(UrgentHandler), Condition = nameof(IsUrgent))]
[AgentGraphEdge("priority-routing", typeof(RoutineHandler), Condition = nameof(IsRoutine))]
[AgentGraphEdge("priority-routing", typeof(FallbackHandler))]
public partial class TriageAgent
{
    /// <summary>Returns <see langword="true"/> when the upstream output signals urgency.</summary>
    public static bool IsUrgent(object? input)
    {
        var text = input?.ToString() ?? "";
        return text.Contains("URGENT", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("critical", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("emergency", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("server is down", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Returns <see langword="true"/> when the upstream output signals routine work.</summary>
    public static bool IsRoutine(object? input)
    {
        var text = input?.ToString() ?? "";
        return text.Contains("ROUTINE", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("weekly", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("status update", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("regular", StringComparison.OrdinalIgnoreCase);
    }
}
