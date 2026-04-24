using NexusLabs.Needlr.AgentFramework;

namespace DagRoutingApp.Agents;

/// <summary>
/// Sink node for the "fast-wins" graph. Uses <see cref="GraphJoinMode.WaitAny"/>
/// so it executes as soon as the first upstream worker completes, demonstrating
/// race-condition semantics.
/// </summary>
[NeedlrAiAgent(
    Description = "Collects the first available result from competing workers.",
    Instructions = """
        You received the result from the fastest worker. Summarize what
        you received and confirm which branch completed first.
        Keep your response under 100 words.
        """,
    FunctionTypes = new Type[0])]
[AgentGraphNode("fast-wins", JoinMode = GraphJoinMode.WaitAny)]
public partial class ResultAgent { }
