using Microsoft.Agents.AI;

namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Per-agent configuration options passed to <see cref="IAgentFactory.CreateAgent"/>.
/// </summary>
public sealed class AgentFactoryOptions
{
    /// <summary>
    /// Gets or sets the agent's name. Used by MAF to populate <c>ExecutorId</c> in workflow events,
    /// making multi-agent output readable. When <see langword="null"/>, MAF assigns a generated identifier.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets a human-readable description of this agent's purpose.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the system instructions for this specific agent.
    /// When set, overrides the default instructions configured on the factory.
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    /// Gets or sets the subset of function types to wire as tools for this agent.
    /// When <see langword="null"/> and <see cref="FunctionGroups"/> is also <see langword="null"/>,
    /// all function types registered with the factory are used.
    /// </summary>
    public IReadOnlyList<Type>? FunctionTypes { get; set; }

    /// <summary>
    /// Gets or sets the named function groups to wire as tools for this agent.
    /// Groups are declared using <see cref="AgentFunctionGroupAttribute"/> on function classes
    /// and registered via <c>AddAgentFunctionGroupsFromAssemblies()</c> or
    /// <c>AddAgentFunctionGroupsFromGenerated()</c>.
    /// When <see langword="null"/> and <see cref="FunctionTypes"/> is also <see langword="null"/>,
    /// all registered function types are used.
    /// </summary>
    public IReadOnlyList<string>? FunctionGroups { get; set; }
}
