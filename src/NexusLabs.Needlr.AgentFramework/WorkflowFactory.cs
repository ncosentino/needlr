using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

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
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);

        var memberTypes = ResolveGroupChatMembers(groupName);

        if (memberTypes.Count < 2)
        {
            throw new InvalidOperationException(
                $"CreateGroupChatWorkflow(\"{groupName}\") failed: {memberTypes.Count} agent(s) are " +
                $"registered as members of group \"{groupName}\". At least two are required. " +
                $"Add [AgentGroupChatMember(\"{groupName}\")] to the agent classes and ensure their " +
                $"assemblies are scanned.");
        }

        var agents = memberTypes.Select(t => _agentFactory.CreateAgent(t.Name)).ToList();

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
            var responseText = lastMessage.Text ?? string.Empty;
            var ctx = new TerminationContext
            {
                AgentId = agentId,
                ResponseText = responseText,
                TurnCount = historyList.Count,
                ConversationHistory = historyList,
            };

            foreach (var (_, condition) in conditions)
            {
                if (condition.ShouldTerminate(ctx))
                    return ValueTask.FromResult(true);
            }

            return ValueTask.FromResult(false);
        };
    }
}
