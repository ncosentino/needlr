using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NexusLabs.Needlr.AgentFramework;

[RequiresUnreferencedCode("AgentFramework function setup uses reflection to discover [AgentFunction] methods.")]
[RequiresDynamicCode("AgentFramework function setup uses reflection APIs that require dynamic code generation.")]
internal sealed class AgentFactory : IAgentFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly List<Action<AgentFrameworkConfigureOptions>> _configureCallbacks;
    private readonly IReadOnlyList<Type> _functionTypes;
    private readonly Lazy<AgentFrameworkConfigureOptions> _lazyConfiguredOptions;
    private readonly Lazy<IReadOnlyDictionary<Type, IReadOnlyList<AIFunction>>> _lazyFunctionsCache;

    public AgentFactory(
        IServiceProvider serviceProvider,
        List<Action<AgentFrameworkConfigureOptions>> configureCallbacks,
        IReadOnlyList<Type> functionTypes)
    {
        _serviceProvider = serviceProvider;
        _configureCallbacks = configureCallbacks;
        _functionTypes = functionTypes;

        _lazyConfiguredOptions = new(() => BuildConfiguredOptions());
        _lazyFunctionsCache = new(() => BuildFunctionsCache());
    }

    public AIAgent CreateAgent(Action<AgentFactoryOptions>? configure = null)
    {
        var agentOptions = new AgentFactoryOptions();
        configure?.Invoke(agentOptions);

        var configuredOpts = _lazyConfiguredOptions.Value;
        var allFunctions = _lazyFunctionsCache.Value;

        var applicableTypes = agentOptions.FunctionTypes ?? _functionTypes;
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
            instructions: instructions,
            tools: tools,
            services: _serviceProvider);
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

    private IReadOnlyList<AIFunction> BuildFunctionsForType(Type type)
    {
        var isStatic = type.IsAbstract && type.IsSealed;

        object? instance = isStatic
            ? null
            : ActivatorUtilities.CreateInstance(_serviceProvider, type);

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
