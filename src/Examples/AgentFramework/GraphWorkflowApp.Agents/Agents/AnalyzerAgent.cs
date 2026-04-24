using NexusLabs.Needlr.AgentFramework;

namespace GraphWorkflowApp.Agents;

/// <summary>
/// Entry point for the research-pipeline graph. Analyzes the user's question
/// and fans out to both <see cref="WebResearchAgent"/> and <see cref="DatabaseAgent"/>.
/// The web research branch is conditional — it only executes when the input
/// mentions "web", demonstrating <see cref="AgentGraphEdgeAttribute.Condition"/>.
/// The database branch is optional — the graph succeeds even if it fails,
/// demonstrating <see cref="AgentGraphEdgeAttribute.IsRequired"/>.
/// </summary>
[NeedlrAiAgent(
    Description = "Analyzes research requests and delegates to specialists.",
    Instructions = """
        You are a research coordinator. When given a question:
        1. Identify what kind of information is needed
        2. Provide a brief analysis of the question
        3. Note what areas should be researched further

        Be concise and structured in your analysis.
        """,
    FunctionTypes = new Type[0])]
[AgentGraphEntry("research-pipeline", RoutingMode = GraphRoutingMode.AllMatching)]
[AgentGraphEdge("research-pipeline", typeof(WebResearchAgent),
    Condition = nameof(NeedsWebResearch))]
[AgentGraphEdge("research-pipeline", typeof(DatabaseAgent),
    IsRequired = false)]
public partial class AnalyzerAgent
{
    /// <summary>
    /// Condition predicate for the web research branch. Returns <see langword="true"/>
    /// when the input contains "web" (case-insensitive), causing the
    /// <see cref="WebResearchAgent"/> edge to activate.
    /// </summary>
    public static bool NeedsWebResearch(object? input)
        => input?.ToString()?.Contains("web", StringComparison.OrdinalIgnoreCase) == true;
}
