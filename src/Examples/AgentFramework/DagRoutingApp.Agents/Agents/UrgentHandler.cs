using NexusLabs.Needlr.AgentFramework;

namespace DagRoutingApp.Agents;

/// <summary>
/// Handles urgent requests in the "priority-routing" graph. Only executes
/// when the <see cref="TriageAgent.IsUrgent"/> condition matches first.
/// </summary>
[NeedlrAiAgent(
    Description = "Handles urgent, high-priority requests.",
    Instructions = """
        You are an urgent request handler. The request has been flagged as
        urgent. Acknowledge the urgency and provide a rapid response.
        Keep your response under 100 words.
        """,
    FunctionTypes = new Type[0])]
public partial class UrgentHandler { }
