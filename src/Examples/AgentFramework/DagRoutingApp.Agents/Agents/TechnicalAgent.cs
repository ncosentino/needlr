using NexusLabs.Needlr.AgentFramework;

namespace DagRoutingApp.Agents;

/// <summary>
/// Handles technical requests in the "exclusive-routing" graph. Only
/// executes when <see cref="ClassifierAgent.IsTechnical"/> matches exclusively.
/// </summary>
[NeedlrAiAgent(
    Description = "Handles technical, engineering-oriented requests.",
    Instructions = """
        You are a technical specialist. The request has been classified as
        technical. Provide a precise, engineering-focused response.
        Keep your response under 100 words.
        """,
    FunctionTypes = new Type[0])]
public partial class TechnicalAgent { }
