using NexusLabs.Needlr.Injection;

namespace NexusLabs.Needlr.AgentFramework.Workflows;

/// <summary>
/// Syringe extension to register graph workflow services alongside the
/// agent framework. Call after <c>UsingAgentFramework()</c> in the syringe
/// fluent chain.
/// </summary>
/// <example>
/// <code>
/// var provider = new Syringe()
///     .UsingReflection()
///     .UsingAgentFramework(af => af.Configure(...))
///     .UsingGraphWorkflows()
///     .BuildServiceProvider(config);
///
/// var runner = provider.GetRequiredService&lt;IGraphWorkflowRunner&gt;();
/// var result = await runner.RunGraphAsync("my-graph", "input");
/// </code>
/// </example>
public static class SyringeGraphWorkflowExtensions
{
    /// <summary>
    /// Registers <see cref="IGraphWorkflowRunner"/> and its dependencies.
    /// Must be called after <c>UsingAgentFramework()</c>.
    /// </summary>
    public static ConfiguredSyringe UsingGraphWorkflows(
        this ConfiguredSyringe syringe) =>
        syringe.UsingPostPluginRegistrationCallback(
            services => services.AddGraphWorkflowRunner());
}
