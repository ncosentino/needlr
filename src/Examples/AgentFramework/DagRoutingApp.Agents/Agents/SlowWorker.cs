using NexusLabs.Needlr.AgentFramework;

namespace DagRoutingApp.Agents;

/// <summary>
/// Slow-completing worker in the "fast-wins" graph. Has longer instructions
/// requesting detailed analysis, so the LLM takes more time than
/// <see cref="FastWorker"/>.
/// </summary>
[NeedlrAiAgent(
    Description = "Thorough worker that provides detailed analysis.",
    Instructions = """
        You are a thorough analyst. Provide a detailed, multi-paragraph
        analysis of the request. Consider multiple perspectives, weigh
        pros and cons, cite relevant background knowledge, and structure
        your response with clear headings. Be comprehensive and aim for
        at least 300 words in your response.
        """,
    FunctionTypes = new Type[0])]
[AgentGraphEdge("fast-wins", typeof(ResultAgent))]
public partial class SlowWorker { }
