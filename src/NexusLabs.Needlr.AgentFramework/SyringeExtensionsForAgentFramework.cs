using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NexusLabs.Needlr.AgentFramework.Budget;
using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Progress;
using NexusLabs.Needlr.Injection;

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
/// When the Needlr source generator is active, the generated <c>[ModuleInitializer]</c> registers
/// an <see cref="IAIFunctionProvider"/> that is used instead of reflection. This makes the
/// integration NativeAOT-compatible without any code changes.
/// </para>
/// <para>
/// When the source generator is not used, the integration falls back to reflection-based
/// <see cref="Microsoft.Extensions.AI.AIFunction"/> schema generation, which requires dynamic code.
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
    public static ConfiguredSyringe UsingAgentFramework(
        this ConfiguredSyringe syringe,
        Func<AgentFrameworkSyringe, AgentFrameworkSyringe> configure)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(configure);

        return syringe.UsingPostPluginRegistrationCallback(services =>
        {
            RegisterAgentFrameworkInfrastructure(services);

            // Both IAgentFactory and IProgressReporterFactory resolve the same
            // BuiltAgentFrameworkSyringe singleton, so the configure delegate
            // runs exactly once and both consumers observe identical state
            // regardless of resolution order.
            services.AddSingleton<BuiltAgentFrameworkSyringe>(sp =>
            {
                AgentFrameworkSyringe afSyringe = new()
                {
                    ServiceProvider = sp,
                };
                afSyringe = configure.Invoke(afSyringe);

                // Auto-populate from [ModuleInitializer] bootstrap if nothing was explicitly configured.
                // Explicit calls (Add*FromGenerated, AddAgentFunctions*, etc.) take precedence.
                if ((afSyringe.FunctionTypes is null || afSyringe.FunctionTypes.Count == 0) &&
                    AgentFrameworkGeneratedBootstrap.TryGetFunctionTypes(out var functionProvider))
                {
                    afSyringe = afSyringe with { FunctionTypes = functionProvider().ToList() };
                }

                if ((afSyringe.FunctionGroupMap is null || afSyringe.FunctionGroupMap.Count == 0) &&
                    AgentFrameworkGeneratedBootstrap.TryGetGroupTypes(out var groupProvider))
                {
                    afSyringe = afSyringe with { FunctionGroupMap = groupProvider() };
                }

                if ((afSyringe.AgentTypes is null || afSyringe.AgentTypes.Count == 0) &&
                    AgentFrameworkGeneratedBootstrap.TryGetAgentTypes(out var agentProvider))
                {
                    afSyringe = afSyringe with { AgentTypes = agentProvider().ToList() };
                }

                return new BuiltAgentFrameworkSyringe(afSyringe);
            });

            services.TryAddSingleton<IProgressReporterFactory>(sp =>
            {
                var defaultSinks = sp.GetServices<IProgressSink>();
                return new ProgressReporterFactory(
                    defaultSinks,
                    sp.GetRequiredService<IProgressSequence>(),
                    sp.GetRequiredService<IProgressReporterErrorHandler>());
            });

            services.AddSingleton<IAgentFactory>(sp =>
                sp.GetRequiredService<BuiltAgentFrameworkSyringe>().Value.BuildAgentFactory());

            services.AddSingleton<IWorkflowFactory>(provider =>
                new WorkflowFactory(provider.GetRequiredService<IAgentFactory>()));
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
    /// <returns>
    /// A new <see cref="ConfiguredSyringe"/> instance containing the registration.
    /// </returns>
    public static ConfiguredSyringe UsingAgentFramework(
        this ConfiguredSyringe syringe,
        Func<AgentFrameworkSyringe> configure)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(configure);

        // Route through the configurable overload so both paths share the same
        // BuiltAgentFrameworkSyringe holder, progress sink wiring, and
        // IProgressReporterFactory construction.
        return syringe.UsingAgentFramework(_ => configure.Invoke());
    }

    private static void RegisterAgentFrameworkInfrastructure(IServiceCollection services)
    {
        services.TryAddSingleton<ITokenBudgetTracker, TokenBudgetTracker>();
        services.TryAddSingleton<IAgentExecutionContextAccessor, AgentExecutionContextAccessor>();
        services.TryAddSingleton<AgentDiagnosticsAccessor>(sp =>
            new AgentDiagnosticsAccessor(sp.GetService<ChatCompletionCollectorHolder>()));
        services.TryAddSingleton<IAgentDiagnosticsAccessor>(sp => sp.GetRequiredService<AgentDiagnosticsAccessor>());
        services.TryAddSingleton<IAgentDiagnosticsWriter>(sp => sp.GetRequiredService<AgentDiagnosticsAccessor>());
        services.TryAddSingleton<IToolMetricsAccessor, ToolMetricsAccessor>();
        services.TryAddSingleton<IAgentMetrics>(sp =>
        {
            var syringe = sp.GetService<BuiltAgentFrameworkSyringe>();
            var options = syringe?.Value.MetricsOptions ?? new AgentFrameworkMetricsOptions();
            return new AgentMetrics(options);
        });
        services.TryAddSingleton<ChatCompletionCollectorHolder>();
        services.TryAddSingleton<IChatCompletionCollector>(sp => sp.GetRequiredService<ChatCompletionCollectorHolder>());
        services.TryAddSingleton<IProgressSequence, ProgressSequenceProvider>();
        services.TryAddSingleton<IProgressReporterAccessor, ProgressReporterAccessor>();
        services.TryAddSingleton<IProgressReporterErrorHandler, NullProgressReporterErrorHandler>();
    }
}
