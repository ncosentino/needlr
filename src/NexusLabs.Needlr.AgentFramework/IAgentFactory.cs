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

    /// <summary>
    /// Creates a new <see cref="AIAgent"/> by reading configuration directly from the
    /// <see cref="NeedlrAiAgentAttribute"/> on <typeparamref name="TAgent"/>.
    /// </summary>
    /// <typeparam name="TAgent">
    /// A class decorated with <see cref="NeedlrAiAgentAttribute"/>.
    /// The class name becomes the agent's <c>Name</c>.
    /// </typeparam>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <typeparamref name="TAgent"/> is not decorated with <see cref="NeedlrAiAgentAttribute"/>.
    /// </exception>
    AIAgent CreateAgent<TAgent>() where TAgent : class;

    /// <summary>
    /// Creates a new <see cref="AIAgent"/> by looking up the registered type for
    /// <paramref name="agentClassName"/> and reading its <see cref="NeedlrAiAgentAttribute"/>.
    /// </summary>
    /// <param name="agentClassName">
    /// The simple class name of an agent type registered via <c>AddAgent&lt;T&gt;()</c>,
    /// <c>AddAgentsFromGenerated()</c>, or the source generator bootstrap.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no agent with the given name is registered.
    /// </exception>
    AIAgent CreateAgent(string agentClassName);
}
