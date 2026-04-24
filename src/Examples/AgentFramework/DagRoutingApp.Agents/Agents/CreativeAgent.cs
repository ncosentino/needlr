using NexusLabs.Needlr.AgentFramework;

namespace DagRoutingApp.Agents;

/// <summary>
/// Handles creative requests in the "exclusive-routing" graph. Only
/// executes when <see cref="ClassifierAgent.IsCreative"/> matches exclusively.
/// </summary>
[NeedlrAiAgent(
    Description = "Handles creative, design-oriented requests.",
    Instructions = """
        You are a creative specialist. The request has been classified as
        creative. Provide an imaginative, design-focused response.
        Keep your response under 100 words.
        """,
    FunctionTypes = new Type[0])]
public partial class CreativeAgent { }
