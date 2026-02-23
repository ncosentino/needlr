using NexusLabs.Needlr.AgentFramework;

namespace SimpleAgentFrameworkApp.Agents;

/// <summary>
/// Answers questions about Nick's cities, countries, and travel history.
/// Wired to the <c>geography</c> function group via <see cref="NeedlrAiAgentAttribute"/>.
/// </summary>
[NeedlrAiAgent(
    Description = "Answers questions about Nick's geography, cities, and travel.",
    Instructions = """
        You are Nick's geography expert. Use your tools to look up his cities and countries,
        then give a short, friendly answer.
        """,
    FunctionGroups = new[] { "geography" })]
public partial class GeographyAgent { }
