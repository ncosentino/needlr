using Microsoft.Agents.AI;

namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Per-agent configuration options passed to <see cref="IAgentFactory.CreateAgent"/>.
/// </summary>
public sealed class AgentFactoryOptions
{
    /// <summary>
    /// Gets or sets the system instructions for this specific agent.
    /// When set, overrides the default instructions configured on the factory.
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    /// Gets or sets the subset of function types to wire as tools for this agent.
    /// When <see langword="null"/>, all function types registered with the factory are used.
    /// When set, only methods from the listed types decorated with
    /// <see cref="AgentFunctionAttribute"/> are wired as <see cref="Microsoft.Extensions.AI.AIFunction"/> tools.
    /// </summary>
    public IReadOnlyList<Type>? FunctionTypes { get; set; }
}
