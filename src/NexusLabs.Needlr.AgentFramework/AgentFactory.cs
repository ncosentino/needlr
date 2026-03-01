using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NexusLabs.Needlr.AgentFramework;

internal sealed class AgentFactory : IAgentFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly List<Action<AgentFrameworkConfigureOptions>> _configureCallbacks;
    private readonly IReadOnlyList<Type> _functionTypes;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<Type>> _functionGroupMap;
    private readonly Lazy<AgentFrameworkConfigureOptions> _lazyConfiguredOptions;
    private readonly Lazy<IReadOnlyDictionary<Type, IReadOnlyList<AIFunction>>> _lazyFunctionsCache;
    private readonly IAIFunctionProvider? _generatedProvider;

    private readonly IReadOnlyDictionary<string, Type> _agentTypeMap;

    public AgentFactory(
        IServiceProvider serviceProvider,
        List<Action<AgentFrameworkConfigureOptions>> configureCallbacks,
        IReadOnlyList<Type> functionTypes,
        IReadOnlyDictionary<string, IReadOnlyList<Type>>? functionGroupMap = null,
        IReadOnlyDictionary<string, Type>? agentTypeMap = null,
        IAIFunctionProvider? generatedProvider = null)
    {
        _serviceProvider = serviceProvider;
        _configureCallbacks = configureCallbacks;
        _functionTypes = functionTypes;
        _functionGroupMap = functionGroupMap ?? new Dictionary<string, IReadOnlyList<Type>>();
        _agentTypeMap = agentTypeMap ?? new Dictionary<string, Type>();
        _generatedProvider = generatedProvider;

        _lazyConfiguredOptions = new(() => BuildConfiguredOptions());
        _lazyFunctionsCache = new(() => BuildFunctionsCache());
    }

    public AIAgent CreateAgent<TAgent>() where TAgent : class
        => CreateAgentFromType(typeof(TAgent));

    public AIAgent CreateAgent(string agentClassName)
    {
        ArgumentNullException.ThrowIfNull(agentClassName);

        if (!_agentTypeMap.TryGetValue(agentClassName, out var type))
            throw new InvalidOperationException(
                $"No agent named '{agentClassName}' is registered. " +
                $"Ensure the class is decorated with [NeedlrAiAgent] and registered via " +
                $"AddAgent<T>(), AddAgentsFromGenerated(), or the source generator [ModuleInitializer].");

        return CreateAgentFromType(type);
    }

    private AIAgent CreateAgentFromType(Type agentType)
    {
        var attr = agentType.GetCustomAttribute<NeedlrAiAgentAttribute>()
            ?? throw new InvalidOperationException(
                $"'{agentType.Name}' is not decorated with [NeedlrAiAgent]. " +
                $"Apply [NeedlrAiAgent] to the class or use CreateAgent(configure) to set options manually.");

        return CreateAgent(opts =>
        {
            opts.Name = agentType.Name;
            opts.Description = attr.Description;
            opts.Instructions = attr.Instructions;
            opts.FunctionTypes = attr.FunctionTypes;
            opts.FunctionGroups = attr.FunctionGroups;
        });
    }

    public AIAgent CreateAgent(Action<AgentFactoryOptions>? configure = null)
    {
        var agentOptions = new AgentFactoryOptions();
        configure?.Invoke(agentOptions);

        var configuredOpts = _lazyConfiguredOptions.Value;
        var allFunctions = _lazyFunctionsCache.Value;

        var applicableTypes = GetApplicableTypes(agentOptions);
        var tools = new List<AITool>();

        foreach (var type in applicableTypes)
        {
            if (allFunctions.TryGetValue(type, out var fns))
            {
                tools.AddRange(fns);
            }
        }

        var chatClient = configuredOpts.ChatClientFactory?.Invoke(_serviceProvider)
            ?? _serviceProvider.GetRequiredService<IChatClient>();

        var instructions = agentOptions.Instructions ?? configuredOpts.DefaultInstructions;

        return chatClient.AsAIAgent(
            name: agentOptions.Name,
            description: agentOptions.Description,
            instructions: instructions,
            tools: tools,
            services: _serviceProvider);
    }

    private IEnumerable<Type> GetApplicableTypes(AgentFactoryOptions agentOptions)
    {
        bool hasExplicitScope = agentOptions.FunctionTypes != null || agentOptions.FunctionGroups != null;

        if (!hasExplicitScope)
            return _functionTypes;

        var types = new List<Type>();

        if (agentOptions.FunctionTypes != null)
            types.AddRange(agentOptions.FunctionTypes);

        if (agentOptions.FunctionGroups != null)
        {
            foreach (var group in agentOptions.FunctionGroups)
            {
                if (_functionGroupMap.TryGetValue(group, out var groupTypes))
                    types.AddRange(groupTypes);
            }
        }

        return types.Distinct();
    }

    private AgentFrameworkConfigureOptions BuildConfiguredOptions()
    {
        var opts = new AgentFrameworkConfigureOptions
        {
            ServiceProvider = _serviceProvider,
        };

        foreach (var callback in _configureCallbacks)
        {
            callback(opts);
        }

        return opts;
    }

    private IReadOnlyDictionary<Type, IReadOnlyList<AIFunction>> BuildFunctionsCache()
    {
        var dict = new Dictionary<Type, IReadOnlyList<AIFunction>>();

        foreach (var type in _functionTypes)
        {
            var functions = BuildFunctionsForType(type);
            if (functions.Count > 0)
            {
                dict[type] = functions;
            }
        }

        return dict;
    }

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Reflection branch is unreachable when GeneratedAIFunctionProvider is registered via ModuleInitializer in AOT-compiled assemblies.")]
    [UnconditionalSuppressMessage("TrimAnalysis", "IL2026", Justification = "Reflection fallback path is unreachable in AOT builds where GeneratedAIFunctionProvider is registered.")]
    [UnconditionalSuppressMessage("TrimAnalysis", "IL2067", Justification = "ActivatorUtilities.CreateInstance is only reached in non-AOT builds where all member metadata is available.")]
    private IReadOnlyList<AIFunction> BuildFunctionsForType(Type type)
    {
        if (_generatedProvider?.TryGetFunctions(type, _serviceProvider, out var generated) == true)
            return generated!;

        var isStatic = type.IsAbstract && type.IsSealed;
        object? instance = isStatic
            ? null
            : ActivatorUtilities.CreateInstance(_serviceProvider, type);

        return BuildFunctionsForTypeViaReflection(type, isStatic, instance);
    }

    [RequiresDynamicCode("Reflection-based AIFunction building requires dynamic code generation.")]
    [RequiresUnreferencedCode("Reflection-based AIFunction building requires unreferenced code access.")]
    private static IReadOnlyList<AIFunction> BuildFunctionsForTypeViaReflection(Type type, bool isStatic, object? instance)
    {
        var bindingFlags = isStatic
            ? BindingFlags.Public | BindingFlags.Static
            : BindingFlags.Public | BindingFlags.Instance;

        var functions = type.GetMethods(bindingFlags)
            .Where(m => m.IsDefined(typeof(AgentFunctionAttribute), inherit: true))
            .Select(m => AIFunctionFactory.Create(m, target: instance))
            .ToList();

        return functions.AsReadOnly();
    }
}
