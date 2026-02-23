namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Marks a class as a declared agent type for Needlr's Agent Framework integration.
/// Apply this attribute to a class to enable compile-time registration via the source generator
/// and <see cref="IAgentFactory.CreateAgent{TAgent}"/> lookup.
/// </summary>
/// <remarks>
/// When the <c>NexusLabs.Needlr.AgentFramework.Generators</c> package is referenced,
/// a <c>[ModuleInitializer]</c> is emitted that automatically registers the agent type
/// with <see cref="AgentFrameworkGeneratedBootstrap"/>. <c>UsingAgentFramework()</c>
/// then discovers and registers these types without any explicit <c>Add*FromGenerated()</c> calls.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class NeedlrAiAgentAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the system prompt instructions for this agent.
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    /// Gets or sets a human-readable description of this agent's purpose.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the function types whose <see cref="AgentFunctionAttribute"/>-tagged methods
    /// are wired as tools for this agent. When null and <see cref="FunctionGroups"/> is also null,
    /// all registered function types are used.
    /// </summary>
    public Type[]? FunctionTypes { get; set; }

    /// <summary>
    /// Gets or sets named function groups (registered via <see cref="AgentFunctionGroupAttribute"/>)
    /// whose types are wired as tools for this agent.
    /// </summary>
    public string[]? FunctionGroups { get; set; }
}
