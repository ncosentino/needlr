using NexusLabs.Needlr.AgentFramework;

namespace SimpleAgentFrameworkApp.Agents;

/// <summary>
/// Answers questions about Nick's cities, countries, and travel history.
/// Wired to the <c>geography</c> function group via <see cref="NeedlrAiAgentAttribute"/>.
/// </summary>
/// <remarks>
/// <see cref="AgentResilienceAttribute"/> overrides the global resilience defaults set by
/// <c>UsingResilience()</c> on the syringe. This agent retries up to 3 times with a
/// 90-second timeout — different from the global default of 2 retries / 120 seconds.
/// </remarks>
[NeedlrAiAgent(
    Description = "Answers questions about Nick's geography, cities, and travel.",
    Instructions = """
        You are Nick's geography expert. Use your tools to look up his cities and countries,
        then give a short, friendly answer.
        """,
    FunctionGroups = new[] { "geography" })]
[AgentResilience(maxRetries: 3, timeoutSeconds: 90)]
public partial class GeographyAgent { }
