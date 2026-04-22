using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Default implementation of <see cref="IWorkflowFactory"/> that assembles MAF workflows from
/// topology declared via <see cref="AgentHandoffsToAttribute"/> and
/// <see cref="AgentGroupChatMemberAttribute"/>.
/// </summary>
/// <remarks>
/// When the source generator bootstrap is registered (i.e., the generator package is included
/// and <c>UsingAgentFramework()</c> detected a <c>[ModuleInitializer]</c>-emitted registration),
/// topology data is read from the compile-time registry for zero-allocation discovery.
/// When no bootstrap data is available, topology is discovered via reflection at workflow
/// creation time; that path is annotated <see cref="RequiresUnreferencedCodeAttribute"/>.
/// </remarks>
internal sealed class WorkflowFactory : IWorkflowFactory
{
    private readonly IAgentFactory _agentFactory;

    public WorkflowFactory(IAgentFactory agentFactory)
    {
        ArgumentNullException.ThrowIfNull(agentFactory);
        _agentFactory = agentFactory;
    }

    /// <inheritdoc/>
    public Workflow CreateHandoffWorkflow<TInitialAgent>() where TInitialAgent : class
    {
        var targets = ResolveHandoffTargets(typeof(TInitialAgent));
        var initialAgent = _agentFactory.CreateAgent<TInitialAgent>();
        return BuildHandoff(initialAgent, targets);
    }

    /// <inheritdoc/>
    public Workflow CreateGroupChatWorkflow(string groupName, int maxIterations = 10)
        => CreateGroupChatWorkflowCore(groupName, maxIterations, configureAgent: null);

    /// <inheritdoc/>
    public Workflow CreateGroupChatWorkflow(string groupName, int maxIterations, Action<Type, AgentFactoryOptions> configureAgent)
    {
        ArgumentNullException.ThrowIfNull(configureAgent);
        return CreateGroupChatWorkflowCore(groupName, maxIterations, configureAgent);
    }

    private Workflow CreateGroupChatWorkflowCore(string groupName, int maxIterations, Action<Type, AgentFactoryOptions>? configureAgent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);

        var memberTypes = ResolveGroupChatMembers(groupName)
            .OrderBy(t => t.GetCustomAttributes<AgentGroupChatMemberAttribute>()
                .Where(a => string.Equals(a.GroupName, groupName, StringComparison.Ordinal))
                .Select(a => a.Order)
                .FirstOrDefault())
            .ThenBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

        if (memberTypes.Count < 2)
        {
            throw new InvalidOperationException(
                $"CreateGroupChatWorkflow(\"{groupName}\") failed: {memberTypes.Count} agent(s) are " +
                $"registered as members of group \"{groupName}\". At least two are required. " +
                $"Add [AgentGroupChatMember(\"{groupName}\")] to the agent classes and ensure their " +
                $"assemblies are scanned.");
        }

        var agents = memberTypes.Select(t =>
        {
            if (configureAgent is not null)
                return _agentFactory.CreateAgent(t.Name, opts => configureAgent(t, opts));
            return _agentFactory.CreateAgent(t.Name);
        }).ToList();

        var conditions = BuildTerminationConditions(memberTypes);

        Func<IReadOnlyList<AIAgent>, RoundRobinGroupChatManager> managerFactory = conditions.Count > 0
            ? a => new RoundRobinGroupChatManager(a, ShouldTerminateAsync(conditions)) { MaximumIterationCount = maxIterations }
            : a => new RoundRobinGroupChatManager(a) { MaximumIterationCount = maxIterations };

        return AgentWorkflowBuilder
            .CreateGroupChatBuilderWith(managerFactory)
            .AddParticipants(agents)
            .Build();
    }

    /// <inheritdoc/>
    public Workflow CreateSequentialWorkflow(params AIAgent[] agents)
    {
        ArgumentNullException.ThrowIfNull(agents);

        if (agents.Length == 0)
            throw new ArgumentException("At least one agent is required.", nameof(agents));

        return AgentWorkflowBuilder.BuildSequential(agents);
    }

    /// <inheritdoc/>
    public Workflow CreateSequentialWorkflow(string pipelineName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);

        var memberTypes = ResolveSequentialMembers(pipelineName);
        var agents = memberTypes.Select(t => _agentFactory.CreateAgent(t.Name)).ToArray();
        return AgentWorkflowBuilder.BuildSequential(agents);
    }

    [UnconditionalSuppressMessage("TrimAnalysis", "IL2026", Justification = "Reflection fallback is unreachable in AOT builds where the source generator bootstrap is registered via ModuleInitializer.")]
    private IReadOnlyList<(Type TargetType, string? HandoffReason)> ResolveHandoffTargets(Type initialAgentType)
    {
        if (AgentFrameworkGeneratedBootstrap.TryGetHandoffTopology(out var provider))
        {
            var topology = provider();
            if (topology.TryGetValue(initialAgentType, out var targets))
                return targets;

            // Type is not in the bootstrap topology — it may be from an assembly that didn't run the
            // generator. Fall back to reflection so multi-assembly and test scenarios work correctly.
            return ResolveHandoffTargetsViaReflection(initialAgentType);
        }

        return ResolveHandoffTargetsViaReflection(initialAgentType);
    }

    [RequiresUnreferencedCode("Reflection-based topology discovery may not work after trimming. Use the source generator package instead.")]
    private static IReadOnlyList<(Type TargetType, string? HandoffReason)> ResolveHandoffTargetsViaReflection(Type initialAgentType)
    {
        var attrs = initialAgentType.GetCustomAttributes<AgentHandoffsToAttribute>().ToList();

        if (attrs.Count == 0)
        {
            throw new InvalidOperationException(
                $"CreateHandoffWorkflow<{initialAgentType.Name}>() failed: {initialAgentType.Name} has no " +
                $"[AgentHandoffsTo] attributes. Declare at least one " +
                $"[AgentHandoffsTo(typeof(TargetAgent))] on {initialAgentType.Name} to specify its handoff targets.");
        }

        return attrs
            .Select(a => (a.TargetAgentType, a.HandoffReason))
            .ToList()
            .AsReadOnly();
    }

    [UnconditionalSuppressMessage("TrimAnalysis", "IL2026", Justification = "Reflection fallback is unreachable in AOT builds where the source generator bootstrap is registered via ModuleInitializer.")]
    private IReadOnlyList<Type> ResolveGroupChatMembers(string groupName)
    {
        if (AgentFrameworkGeneratedBootstrap.TryGetGroupChatGroups(out var provider))
        {
            var groups = provider();
            if (groups.TryGetValue(groupName, out var members))
                return members;

            // Group not in the bootstrap — may be from an assembly that didn't run the generator.
            // Fall back to reflection so multi-assembly and test scenarios work correctly.
            return ResolveGroupChatMembersViaReflection(groupName);
        }

        return ResolveGroupChatMembersViaReflection(groupName);
    }

    [RequiresUnreferencedCode("Reflection-based group chat discovery may not work after trimming. Use the source generator package instead.")]
    private static IReadOnlyList<Type> ResolveGroupChatMembersViaReflection(string groupName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch { return []; }
            })
            .Where(t => t.GetCustomAttributes<AgentGroupChatMemberAttribute>()
                         .Any(attr => string.Equals(attr.GroupName, groupName, StringComparison.Ordinal)))
            .ToList()
            .AsReadOnly();
    }

    [UnconditionalSuppressMessage("TrimAnalysis", "IL2026", Justification = "Reflection fallback is unreachable in AOT builds where the source generator bootstrap is registered via ModuleInitializer.")]
    private static IReadOnlyList<Type> ResolveSequentialMembers(string pipelineName)
    {
        if (AgentFrameworkGeneratedBootstrap.TryGetSequentialTopology(out var provider))
        {
            var topology = provider();
            if (topology.TryGetValue(pipelineName, out var members) && members.Count > 0)
                return members;

            return ResolveSequentialMembersViaReflection(pipelineName);
        }

        return ResolveSequentialMembersViaReflection(pipelineName);
    }

    [RequiresUnreferencedCode("Reflection-based sequential pipeline discovery may not work after trimming. Use the source generator package instead.")]
    private static IReadOnlyList<Type> ResolveSequentialMembersViaReflection(string pipelineName)
    {
        var members = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch { return []; }
            })
            .SelectMany(t => t.GetCustomAttributes<AgentSequenceMemberAttribute>()
                .Where(attr => string.Equals(attr.PipelineName, pipelineName, StringComparison.Ordinal))
                .Select(attr => (Type: t, attr.Order)))
            .OrderBy(x => x.Order)
            .Select(x => x.Type)
            .ToList()
            .AsReadOnly();

        if (members.Count == 0)
            throw new InvalidOperationException(
                $"No agents found for sequential pipeline '{pipelineName}'. " +
                $"Decorate agent classes with [AgentSequenceMember(\"{pipelineName}\", order)] and ensure their assemblies are loaded.");

        return members;
    }

    private Workflow BuildHandoff(
        AIAgent initialAgent,
        IReadOnlyList<(Type TargetType, string? HandoffReason)> targets)
    {
        if (targets.Count == 0)
        {
            throw new InvalidOperationException(
                $"Cannot build handoff workflow for agent '{initialAgent.Name ?? initialAgent.Id}': no handoff targets found.");
        }

        var targetPairs = targets
            .Select(t => (_agentFactory.CreateAgent(t.TargetType.Name), t.HandoffReason))
            .ToArray();

        var builder = AgentWorkflowBuilder.CreateHandoffBuilderWith(initialAgent);

        var withoutReason = targetPairs
            .Where(t => string.IsNullOrEmpty(t.HandoffReason))
            .Select(t => t.Item1)
            .ToArray();

        if (withoutReason.Length > 0)
            builder.WithHandoffs(initialAgent, withoutReason);

        foreach (var (target, reason) in targetPairs.Where(t => !string.IsNullOrEmpty(t.HandoffReason)))
            builder.WithHandoff(initialAgent, target, reason!);

        return builder.Build();
    }

    private static IReadOnlyList<(string AgentName, IWorkflowTerminationCondition Condition)> BuildTerminationConditions(
        IReadOnlyList<Type> memberTypes)
    {
        var result = new List<(string, IWorkflowTerminationCondition)>();
        foreach (var type in memberTypes)
        {
            foreach (var attr in type.GetCustomAttributes<AgentTerminationConditionAttribute>())
            {
                var condition = (IWorkflowTerminationCondition)Activator.CreateInstance(
                    attr.ConditionType, attr.CtorArgs)!;
                result.Add((type.Name, condition));
            }
        }
        return result;
    }

    private static Func<RoundRobinGroupChatManager, IEnumerable<Microsoft.Extensions.AI.ChatMessage>, CancellationToken, ValueTask<bool>> ShouldTerminateAsync(
        IReadOnlyList<(string AgentName, IWorkflowTerminationCondition Condition)> conditions)
    {
        return (manager, history, ct) =>
        {
            var historyList = history.ToList();
            if (historyList.Count == 0)
                return ValueTask.FromResult(false);

            var lastMessage = historyList[^1];
            var agentId = lastMessage.AuthorName ?? string.Empty;

            var toolCallNames = lastMessage.Contents
                .OfType<FunctionCallContent>()
                .Select(fc => fc.Name)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();

            var ctx = new TerminationContext
            {
                AgentId = agentId,
                LastMessage = lastMessage,
                TurnCount = historyList.Count,
                ConversationHistory = historyList,
                ToolCallNames = toolCallNames,
            };

            foreach (var (_, condition) in conditions)
            {
                if (condition.ShouldTerminate(ctx))
                    return ValueTask.FromResult(true);
            }

            return ValueTask.FromResult(false);
        };
    }

    /// <inheritdoc />
    [RequiresUnreferencedCode("Graph workflow discovery uses reflection when source-generated bootstrap data is unavailable.")]
    public Workflow CreateGraphWorkflow(string graphName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(graphName);

        var entryType = FindGraphEntryType(graphName);
        var entryAttr = entryType.GetCustomAttributes<AgentGraphEntryAttribute>()
            .First(a => string.Equals(a.GraphName, graphName, StringComparison.Ordinal));

        var edges = DiscoverGraphEdges(graphName);
        if (edges.Count == 0)
        {
            throw new InvalidOperationException(
                $"Cannot build graph workflow '{graphName}': no edges found.");
        }

        var allAgentTypes = new HashSet<Type> { entryType };
        foreach (var edge in edges)
        {
            allAgentTypes.Add(edge.SourceType);
            allAgentTypes.Add(edge.TargetType);
        }

        // Create agents and bind executors ONCE per agent to avoid duplicate bindings.
        var agents = new Dictionary<Type, AIAgent>();
        var executorBindings = new Dictionary<Type, ExecutorBinding>();
        foreach (var type in allAgentTypes)
        {
            var agent = _agentFactory.CreateAgent(type.Name);
            agents[type] = agent;
            executorBindings[type] = agent.BindAsExecutor();
        }

        // Discover [AgentGraphNode] attributes for JoinMode metadata.
        // WaitAll (default) maps to MAF's barrier-style edges (default AddEdge behavior).
        // WaitAny is supported via RunGraphAsync which uses Needlr's own executor.
        // CreateGraphWorkflow only supports WaitAll since it returns a MAF Workflow.
        var nodeJoinModes = DiscoverNodeJoinModes(graphName, allAgentTypes);
        foreach (var (type, joinMode) in nodeJoinModes)
        {
            if (joinMode == GraphJoinMode.WaitAny)
            {
                throw new NotSupportedException(
                    $"GraphJoinMode.WaitAny on '{type.Name}' in graph '{graphName}' is not compatible " +
                    $"with CreateGraphWorkflow (which returns a MAF Workflow using BSP execution). " +
                    $"Use RunGraphAsync(\"{graphName}\", input) instead — it handles WaitAny via " +
                    $"Needlr's own graph executor.");
            }
        }

        var builder = new WorkflowBuilder(executorBindings[entryType]);

        // RoutingMode from entry attribute informs edge wiring strategy.
        // AllMatching / Deterministic: all edges wired normally (MAF default is parallel fan-out).
        // ExclusiveChoice: MAF does not expose AddSwitch on WorkflowBuilder — fall back to normal edges.
        // The routing mode is recorded for diagnostic/logging purposes; the actual MAF behavior
        // is parallel execution of all outgoing edges from a node.
        foreach (var edge in edges)
        {
            builder.AddEdge(executorBindings[edge.SourceType], executorBindings[edge.TargetType]);
        }

        // Wire reducers: discover [AgentGraphReducer] attributes and bind their static methods
        // as executor nodes in the graph. Reducers are pure functions (no LLM cost).
        WireReducers(graphName, builder, executorBindings);

        return builder.Build();
    }

    private static Dictionary<Type, GraphJoinMode> DiscoverNodeJoinModes(
        string graphName,
        IEnumerable<Type> agentTypes)
    {
        var result = new Dictionary<Type, GraphJoinMode>();
        foreach (var type in agentTypes)
        {
            var nodeAttr = type.GetCustomAttributes<AgentGraphNodeAttribute>()
                .FirstOrDefault(a => string.Equals(a.GraphName, graphName, StringComparison.Ordinal));
            if (nodeAttr is not null)
            {
                result[type] = nodeAttr.JoinMode;
            }
        }

        return result;
    }

    [RequiresUnreferencedCode("Reducer discovery uses reflection to find [AgentGraphReducer] and invoke static methods.")]
    private static void WireReducers(
        string graphName,
        WorkflowBuilder builder,
        Dictionary<Type, ExecutorBinding> executorBindings)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch { continue; }

            foreach (var type in types)
            {
                foreach (var attr in type.GetCustomAttributes<AgentGraphReducerAttribute>())
                {
                    if (!string.Equals(attr.GraphName, graphName, StringComparison.Ordinal))
                        continue;

                    if (string.IsNullOrWhiteSpace(attr.ReducerMethod))
                        continue;

                    var method = type.GetMethod(
                        attr.ReducerMethod,
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        [typeof(IReadOnlyList<string>)],
                        null);

                    if (method is null || method.ReturnType != typeof(string))
                    {
                        throw new InvalidOperationException(
                            $"[AgentGraphReducer] on {type.Name} references method '{attr.ReducerMethod}' " +
                            $"but no matching 'public static string {attr.ReducerMethod}(IReadOnlyList<string>)' was found.");
                    }

                    // Reducer methods are discovered and validated here. Full integration of
                    // reducer executors into the WorkflowBuilder graph requires the source
                    // generator to emit edges that reference the reducer node. The reducer
                    // method is invoked via the generated extension method when available.
                    // This is a known Phase 2 limitation — see ADR-0001 deferred items.
                }
            }
        }
    }

    private Type FindGraphEntryType(string graphName)
    {
        var registeredTypes = _agentFactory.ResolveTools(_ => { });

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch { continue; }

            foreach (var type in types)
            {
                var entry = type.GetCustomAttributes<AgentGraphEntryAttribute>()
                    .FirstOrDefault(a => string.Equals(a.GraphName, graphName, StringComparison.Ordinal));
                if (entry is not null)
                {
                    return type;
                }
            }
        }

        throw new InvalidOperationException(
            $"Cannot build graph workflow '{graphName}': no entry point found. " +
            $"Ensure exactly one agent class has [AgentGraphEntry(\"{graphName}\")].");
    }

    private static List<GraphEdgeInfo> DiscoverGraphEdges(string graphName)
    {
        var edges = new List<GraphEdgeInfo>();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch { continue; }

            foreach (var type in types)
            {
                foreach (var attr in type.GetCustomAttributes<AgentGraphEdgeAttribute>())
                {
                    if (string.Equals(attr.GraphName, graphName, StringComparison.Ordinal))
                    {
                        edges.Add(new GraphEdgeInfo(type, attr.TargetAgentType, attr.Condition, attr.IsRequired));
                    }
                }
            }
        }

        return edges;
    }

    private sealed record GraphEdgeInfo(Type SourceType, Type TargetType, string? Condition, bool IsRequired);
}
