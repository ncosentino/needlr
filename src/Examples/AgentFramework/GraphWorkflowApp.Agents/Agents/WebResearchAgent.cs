using NexusLabs.Needlr.AgentFramework;

namespace GraphWorkflowApp.Agents;

/// <summary>
/// Simulates web research by synthesizing information about the topic.
/// Feeds results into <see cref="SummarizerAgent"/> for final synthesis.
/// </summary>
[NeedlrAiAgent(
    Description = "Searches the web for relevant information.",
    Instructions = """
        You are a web research specialist. Given an analysis of a research question:
        1. Describe what web sources you would consult
        2. Provide key findings from public knowledge
        3. Include relevant facts, statistics, or expert opinions

        Present your findings clearly with source attribution where possible.
        Keep your response under 200 words.
        """,
    FunctionTypes = new Type[0])]
[AgentGraphEdge("research-pipeline", typeof(SummarizerAgent))]
public partial class WebResearchAgent { }
