using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;

using System.Diagnostics.CodeAnalysis;

namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Extension methods for <see cref="ConfiguredSyringe"/> that enable registering
/// Microsoft Agent Framework infrastructure (namely <see cref="IAgentFactory"/>)
/// as part of the Needlr build pipeline.
/// </summary>
/// <remarks>
/// <para>
/// These helpers defer service registration using the Syringe
/// post-plugin registration callback so that function discovery and
/// registration are completed before the agent factory is added.
/// </para>
/// <para>
/// <strong>Note:</strong> Microsoft.Extensions.AI internally uses reflection to build
/// <see cref="Microsoft.Extensions.AI.AIFunction"/> JSON schemas from method signatures.
/// This integration therefore requires reflection and is not fully AOT-compatible.
/// For AOT scenarios, consider using the source generator and registering agent functions explicitly.
/// </para>
/// </remarks>
public static class SyringeExtensionsForAgentFramework
{
    /// <summary>
    /// Registers an <see cref="IAgentFactory"/> built via a
    /// <see cref="AgentFrameworkSyringe"/> instance.
    /// </summary>
    /// <param name="syringe">
    /// The <see cref="ConfiguredSyringe"/> to augment with the registration.
    /// </param>
    /// <returns>
    /// A new <see cref="ConfiguredSyringe"/> instance containing the registration.
    /// </returns>
    [RequiresUnreferencedCode("Agent Framework uses reflection to build AIFunction schemas from method signatures.")]
    [RequiresDynamicCode("Agent Framework uses reflection APIs that require dynamic code generation.")]
    public static ConfiguredSyringe UsingAgentFramework(
        this ConfiguredSyringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.UsingAgentFramework(s => s);
    }

    /// <summary>
    /// Registers an <see cref="IAgentFactory"/> built via a configurable
    /// <see cref="AgentFrameworkSyringe"/> instance.
    /// </summary>
    /// <param name="syringe">
    /// The <see cref="ConfiguredSyringe"/> to augment with the registration.
    /// </param>
    /// <param name="configure">
    /// A delegate that receives a pre-initialized <see cref="AgentFrameworkSyringe"/>
    /// (with its <see cref="AgentFrameworkSyringe.ServiceProvider"/> set) and
    /// returns the configured instance used to build the agent factory.
    /// </param>
    /// <returns>
    /// A new <see cref="ConfiguredSyringe"/> instance containing the registration.
    /// </returns>
    [RequiresUnreferencedCode("Agent Framework uses reflection to build AIFunction schemas from method signatures.")]
    [RequiresDynamicCode("Agent Framework uses reflection APIs that require dynamic code generation.")]
    public static ConfiguredSyringe UsingAgentFramework(
        this ConfiguredSyringe syringe,
        Func<AgentFrameworkSyringe, AgentFrameworkSyringe> configure)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(configure);

        return syringe.UsingPostPluginRegistrationCallback(services =>
        {
            services.AddSingleton<IAgentFactory>(provider =>
            {
                AgentFrameworkSyringe afSyringe = new()
                {
                    ServiceProvider = provider,
                };
                afSyringe = configure.Invoke(afSyringe);
                return afSyringe.BuildAgentFactory();
            });
        });
    }

    /// <summary>
    /// Registers an <see cref="IAgentFactory"/> built via an
    /// <see cref="AgentFrameworkSyringe"/> created by the supplied delegate.
    /// </summary>
    /// <param name="syringe">
    /// The <see cref="ConfiguredSyringe"/> to augment with the registration.
    /// </param>
    /// <param name="configure">
    /// A factory that creates a fully-configured <see cref="AgentFrameworkSyringe"/>
    /// used to build the agent factory. Useful when configuration does
    /// not need the service provider.
    /// </param>
    [RequiresUnreferencedCode("Agent Framework uses reflection to build AIFunction schemas from method signatures.")]
    [RequiresDynamicCode("Agent Framework uses reflection APIs that require dynamic code generation.")]
    public static ConfiguredSyringe UsingAgentFramework(
        this ConfiguredSyringe syringe,
        Func<AgentFrameworkSyringe> configure)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(configure);

        return syringe.UsingPostPluginRegistrationCallback(services =>
        {
            services.AddSingleton<IAgentFactory>(provider =>
            {
                var afSyringe = configure.Invoke();
                return afSyringe.BuildAgentFactory();
            });
        });
    }
}
