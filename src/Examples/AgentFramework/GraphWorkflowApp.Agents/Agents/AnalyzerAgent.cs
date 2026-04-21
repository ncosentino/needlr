using NexusLabs.Needlr.AgentFramework;

namespace GraphWorkflowApp.Agents;

/// <summary>
/// Entry point for the research-pipeline graph. Analyzes the user's question
/// and fans out to both <see cref="WebResearchAgent"/> and <see cref="DatabaseAgent"/>.
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
[AgentGraphEntry("research-pipeline", MaxSupersteps = 15, RoutingMode = GraphRoutingMode.AllMatching)]
[AgentGraphEdge("research-pipeline", typeof(WebResearchAgent))]
[AgentGraphEdge("research-pipeline", typeof(DatabaseAgent))]
public partial class AnalyzerAgent { }
