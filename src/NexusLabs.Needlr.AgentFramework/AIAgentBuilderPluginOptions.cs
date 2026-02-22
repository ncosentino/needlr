using Microsoft.Agents.AI;

namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Options passed to <see cref="IAIAgentBuilderPlugin.Configure"/> to allow
/// plugins to participate in agent-builder configuration.
/// </summary>
public sealed class AIAgentBuilderPluginOptions
{
    /// <summary>
    /// Gets the <see cref="AIAgentBuilder"/> being configured.
    /// </summary>
    public required AIAgentBuilder AgentBuilder { get; init; }
}
