using NexusLabs.Needlr.AgentFramework;

namespace GeneratorCoexistenceApp;

/// <summary>
/// Needlr-side: <see cref="NeedlrAiAgentAttribute"/> declares this agent.
/// Needlr's source generator emits it into the <c>AgentRegistry</c>, generates
/// a partial companion with topology metadata, and wires function groups.
/// </summary>
[NeedlrAiAgent(
    Description = "A data lookup assistant that answers questions using the data store.",
    Instructions = "You are a helpful assistant. Use the data-tools to look up information.",
    FunctionGroups = new[] { "data-tools" })]
public partial class DataAssistant { }
