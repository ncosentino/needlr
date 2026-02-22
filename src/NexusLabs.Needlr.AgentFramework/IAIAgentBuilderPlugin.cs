namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Marker interface for classes that participate in configuring the agent builder
/// pipeline (e.g., adding middleware layers).
/// </summary>
public interface IAIAgentBuilderPlugin
{
    /// <summary>
    /// Called during agent factory initialisation to configure the agent builder.
    /// </summary>
    void Configure(AIAgentBuilderPluginOptions options);
}
