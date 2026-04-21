using NexusLabs.Needlr.AgentFramework;

namespace GraphWorkflowApp.Agents;

/// <summary>
/// Simulates internal database lookups for structured data.
/// Feeds results into <see cref="SummarizerAgent"/> for final synthesis.
/// </summary>
[NeedlrAiAgent(
    Description = "Queries internal databases for structured data.",
    Instructions = """
        You are a database research specialist. Given an analysis of a research question:
        1. Describe what internal data sources you would query
        2. Provide structured data points relevant to the question
        3. Present findings in a tabular or bullet-point format

        Focus on quantitative data and structured facts.
        Keep your response under 200 words.
        """,
    FunctionTypes = new Type[0])]
[AgentGraphEdge("research-pipeline", typeof(SummarizerAgent))]
public partial class DatabaseAgent { }
