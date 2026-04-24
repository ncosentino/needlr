using NexusLabs.Needlr.AgentFramework;

namespace DagRoutingApp.Agents;

/// <summary>
/// Fast-completing worker in the "fast-wins" graph. Has short instructions
/// so the LLM produces a quick response, racing against <see cref="SlowWorker"/>.
/// </summary>
[NeedlrAiAgent(
    Description = "Fast worker that provides a quick answer.",
    Instructions = """
        Reply in one sentence. Be extremely brief.
        """,
    FunctionTypes = new Type[0])]
[AgentGraphEdge("fast-wins", typeof(ResultAgent))]
public partial class FastWorker { }
