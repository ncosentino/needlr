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
        You are a request classifier. Determine whether the request is
        technical or creative in nature and explain your reasoning briefly.
        """,
    FunctionTypes = new Type[0])]
[AgentGraphEntry("exclusive-routing", RoutingMode = GraphRoutingMode.ExclusiveChoice)]
[AgentGraphEdge("exclusive-routing", typeof(TechnicalAgent), Condition = nameof(IsTechnical))]
[AgentGraphEdge("exclusive-routing", typeof(CreativeAgent), Condition = nameof(IsCreative))]
public partial class ClassifierAgent
{
    /// <summary>Returns <see langword="true"/> when the upstream output signals a technical request.</summary>
    public static bool IsTechnical(object? input)
    {
        var text = input?.ToString() ?? "";
        return text.Contains("TECHNICAL", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("database", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("optimize", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("code", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("engineering", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Returns <see langword="true"/> when the upstream output signals a creative request.</summary>
    public static bool IsCreative(object? input)
    {
        var text = input?.ToString() ?? "";
        return text.Contains("CREATIVE", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("design", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("logo", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("brand", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("artistic", StringComparison.OrdinalIgnoreCase);
    }
}
