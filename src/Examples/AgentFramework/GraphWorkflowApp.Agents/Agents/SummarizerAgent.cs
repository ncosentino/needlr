using NexusLabs.Needlr.AgentFramework;

namespace GraphWorkflowApp.Agents;

/// <summary>
/// Terminal node that synthesizes research from both
/// <see cref="WebResearchAgent"/> and <see cref="DatabaseAgent"/> branches.
/// Uses <see cref="GraphJoinMode.WaitAll"/> to ensure both branches complete.
/// </summary>
[NeedlrAiAgent(
    Description = "Synthesizes research findings into a final report.",
    Instructions = """
        You are a research synthesizer. You receive findings from multiple research branches
        (web research and database queries). Your job is to:
        1. Combine all findings into a coherent summary
        2. Highlight key insights and any contradictions
        3. Provide a final conclusion

        Be concise but comprehensive. Present a well-structured final report.
        """,
    FunctionTypes = new Type[0])]
[AgentGraphNode("research-pipeline", JoinMode = GraphJoinMode.WaitAll)]
public partial class SummarizerAgent { }
