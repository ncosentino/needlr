using NexusLabs.Needlr.AgentFramework;

namespace DagRoutingApp.Agents;

/// <summary>
/// Entry point for the "fast-wins" graph. Fans out to both
/// <see cref="FastWorker"/> and <see cref="SlowWorker"/>; the
/// <see cref="ResultAgent"/> downstream uses <see cref="GraphJoinMode.WaitAny"/>
/// to proceed as soon as the first worker finishes.
/// </summary>
[NeedlrAiAgent(
    Description = "Dispatches work to competing parallel workers.",
    Instructions = """
        You are a dispatch coordinator. Forward the request to parallel
        workers. Provide a brief summary of what is being dispatched.
        Keep your response under 50 words.
        """,
    FunctionTypes = new Type[0])]
[AgentGraphEntry("fast-wins")]
[AgentGraphEdge("fast-wins", typeof(FastWorker))]
[AgentGraphEdge("fast-wins", typeof(SlowWorker))]
public partial class DispatchAgent { }
