using NexusLabs.Needlr.AgentFramework;

namespace SimpleAgentFrameworkApp.Agents;

/// <summary>
/// Routes customer questions to the appropriate specialist agent.
/// Declared with <see cref="NeedlrAiAgentAttribute"/> so the source generator registers it
/// automatically — no startup code required.
/// The <see cref="AgentHandoffsToAttribute"/> declarations are read by the generator to emit
/// a strongly-typed <c>CreateTriageHandoffWorkflow()</c> extension method on
/// <see cref="IWorkflowFactory"/>.
/// </summary>
[NeedlrAiAgent(
    Description = "Routes questions about Nick to the appropriate specialist.",
    Instructions = """
        You are a triage assistant for questions about Nick. Route each question to exactly one specialist:
        - GeographyAgent: cities, countries, travel, places Nick has lived
        - LifestyleAgent: hobbies, food, ice cream, daily life, interests
        Always hand off. Never answer directly.
        """,
    FunctionTypes = new Type[0])]
[AgentHandoffsTo(typeof(GeographyAgent), "For questions about Nick's cities, countries, or places he has lived")]
[AgentHandoffsTo(typeof(LifestyleAgent), "For questions about Nick's hobbies, food preferences, or daily life")]
public partial class TriageAgent { }
