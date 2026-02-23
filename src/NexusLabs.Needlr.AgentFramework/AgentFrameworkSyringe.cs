using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using System.Diagnostics.CodeAnalysis;

namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Fluent builder for configuring the Microsoft Agent Framework with Needlr function discovery.
/// </summary>
/// <remarks>
/// This class uses reflection to discover methods with <see cref="AgentFunctionAttribute"/>.
/// For AOT/trimmed applications, consider registering agent functions explicitly.
/// </remarks>
[DoNotAutoRegister]
[RequiresUnreferencedCode("AgentFramework function setup uses reflection to discover [AgentFunction] methods.")]
[RequiresDynamicCode("AgentFramework function setup uses reflection APIs that require dynamic code generation.")]
public sealed record AgentFrameworkSyringe
{
    public required IServiceProvider ServiceProvider { get; init; }

    internal List<Action<AgentFrameworkConfigureOptions>>? ConfigureAgentFactory { get; init; } = [];

    internal List<Type>? FunctionTypes { get; init; } = [];

    internal IReadOnlyDictionary<string, IReadOnlyList<Type>>? FunctionGroupMap { get; init; }

    internal List<Type>? AgentTypes { get; init; } = [];

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

        return new AgentFactory(
            serviceProvider: ServiceProvider,
            configureCallbacks: ConfigureAgentFactory ?? [],
            functionTypes: allFunctionTypes,
            functionGroupMap: FunctionGroupMap,
            agentTypeMap: agentTypeMap);
    }
}
