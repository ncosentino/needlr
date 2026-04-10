using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Fluent builder for configuring the Microsoft Agent Framework with Needlr function discovery.
/// </summary>
/// <remarks>
/// <para>
/// When the Needlr source generator is active (the common case), this class uses pre-built
/// <see cref="IAIFunctionProvider"/> instances registered by the generated <c>[ModuleInitializer]</c>.
/// No reflection is required in that path.
/// </para>
/// <para>
/// When the source generator is not used, this class falls back to reflection to discover
/// methods decorated with <see cref="AgentFunctionAttribute"/>. That path carries
/// <c>[RequiresDynamicCode]</c> and is not NativeAOT-compatible.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Obtained from SyringeAgentFrameworkExtensions.UsingAgentFramework()
/// AgentFrameworkSyringe syringe = app.Services.UsingAgentFramework();
///
/// // Register function types and build the factory
/// IAgentFactory factory = syringe
///     .AddAgentFunctionsFromGenerated(GeneratedAgentFunctions.AllFunctionTypes)
///     .BuildAgentFactory();
///
/// // Create agents from the factory
/// var supportAgent = factory.CreateAgent&lt;CustomerSupportAgent&gt;();
/// </code>
/// </example>
[DoNotAutoRegister]
public sealed record AgentFrameworkSyringe
{
    public required IServiceProvider ServiceProvider { get; init; }

    internal List<Action<AgentFrameworkConfigureOptions>>? ConfigureAgentFactory { get; init; } = [];

    internal List<Type>? FunctionTypes { get; init; } = [];

    internal IReadOnlyDictionary<string, IReadOnlyList<Type>>? FunctionGroupMap { get; init; }

    internal List<Type>? AgentTypes { get; init; } = [];

    /// <summary>
    /// Agent-builder plugins applied to every agent created by the factory.
    /// Populated by middleware extension methods such as <c>UsingToolResultMiddleware()</c>
    /// and <c>UsingResilience()</c>.
    /// </summary>
    internal IReadOnlyList<IAIAgentBuilderPlugin>? Plugins { get; init; }

    /// <summary>
    /// Factory that creates an <see cref="IAIAgentBuilderPlugin"/> from a
    /// <see cref="AgentResilienceAttribute"/> found on an agent type.
    /// Set by <c>UsingResilience()</c> to enable per-agent resilience overrides via
    /// <c>[AgentResilience]</c>.
    /// </summary>
    internal Func<AgentResilienceAttribute, IAIAgentBuilderPlugin>? PerAgentResilienceFactory { get; init; }

    /// <summary>
    /// Factories for progress sinks to register in DI. Populated by
    /// <c>AddProgressSink&lt;T&gt;()</c> on the syringe. Each factory captures its
    /// closed generic type at the call site so the constructor requirements flow
    /// through the AOT trim analyzer without needing to annotate a shared
    /// <c>List&lt;Type&gt;</c> enumerator.
    /// </summary>
    internal List<Func<IServiceProvider, Progress.IProgressSink>>? ProgressSinkFactories { get; init; } = [];

    public IAgentFactory BuildAgentFactory()
    {
        var groupTypes = (FunctionGroupMap ?? new Dictionary<string, IReadOnlyList<Type>>())
            .SelectMany(kvp => kvp.Value);

        var allFunctionTypes = (FunctionTypes ?? [])
            .Concat(groupTypes)
            .Distinct()
            .ToList();

        var agentTypeMap = (AgentTypes ?? [])
            .ToDictionary(t => t.Name, t => t);

        AgentFrameworkGeneratedBootstrap.TryGetAIFunctionProvider(out var generatedProvider);

        return new AgentFactory(
            serviceProvider: ServiceProvider,
            configureCallbacks: ConfigureAgentFactory ?? [],
            functionTypes: allFunctionTypes,
            functionGroupMap: FunctionGroupMap,
            agentTypeMap: agentTypeMap,
            generatedProvider: generatedProvider,
            plugins: Plugins ?? [],
            perAgentResilienceFactory: PerAgentResilienceFactory);
    }
}
