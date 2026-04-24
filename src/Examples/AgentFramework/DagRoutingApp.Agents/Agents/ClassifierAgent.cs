using NexusLabs.Needlr.AgentFramework;

namespace DagRoutingApp.Agents;

/// <summary>
/// Entry point for the "exclusive-routing" graph. Uses
/// <see cref="GraphRoutingMode.ExclusiveChoice"/> to enforce that exactly
/// one condition matches. Zero or multiple matches cause a runtime error.
/// </summary>
[NeedlrAiAgent(
    Description = "Classifies requests into exactly one category.",
    Instructions = """
        You are a request classifier. Read the incoming message and respond
        with ONLY ONE of these exact words on a single line — nothing else:
        TECHNICAL
        CREATIVE
        """,
    FunctionTypes = new Type[0])]
[AgentGraphEntry("exclusive-routing", RoutingMode = GraphRoutingMode.ExclusiveChoice)]
[AgentGraphEdge("exclusive-routing", typeof(TechnicalAgent), Condition = nameof(IsTechnical))]
[AgentGraphEdge("exclusive-routing", typeof(CreativeAgent), Condition = nameof(IsCreative))]
public partial class ClassifierAgent
{
    /// <summary>Returns <see langword="true"/> when the upstream output contains "TECHNICAL".</summary>
    public static bool IsTechnical(object? input) =>
        input?.ToString()?.Contains("TECHNICAL", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>Returns <see langword="true"/> when the upstream output contains "CREATIVE".</summary>
    public static bool IsCreative(object? input) =>
        input?.ToString()?.Contains("CREATIVE", StringComparison.OrdinalIgnoreCase) == true;
}
