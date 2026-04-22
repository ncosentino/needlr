using System.Reflection;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework;

namespace NexusLabs.Needlr.AgentFramework.Workflows;

/// <summary>
/// Extension methods for running DAG graph workflows with support for both
/// <see cref="GraphJoinMode.WaitAll"/> (MAF-native BSP) and
/// <see cref="GraphJoinMode.WaitAny"/> (Needlr-native executor).
/// </summary>
public static class GraphWorkflowRunExtensions
{
    /// <summary>
    /// Executes a graph/DAG workflow, choosing the execution strategy based on
    /// the declared <see cref="GraphJoinMode"/> values. Graphs with only WaitAll
    /// nodes use MAF's native BSP executor. Graphs containing any WaitAny node
    /// use Needlr's own executor with <see cref="Task.WhenAny(Task[])"/>.
    /// </summary>
    /// <param name="factory">The workflow factory.</param>
    /// <param name="graphName">The graph name (case-sensitive).</param>
    /// <param name="input">The input message to send to the entry node.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Per-node responses keyed by agent name.</returns>
    public static async Task<IReadOnlyDictionary<string, string>> RunGraphAsync(
        this IWorkflowFactory factory,
        string graphName,
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(graphName);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var hasWaitAny = HasWaitAnyNodes(graphName);

        if (!hasWaitAny)
        {
            var workflow = factory.CreateGraphWorkflow(graphName);
            return await workflow.RunAsync(input, cancellationToken: cancellationToken);
        }

        return await RunWithWaitAnyAsync(factory, graphName, input, cancellationToken);
    }

    private static bool HasWaitAnyNodes(string graphName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch { continue; }

            foreach (var type in types)
            {
                foreach (var attr in type.GetCustomAttributes<AgentGraphNodeAttribute>())
                {
                    if (string.Equals(attr.GraphName, graphName, StringComparison.Ordinal) &&
                        attr.JoinMode == GraphJoinMode.WaitAny)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static async Task<IReadOnlyDictionary<string, string>> RunWithWaitAnyAsync(
        IWorkflowFactory factory,
        string graphName,
        string input,
        CancellationToken cancellationToken)
    {
        // Discover the graph topology from attributes.
        Type? entryType = null;
        var edges = new List<(Type Source, Type Target)>();
        var joinModes = new Dictionary<Type, GraphJoinMode>();
        var allTypes = new HashSet<Type>();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch { continue; }

            foreach (var type in types)
            {
                foreach (var attr in type.GetCustomAttributes<AgentGraphEntryAttribute>())
                {
                    if (string.Equals(attr.GraphName, graphName, StringComparison.Ordinal))
                    {
                        entryType = type;
                        allTypes.Add(type);
                    }
                }

                foreach (var attr in type.GetCustomAttributes<AgentGraphEdgeAttribute>())
                {
                    if (string.Equals(attr.GraphName, graphName, StringComparison.Ordinal))
                    {
                        edges.Add((type, attr.TargetAgentType));
                        allTypes.Add(type);
                        allTypes.Add(attr.TargetAgentType);
                    }
                }

                foreach (var attr in type.GetCustomAttributes<AgentGraphNodeAttribute>())
                {
                    if (string.Equals(attr.GraphName, graphName, StringComparison.Ordinal))
                    {
                        joinModes[type] = attr.JoinMode;
                    }
                }
            }
        }

        if (entryType is null)
        {
            throw new InvalidOperationException(
                $"Cannot run graph workflow '{graphName}': no entry point found.");
        }

        // Create agents via the factory's agent resolution.
        // We need an IAgentFactory — get it by resolving tools through the workflow factory
        // and then creating agents by name. The simplest path: use reflection to access
        // the factory's _agentFactory field, or create agents via the factory's
        // CreateGraphWorkflow path. For now, use a simpler approach: resolve agents by name.
        var agentFactory = GetAgentFactory(factory);

        var agents = new Dictionary<Type, AIAgent>();
        foreach (var type in allTypes)
        {
            agents[type] = agentFactory.CreateAgent(type.Name);
        }

        // Build incoming edges per node.
        var incoming = new Dictionary<Type, List<Type>>();
        foreach (var type in allTypes)
        {
            incoming[type] = [];
        }
        foreach (var (source, target) in edges)
        {
            incoming[target].Add(source);
        }

        // Each node gets a TaskCompletionSource for dependency tracking.
        var completionSources = new Dictionary<Type, TaskCompletionSource<string>>();
        foreach (var type in allTypes)
        {
            completionSources[type] = new TaskCompletionSource<string>();
        }

        var nodeTasks = new List<Task>();

        foreach (var type in allTypes)
        {
            var nodeType = type;
            var deps = incoming[nodeType];
            var joinMode = joinModes.GetValueOrDefault(nodeType, GraphJoinMode.WaitAll);

            nodeTasks.Add(Task.Run(async () =>
            {
                try
                {
                    string nodeInput;
                    if (nodeType == entryType)
                    {
                        nodeInput = input;
                    }
                    else if (deps.Count == 0)
                    {
                        nodeInput = input;
                    }
                    else if (joinMode == GraphJoinMode.WaitAny)
                    {
                        var depTasks = deps.Select(d => completionSources[d].Task).ToArray();
                        var first = await Task.WhenAny(depTasks).WaitAsync(cancellationToken);
                        nodeInput = await first;
                    }
                    else
                    {
                        var depTasks = deps.Select(d => completionSources[d].Task).ToArray();
                        var results = await Task.WhenAll(depTasks).WaitAsync(cancellationToken);
                        nodeInput = string.Join("\n\n---\n\n", results);
                    }

                    var agent = agents[nodeType];
                    var response = await agent.RunAsync(nodeInput, cancellationToken: cancellationToken);
                    var text = string.Join("\n", response.Messages
                        .Where(m => !string.IsNullOrEmpty(m.Text))
                        .Select(m => m.Text));
                    completionSources[nodeType].TrySetResult(text);
                }
                catch (Exception ex)
                {
                    completionSources[nodeType].TrySetException(ex);
                }
            }, cancellationToken));
        }

        await Task.WhenAll(nodeTasks).WaitAsync(cancellationToken);

        var outputs = new Dictionary<string, string>();
        foreach (var (type, tcs) in completionSources)
        {
            if (tcs.Task.IsCompletedSuccessfully)
            {
                outputs[agents[type].Name ?? type.Name] = await tcs.Task;
            }
        }
        return outputs;
    }

    private static IAgentFactory GetAgentFactory(IWorkflowFactory factory)
    {
        // WorkflowFactory holds an IAgentFactory. Access it to create agents
        // for the Needlr-native executor path.
        var field = factory.GetType().GetField(
            "_agentFactory",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field?.GetValue(factory) is IAgentFactory agentFactory)
        {
            return agentFactory;
        }

        throw new InvalidOperationException(
            "Cannot resolve IAgentFactory from the workflow factory. " +
            "RunGraphAsync with WaitAny requires a WorkflowFactory backed by an IAgentFactory.");
    }
}
