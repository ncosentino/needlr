using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;

namespace NexusLabs.Needlr.AgentFramework.DevUI;

/// <summary>
/// Extension methods for bridging Needlr's <see cref="NeedlrAiAgentAttribute"/>-declared
/// agents into MAF DevUI's entity discovery.
/// </summary>
/// <remarks>
/// <para>
/// MAF DevUI discovers agents via <c>IServiceProvider.GetKeyedServices&lt;AIAgent&gt;(KeyedService.AnyKey)</c>.
/// Needlr creates agents lazily via <see cref="IAgentFactory.CreateAgent(string)"/>.
/// This bridge registers each source-generated agent type as a keyed <see cref="AIAgent"/>
/// singleton so DevUI's <c>/v1/entities</c> endpoint lists them.
/// </para>
/// <para>
/// This package deliberately isolates the preview-only DevUI and Hosting package
/// dependencies from the stable <c>NexusLabs.Needlr.AgentFramework</c> package.
/// </para>
/// </remarks>
public static class NeedlrDevUIServiceCollectionExtensions
{
    /// <summary>
    /// Registers all <see cref="NeedlrAiAgentAttribute"/>-declared agents as keyed
    /// <see cref="AIAgent"/> services in DI so they appear in MAF DevUI's entity
    /// discovery at <c>/v1/entities</c>.
    /// </summary>
    /// <param name="services">The service collection to register agents into.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Call this after <c>UsingAgentFramework()</c> and before <c>builder.Build()</c>.
    /// The method reads agent types from the static
    /// <see cref="AgentFrameworkGeneratedBootstrap"/> registry (populated by the
    /// source-generated <c>[ModuleInitializer]</c>) and registers each as a keyed
    /// singleton. The key is the agent class name (e.g., <c>"TriageAgent"</c>).
    /// </para>
    /// <para>
    /// Each keyed registration delegates to <see cref="IAgentFactory.CreateAgent(string)"/>
    /// for lazy creation, so the agent's chat client,
    /// tools, and middleware are resolved from DI at first access.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var builder = WebApplication.CreateBuilder(args);
    ///
    /// builder.Services.AddNeedlr()
    ///     .UsingAgentFramework()
    ///     .Build();
    ///
    /// // Bridge Needlr agents → DevUI
    /// builder.Services.AddNeedlrDevUI();
    ///
    /// // Standard DevUI setup
    /// builder.Services.AddDevUI();
    ///
    /// var app = builder.Build();
    /// app.MapOpenAIResponses();
    /// app.MapOpenAIConversations();
    /// app.MapDevUI();
    /// app.Run();
    /// </code>
    /// </example>
    public static IServiceCollection AddNeedlrDevUI(
        this IServiceCollection services)
    {
        if (!AgentFrameworkGeneratedBootstrap.TryGetAgentTypes(out var agentTypesProvider))
        {
            return services;
        }

        var agentTypes = agentTypesProvider();

        foreach (var agentType in agentTypes)
        {
            var agentName = agentType.Name;
            services.AddKeyedSingleton<AIAgent>(agentName, (sp, key) =>
            {
                var factory = sp.GetRequiredService<IAgentFactory>();
                return factory.CreateAgent(key as string ?? agentName);
            });
        }

        return services;
    }
}
