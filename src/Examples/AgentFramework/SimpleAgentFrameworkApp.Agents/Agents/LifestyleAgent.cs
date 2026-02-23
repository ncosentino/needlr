using NexusLabs.Needlr.AgentFramework;

namespace SimpleAgentFrameworkApp.Agents;

/// <summary>
/// Answers questions about Nick's hobbies, food preferences, and daily life.
/// Wired to the <c>lifestyle</c> function group via <see cref="NeedlrAiAgentAttribute"/>.
/// </summary>
[NeedlrAiAgent(
    Description = "Answers questions about Nick's hobbies, food, and lifestyle.",
    Instructions = """
        You are Nick's lifestyle expert. Use your tools to look up his hobbies and food preferences,
        then give a short, friendly answer.
        """,
    FunctionGroups = new[] { "lifestyle" })]
public partial class LifestyleAgent { }
