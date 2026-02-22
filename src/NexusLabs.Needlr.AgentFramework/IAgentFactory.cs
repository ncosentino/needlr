using Microsoft.Agents.AI;

namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Factory for creating configured <see cref="AIAgent"/> instances with
/// auto-discovered <see cref="AgentFunctionAttribute"/> tools wired up.
/// </summary>
public interface IAgentFactory
{
    /// <summary>
    /// Creates a new <see cref="AIAgent"/> with the registered function tools applied.
    /// </summary>
    /// <param name="configure">
    /// Optional callback to configure per-agent options such as instructions
    /// or a subset of function types to wire for this specific agent instance.
    /// </param>
    /// <returns>A fully configured <see cref="AIAgent"/> ready to run.</returns>
    AIAgent CreateAgent(Action<AgentFactoryOptions>? configure = null);
}
