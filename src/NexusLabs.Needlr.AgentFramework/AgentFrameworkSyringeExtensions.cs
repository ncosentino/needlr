using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using NexusLabs.Needlr.AgentFramework.FunctionScanners;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Extension methods for <see cref="AgentFrameworkSyringe"/> providing fluent configuration
/// of the Microsoft Agent Framework integration.
/// </summary>
public static class AgentFrameworkSyringeExtensions
{
    /// <summary>
    /// Sets the <see cref="IChatClient"/> used by all agents created from the factory.
    /// This is the preferred alternative to calling
    /// <see cref="Configure"/> and setting <see cref="AgentFrameworkConfigureOptions.ChatClientFactory"/>.
    /// </summary>
    public static AgentFrameworkSyringe UsingChatClient(
        this AgentFrameworkSyringe syringe,
        IChatClient chatClient)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(chatClient);

        return syringe.Configure(opts => opts.ChatClientFactory = _ => chatClient);
    }

    /// <summary>
    /// Sets a factory that creates the <see cref="IChatClient"/> used by all agents.
    /// The factory receives the DI <see cref="IServiceProvider"/> for resolving dependencies.
    /// </summary>
    public static AgentFrameworkSyringe UsingChatClient(
        this AgentFrameworkSyringe syringe,
        Func<IServiceProvider, IChatClient> chatClientFactory)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(chatClientFactory);

        return syringe.Configure(opts => opts.ChatClientFactory = chatClientFactory);
    }

    public static AgentFrameworkSyringe Configure(
        this AgentFrameworkSyringe syringe,
        Action<AgentFrameworkConfigureOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(configure);

        return syringe with
        {
            ConfigureAgentFactory = (syringe.ConfigureAgentFactory ?? []).Append(configure).ToList()
        };
    }

    public static AgentFrameworkSyringe AddAgentFunction<T>(
        this AgentFrameworkSyringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);

        return syringe.AddAgentFunctions([typeof(T)]);
    }

    /// <summary>
    /// Adds agent functions from a compile-time generated list of types.
    /// This is the recommended approach for AOT/trimmed applications because
    /// the source generator discovers types at build time rather than runtime.
    /// </summary>
    /// <param name="syringe">The agent framework syringe to configure.</param>
    /// <param name="functionTypes">
    /// Compile-time discovered function types, typically from the generated
    /// <c>NexusLabs.Needlr.Generated.AgentFrameworkFunctions.AllFunctionTypes</c>.
    /// </param>
    /// <remarks>
    /// This overload performs no reflection for type discovery. The
    /// <see cref="AgentFactory"/> still uses reflection when building
    /// <see cref="Microsoft.Extensions.AI.AIFunction"/> schema from method signatures —
    /// the same inherent limitation present in Microsoft.Extensions.AI.
    /// </remarks>
    public static AgentFrameworkSyringe AddAgentFunctionsFromGenerated(
        this AgentFrameworkSyringe syringe,
        IReadOnlyList<Type> functionTypes)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(functionTypes);

        return syringe with
        {
            FunctionTypes = (syringe.FunctionTypes ?? []).Concat(functionTypes).Distinct().ToList()
        };
    }

    public static AgentFrameworkSyringe AddAgentFunctionsFromScanner(
        this AgentFrameworkSyringe syringe,
        IAgentFrameworkFunctionScanner scanner)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(scanner);

        return syringe.AddAgentFunctions(scanner.ScanForFunctionTypes());
    }

    [RequiresUnreferencedCode("Assembly scanning uses reflection to discover types with [AgentFunction] methods.")]
    [RequiresDynamicCode("Assembly scanning uses reflection APIs that may require dynamic code generation.")]
    public static AgentFrameworkSyringe AddAgentFunctionsFromAssemblies(
        this AgentFrameworkSyringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);

        var assemblies = syringe.ServiceProvider.GetRequiredService<IReadOnlyList<Assembly>>();
        return syringe.AddAgentFunctionsFromAssemblies(assemblies);
    }

    [RequiresUnreferencedCode("Assembly scanning uses reflection to discover types with [AgentFunction] methods.")]
    [RequiresDynamicCode("Assembly scanning uses reflection APIs that may require dynamic code generation.")]
    public static AgentFrameworkSyringe AddAgentFunctionsFromAssemblies(
        this AgentFrameworkSyringe syringe,
        IReadOnlyList<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(assemblies);

        var scanner = new AssemblyAgentFunctionScanner(assemblies);
        return syringe.AddAgentFunctionsFromScanner(scanner);
    }

    [RequiresUnreferencedCode("Service provider scanning uses reflection to discover types with [AgentFunction] methods.")]
    [RequiresDynamicCode("Service provider scanning uses reflection APIs that may require dynamic code generation.")]
    public static AgentFrameworkSyringe AddAgentFunctionsFromProvider(
        this AgentFrameworkSyringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);

        var scanner = new ServiceProviderAgentFunctionScanner(syringe.ServiceProvider);
        return syringe.AddAgentFunctionsFromScanner(scanner);
    }

    [RequiresUnreferencedCode("Function type inspection uses reflection to check for [AgentFunction] methods.")]
    [RequiresDynamicCode("Function type inspection uses reflection APIs that may require dynamic code generation.")]
    public static AgentFrameworkSyringe AddAgentFunctions(
        this AgentFrameworkSyringe syringe,
        IReadOnlyList<Type> functionTypes)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(functionTypes);

        List<Type> typesToAdd = [];

        foreach (var functionType in functionTypes)
        {
            var bindingFlags = functionType.IsStatic()
                ? BindingFlags.Public | BindingFlags.Static
                : BindingFlags.Public | BindingFlags.Instance;

            if (!functionType.GetMethods(bindingFlags)
                .Any(m => m.IsDefined(typeof(AgentFunctionAttribute), inherit: true)))
            {
                continue;
            }

            typesToAdd.Add(functionType);
        }

        return syringe with
        {
            FunctionTypes = (syringe.FunctionTypes ?? []).Concat(typesToAdd).Distinct().ToList()
        };
    }
}
