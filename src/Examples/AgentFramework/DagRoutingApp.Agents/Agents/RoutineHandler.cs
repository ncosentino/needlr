using NexusLabs.Needlr.AgentFramework;

namespace DagRoutingApp.Agents;

/// <summary>
/// Handles routine requests in the "priority-routing" graph. Only executes
/// when the <see cref="TriageAgent.IsRoutine"/> condition matches first.
/// </summary>
[NeedlrAiAgent(
    Description = "Handles routine, standard-priority requests.",
    Instructions = """
        You are a routine request handler. The request has been flagged as
        routine. Process it with standard priority and provide a thorough response.
        Keep your response under 100 words.
        """,
    FunctionTypes = new Type[0])]
public partial class RoutineHandler { }
