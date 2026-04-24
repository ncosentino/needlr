using System.Collections.Concurrent;
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
                return _agentFactory.CreateAgent(t.FullName ?? t.Name, opts => configureAgent(t, opts));
            return _agentFactory.CreateAgent(t.FullName ?? t.Name);
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
        var agents = memberTypes.Select(t => _agentFactory.CreateAgent(t.FullName ?? t.Name)).ToArray();
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
            .Select(t => (_agentFactory.CreateAgent(t.TargetType.FullName ?? t.TargetType.Name), t.HandoffReason))
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
            var agent = _agentFactory.CreateAgent(type.FullName ?? type.Name);
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
                    $"GraphJoinMode.WaitAny on '{type.FullName ?? type.Name}' in graph '{graphName}' is not compatible " +
                    $"with CreateGraphWorkflow (which returns a MAF Workflow using BSP execution). " +
                    $"Use RunGraphAsync(\"{graphName}\", input) instead — it handles WaitAny via " +
                    $"Needlr's own graph executor.");
            }
        }

        var builder = new WorkflowBuilder(executorBindings[entryType]);

        // Discover reducer bindings before wiring edges so fan-in edges can be
        // routed through the reducer FunctionExecutor node.
        var reducerBinding = DiscoverReducerBinding(graphName);

        // Identify fan-in targets (agent types with two or more incoming edges)
        // so their inbound edges can be redirected through the reducer.
        var fanInSources = new Dictionary<Type, List<Type>>();
        foreach (var edge in edges)
        {
            if (!fanInSources.TryGetValue(edge.TargetType, out var sources))
            {
                sources = [];
                fanInSources[edge.TargetType] = sources;
            }

            sources.Add(edge.SourceType);
        }

        var fanInTargets = reducerBinding is not null
            ? fanInSources
                .Where(kv => kv.Value.Count >= 2)
                .Select(kv => kv.Key)
                .ToHashSet()
            : [];

        // Compute effective routing mode per source node: per-node override wins,
        // then graph-wide default from the entry attribute.
        var graphRoutingMode = entryAttr.RoutingMode;
        var edgesBySource = edges.GroupBy(e => e.SourceType).ToDictionary(g => g.Key, g => g.ToList());
        var effectiveRoutingModes = new Dictionary<Type, GraphRoutingMode>();
        foreach (var (sourceType, sourceEdges) in edgesBySource)
        {
            var nodeOverride = sourceEdges
                .Select(e => e.NodeRoutingModeOverride)
                .FirstOrDefault(m => m is not null);
            effectiveRoutingModes[sourceType] = nodeOverride ?? graphRoutingMode;
        }

        // Validate: LlmChoice is not supported in the BSP path — it requires
        // async LLM calls that CreateGraphWorkflow (synchronous build) cannot provide.
        foreach (var (sourceType, routingMode) in effectiveRoutingModes)
        {
            if (routingMode == GraphRoutingMode.LlmChoice)
            {
                throw new NotSupportedException(
                    $"GraphRoutingMode.LlmChoice on '{sourceType.FullName ?? sourceType.Name}' in graph '{graphName}' is not compatible " +
                    $"with CreateGraphWorkflow (which returns a MAF Workflow using BSP execution). " +
                    $"Use RunGraphAsync(\"{graphName}\", input) instead — it handles LlmChoice via " +
                    $"Needlr's own graph executor with an IChatClient.");
            }
        }

        if (reducerBinding is not null && fanInTargets.Count > 0)
        {
            builder.BindExecutor(reducerBinding);
            var wiredFanInTargets = new HashSet<Type>();

            foreach (var edge in edges)
            {
                if (fanInTargets.Contains(edge.TargetType))
                {
                    // Redirect fan-in edges through the reducer function node:
                    // source → reducer (instead of source → fan-in agent)
                    AddRoutedEdge(builder, executorBindings[edge.SourceType], reducerBinding,
                        edge, effectiveRoutingModes, edgesBySource);

                    // reducer → original fan-in agent (wired once per target)
                    if (wiredFanInTargets.Add(edge.TargetType))
                    {
                        builder.AddEdge(reducerBinding, executorBindings[edge.TargetType]);
                    }
                }
                else
                {
                    AddRoutedEdge(builder, executorBindings[edge.SourceType], executorBindings[edge.TargetType],
                        edge, effectiveRoutingModes, edgesBySource);
                }
            }
        }
        else
        {
            foreach (var edge in edges)
            {
                AddRoutedEdge(builder, executorBindings[edge.SourceType], executorBindings[edge.TargetType],
                    edge, effectiveRoutingModes, edgesBySource);
            }
        }

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

    /// <summary>
    /// Discovers a single <see cref="AgentGraphReducerAttribute"/> for the graph and
    /// creates a <see cref="FunctionExecutor{TInput, TOutput}"/> wrapped in an
    /// <see cref="ExecutorBinding"/> so it can participate as a node in the
    /// <see cref="WorkflowBuilder"/> DAG.
    /// </summary>
    /// <returns>
    /// An <see cref="ExecutorBinding"/> for the reducer, or <c>null</c> if no reducer
    /// is declared for this graph.
    /// </returns>
    [RequiresUnreferencedCode("Reducer discovery uses reflection to find [AgentGraphReducer] and invoke static methods.")]
    private static ExecutorBinding? DiscoverReducerBinding(string graphName)
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
                            $"[AgentGraphReducer] on {type.FullName ?? type.Name} references method '{attr.ReducerMethod}' " +
                            $"but no matching 'public static string {attr.ReducerMethod}(IReadOnlyList<string>)' was found.");
                    }

                    return CreateReducerExecutorBinding(type, method);
                }
            }
        }

        return null;
    }

    private static ExecutorBinding CreateReducerExecutorBinding(Type reducerType, MethodInfo reducerMethod)
    {
        var reducerId = $"reducer:{reducerType.FullName ?? reducerType.Name}";

        // The FunctionExecutor receives a string input per invocation. In the
        // BSP model each superstep delivers one message. The reducer is invoked
        // with a single-element list per call — the downstream agent sees the
        // reduced output from each branch independently. No shared mutable
        // state is needed because each invocation is self-contained.
        var executor = new FunctionExecutor<string, string>(
            reducerId,
            (input, _, _) =>
            {
                var inputs = new List<string> { input };
                var result = (string)reducerMethod.Invoke(
                    null,
                    [inputs.AsReadOnly()])!;
                return new ValueTask<string>(result);
            },
            options: null,
            sentMessageTypes: null,
            outputTypes: null,
            declareCrossRunShareable: false);

        return new ExecutorInstanceBinding(executor);
    }

    private Type FindGraphEntryType(string graphName)
    {
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
                        edges.Add(new GraphEdgeInfo(
                            type,
                            attr.TargetAgentType,
                            attr.Condition,
                            attr.IsRequired,
                            attr.HasNodeRoutingMode ? attr.NodeRoutingMode : null));
                    }
                }
            }
        }

        return edges;
    }

    /// <summary>
    /// Wires a single edge into the <see cref="WorkflowBuilder"/>, applying
    /// condition functions according to the effective routing mode for the
    /// source node.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="GraphEdgeInfo.IsRequired"/> is intentionally NOT wired in the BSP path.
    /// MAF's <see cref="WorkflowBuilder.AddEdge(ExecutorBinding, ExecutorBinding)"/> API
    /// has no concept of optional vs. required edges — all edges are implicitly required.
    /// The <c>IsRequired</c> semantic is a Needlr-native-executor-only feature handled by
    /// <c>RunGraphAsync</c>, which uses Needlr's own graph executor.
    /// </para>
    /// </remarks>
    private static void AddRoutedEdge(
        WorkflowBuilder builder,
        ExecutorBinding source,
        ExecutorBinding target,
        GraphEdgeInfo edge,
        Dictionary<Type, GraphRoutingMode> effectiveRoutingModes,
        Dictionary<Type, List<GraphEdgeInfo>> edgesBySource)
    {
        var routingMode = effectiveRoutingModes.GetValueOrDefault(edge.SourceType, GraphRoutingMode.Deterministic);

        if (edge.Condition is null)
        {
            builder.AddEdge(source, target);
            return;
        }

        switch (routingMode)
        {
            case GraphRoutingMode.Deterministic:
            case GraphRoutingMode.AllMatching:
                builder.AddEdge<object>(source, target,
                    input => EvaluateEdgeCondition(edge.SourceType, edge.Condition, input));
                break;

            case GraphRoutingMode.FirstMatching:
            {
                var sourceEdges = edgesBySource[edge.SourceType];
                var edgeIndex = sourceEdges.IndexOf(edge);
                var precedingConditionalEdges = sourceEdges
                    .Take(edgeIndex)
                    .Where(e => e.Condition is not null)
                    .ToList();

                builder.AddEdge<object>(source, target, input =>
                {
                    // Only follow this edge if its condition passes AND
                    // no earlier conditional edge's condition passed.
                    if (!EvaluateEdgeCondition(edge.SourceType, edge.Condition, input))
                        return false;

                    foreach (var earlier in precedingConditionalEdges)
                    {
                        if (EvaluateEdgeCondition(edge.SourceType, earlier.Condition!, input))
                            return false;
                    }

                    return true;
                });
                break;
            }

            case GraphRoutingMode.ExclusiveChoice:
            {
                var sourceEdges = edgesBySource[edge.SourceType];
                builder.AddEdge<object>(source, target, input =>
                {
                    var matchCount = 0;
                    var thisMatches = false;

                    foreach (var e in sourceEdges)
                    {
                        if (e.Condition is null)
                            continue;
                        if (EvaluateEdgeCondition(edge.SourceType, e.Condition, input))
                        {
                            matchCount++;
                            if (ReferenceEquals(e, edge))
                                thisMatches = true;
                        }
                    }

                    if (matchCount == 0)
                    {
                        throw new InvalidOperationException(
                            $"ExclusiveChoice routing on '{edge.SourceType.Name}': no edge condition matched. " +
                            $"Exactly one must match.");
                    }

                    if (matchCount > 1)
                    {
                        var names = string.Join(", ", sourceEdges
                            .Where(e => e.Condition is not null && EvaluateEdgeCondition(edge.SourceType, e.Condition, input))
                            .Select(e => e.TargetType.Name));
                        throw new InvalidOperationException(
                            $"ExclusiveChoice routing on '{edge.SourceType.Name}': {matchCount} edges matched " +
                            $"({names}). Exactly one must match.");
                    }

                    return thisMatches;
                });
                break;
            }

            default:
                builder.AddEdge(source, target);
                break;
        }
    }

    /// <summary>
    /// Evaluates a condition string by looking up a static method on the source
    /// agent type that accepts <c>object?</c> and returns <c>bool</c>.
    /// </summary>
    [RequiresUnreferencedCode("Condition evaluation uses reflection to invoke static predicate methods on agent types.")]
    private static bool EvaluateEdgeCondition(Type sourceType, string conditionMethodName, object? upstreamOutput)
    {
        var method = sourceType.GetMethod(
            conditionMethodName,
            BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic,
            null,
            [typeof(object)],
            null);

        if (method is null)
        {
            method = sourceType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == conditionMethodName && m.GetParameters().Length == 1);
        }

        if (method is null || method.ReturnType != typeof(bool))
        {
            throw new InvalidOperationException(
                $"Condition '{conditionMethodName}' on '{sourceType.Name}' must be a static method " +
                $"with signature 'static bool {conditionMethodName}(object? upstreamOutput)'.");
        }

        return (bool)method.Invoke(null, [upstreamOutput])!;
    }

    private sealed record GraphEdgeInfo(Type SourceType, Type TargetType, string? Condition, bool IsRequired, GraphRoutingMode? NodeRoutingModeOverride);
}
