namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Extension point for adding middleware layers to every agent created by
/// <see cref="IAgentFactory"/>. Implementations are registered via
/// <see cref="AgentFrameworkSyringe.Plugins"/> and applied in order during
/// <see cref="IAgentFactory.CreateAgent{TAgent}()"/>.
/// </summary>
/// <remarks>
/// <para>
/// Built-in plugins include the diagnostics middleware (<c>AgentDiagnosticsPlugin</c>)
/// and resilience middleware (<c>AgentResiliencePlugin</c>). Custom plugins can add
/// logging, rate limiting, content filtering, or any other cross-cutting concern
/// that should apply to every agent.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class LoggingPlugin : IAIAgentBuilderPlugin
/// {
///     public void Configure(AIAgentBuilderPluginOptions options)
///     {
///         options.AgentBuilder.Use(
///             runFunc: async (messages, session, runOptions, innerAgent, ct) =>
///             {
///                 Console.WriteLine($"Agent '{innerAgent.Name}' starting...");
///                 var response = await innerAgent.RunAsync(messages, session, runOptions, ct);
///                 Console.WriteLine($"Agent '{innerAgent.Name}' completed.");
///                 return response;
///             },
///             runStreamingFunc: null);
///     }
/// }
/// </code>
/// </example>
public interface IAIAgentBuilderPlugin
{
    /// <summary>
    /// Called once per agent creation to configure the builder's middleware pipeline.
    /// Use <see cref="AIAgentBuilderPluginOptions.AgentBuilder"/> to add
    /// <c>Use(...)</c> middleware layers.
    /// </summary>
    /// <param name="options">Provides access to the <c>AIAgentBuilder</c> for the agent being constructed.</param>
    void Configure(AIAgentBuilderPluginOptions options);
}
