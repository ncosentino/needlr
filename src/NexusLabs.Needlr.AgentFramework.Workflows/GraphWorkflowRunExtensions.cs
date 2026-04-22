using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Progress;

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
    /// <param name="progress">
    /// Optional progress reporter for real-time execution events. When provided,
    /// emits <see cref="AgentInvokedEvent"/> and <see cref="AgentCompletedEvent"/>
    /// for each node, plus <see cref="WorkflowStartedEvent"/> and
    /// <see cref="WorkflowCompletedEvent"/> at workflow boundaries.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// An <see cref="IDagRunResult"/> containing per-node diagnostics, edge metadata,
    /// timing offsets, aggregate token usage, and backward-compatible
    /// <see cref="IPipelineRunResult.Stages"/>.
    /// </returns>
    public static async Task<IDagRunResult> RunGraphAsync(
        this IWorkflowFactory factory,
        string graphName,
        string input,
        IProgressReporter? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(graphName);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var hasWaitAny = HasWaitAnyNodes(graphName);

        if (!hasWaitAny)
        {
            return await RunWaitAllWithDiagnosticsAsync(
                factory, graphName, input, progress, cancellationToken);
        }

        return await RunWithWaitAnyAsync(
            factory, graphName, input, progress, cancellationToken);
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

    private static async Task<IDagRunResult> RunWaitAllWithDiagnosticsAsync(
        IWorkflowFactory factory,
        string graphName,
        string input,
        IProgressReporter? progress,
        CancellationToken cancellationToken)
    {
        var workflow = factory.CreateGraphWorkflow(graphName);
        var dagStart = Stopwatch.GetTimestamp();

        progress?.Report(new WorkflowStartedEvent(
            DateTimeOffset.UtcNow,
            progress.WorkflowId,
            progress.AgentId,
            null,
            progress.Depth,
            progress.NextSequence()));

        var responses = await workflow.RunAsync(input, cancellationToken: cancellationToken);

        var totalDuration = Stopwatch.GetElapsedTime(dagStart);

        // In the WaitAll BSP path we don't have per-node timing, so we build
        // minimal DagNodeResult entries from the response dictionary.
        var topology = DiscoverTopology(graphName);
        var nodeResults = new Dictionary<string, IDagNodeResult>();
        var stages = new List<IAgentStageResult>();

        foreach (var (agentName, text) in responses)
        {
            var nodeResult = new DagNodeResult(
                nodeId: agentName,
                agentName: agentName,
                kind: NodeKind.Agent,
                diagnostics: null,
                finalResponse: null,
                inboundEdges: topology.InboundEdges.GetValueOrDefault(agentName, []),
                outboundEdges: topology.OutboundEdges.GetValueOrDefault(agentName, []),
                startOffset: TimeSpan.Zero,
                duration: totalDuration);
            nodeResults[agentName] = nodeResult;
            stages.Add(new AgentStageResult(agentName, null, null));
        }

        progress?.Report(new WorkflowCompletedEvent(
            DateTimeOffset.UtcNow,
            progress.WorkflowId,
            progress.AgentId,
            null,
            progress.Depth,
            progress.NextSequence(),
            Succeeded: true,
            ErrorMessage: null,
            TotalDuration: totalDuration));

        return new DagRunResult(
            stages: stages,
            nodeResults: nodeResults,
            branchResults: new Dictionary<string, IReadOnlyList<IAgentStageResult>>(),
            totalDuration: totalDuration,
            succeeded: true,
            errorMessage: null);
    }

    private static async Task<IDagRunResult> RunWithWaitAnyAsync(
        IWorkflowFactory factory,
        string graphName,
        string input,
        IProgressReporter? progress,
        CancellationToken cancellationToken)
    {
        var topology = DiscoverTopology(graphName);

        if (topology.EntryType is null)
        {
            throw new InvalidOperationException(
                $"Cannot run graph workflow '{graphName}': no entry point found.");
        }

        var agentFactory = GetAgentFactory(factory);

        var agents = new Dictionary<Type, AIAgent>();
        foreach (var type in topology.AllTypes)
        {
            agents[type] = agentFactory.CreateAgent(type.Name);
        }

        var completionSources = new Dictionary<Type, TaskCompletionSource<string>>();
        foreach (var type in topology.AllTypes)
        {
            completionSources[type] = new TaskCompletionSource<string>();
        }

        var dagStart = Stopwatch.GetTimestamp();
        var nodeTimings = new ConcurrentDictionary<Type, (TimeSpan StartOffset, TimeSpan Duration)>();
        var nodeDiagnostics = new ConcurrentDictionary<Type, IAgentRunDiagnostics?>();
        var nodeExceptions = new ConcurrentDictionary<Type, Exception>();

        progress?.Report(new WorkflowStartedEvent(
            DateTimeOffset.UtcNow,
            progress?.WorkflowId ?? string.Empty,
            progress?.AgentId,
            null,
            progress?.Depth ?? 0,
            progress?.NextSequence() ?? 0));

        var nodeTasks = new List<Task>();

        foreach (var type in topology.AllTypes)
        {
            var nodeType = type;
            var deps = topology.IncomingTypes.GetValueOrDefault(nodeType, []);
            var joinMode = topology.JoinModes.GetValueOrDefault(nodeType, GraphJoinMode.WaitAll);

            nodeTasks.Add(Task.Run(async () =>
            {
                try
                {
                    string nodeInput;
                    if (nodeType == topology.EntryType)
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
                    var agentName = agent.Name ?? nodeType.Name;

                    progress?.Report(new AgentInvokedEvent(
                        DateTimeOffset.UtcNow,
                        progress.WorkflowId,
                        agentName,
                        null,
                        progress.Depth + 1,
                        progress.NextSequence(),
                        AgentName: agentName,
                        GraphName: graphName,
                        NodeId: nodeType.Name));

                    var nodeStart = Stopwatch.GetTimestamp();
                    using var diagnosticsBuilder = AgentRunDiagnosticsBuilder.StartNew(agentName);
                    var response = await agent.RunAsync(nodeInput, cancellationToken: cancellationToken);
                    var nodeElapsed = Stopwatch.GetElapsedTime(nodeStart);
                    var startOffset = Stopwatch.GetElapsedTime(dagStart, nodeStart);

                    var diag = diagnosticsBuilder.Build();
                    nodeDiagnostics[nodeType] = diag;
                    nodeTimings[nodeType] = (startOffset, nodeElapsed);

                    var text = string.Join("\n", response.Messages
                        .Where(m => !string.IsNullOrEmpty(m.Text))
                        .Select(m => m.Text));

                    var totalTokens = diag.AggregateTokenUsage.TotalTokens;
                    progress?.Report(new AgentCompletedEvent(
                        DateTimeOffset.UtcNow,
                        progress.WorkflowId,
                        agentName,
                        null,
                        progress.Depth + 1,
                        progress.NextSequence(),
                        AgentName: agentName,
                        Duration: nodeElapsed,
                        TotalTokens: totalTokens,
                        InputTokens: diag.AggregateTokenUsage.InputTokens,
                        OutputTokens: diag.AggregateTokenUsage.OutputTokens));

                    completionSources[nodeType].TrySetResult(text);
                }
                catch (Exception ex)
                {
                    var agentName = agents.TryGetValue(nodeType, out var a)
                        ? a.Name ?? nodeType.Name
                        : nodeType.Name;

                    nodeExceptions[nodeType] = ex;

                    progress?.Report(new AgentFailedEvent(
                        DateTimeOffset.UtcNow,
                        progress.WorkflowId,
                        agentName,
                        null,
                        progress.Depth + 1,
                        progress.NextSequence(),
                        AgentName: agentName,
                        ErrorMessage: ex.Message));

                    completionSources[nodeType].TrySetException(ex);
                }
            }, cancellationToken));
        }

        Exception? dagException = null;
        bool succeeded;
        string? errorMessage = null;

        try
        {
            await Task.WhenAll(nodeTasks).WaitAsync(cancellationToken);
            succeeded = nodeExceptions.IsEmpty;
            if (!succeeded)
            {
                var firstError = nodeExceptions.Values.First();
                errorMessage = firstError.Message;
                dagException = new AggregateException(nodeExceptions.Values);
            }
        }
        catch (Exception ex)
        {
            succeeded = false;
            errorMessage = ex.Message;
            dagException = ex;
        }

        var totalDuration = Stopwatch.GetElapsedTime(dagStart);

        // Build per-node results.
        var nodeResultsDict = new Dictionary<string, IDagNodeResult>();
        var stagesList = new List<IAgentStageResult>();

        foreach (var type in topology.AllTypes)
        {
            var agentName = agents.TryGetValue(type, out var ag)
                ? ag.Name ?? type.Name
                : type.Name;
            var (startOffset, duration) = nodeTimings.GetValueOrDefault(type, (TimeSpan.Zero, TimeSpan.Zero));
            var diag = nodeDiagnostics.GetValueOrDefault(type);

            ChatResponse? finalResponse = null;
            if (completionSources[type].Task.IsCompletedSuccessfully)
            {
                var text = completionSources[type].Task.Result;
                finalResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
            }

            var nodeResult = new DagNodeResult(
                nodeId: type.Name,
                agentName: agentName,
                kind: NodeKind.Agent,
                diagnostics: diag,
                finalResponse: finalResponse,
                inboundEdges: topology.InboundEdges.GetValueOrDefault(agentName, []),
                outboundEdges: topology.OutboundEdges.GetValueOrDefault(agentName, []),
                startOffset: startOffset,
                duration: duration);

            nodeResultsDict[type.Name] = nodeResult;
            stagesList.Add(new AgentStageResult(agentName, finalResponse, diag));
        }

        // Group parallel branches: nodes sharing the same set of inbound edges.
        var branchResults = new Dictionary<string, IReadOnlyList<IAgentStageResult>>();
        var branchIndex = 0;
        var nodesByInbound = stagesList
            .GroupBy(s => string.Join(",",
                (nodeResultsDict.TryGetValue(
                    topology.AllTypes.FirstOrDefault(t =>
                        (agents.TryGetValue(t, out var a2) ? a2.Name ?? t.Name : t.Name) == s.AgentName)?.Name ?? s.AgentName,
                    out var nr)
                    ? nr.InboundEdges
                    : Array.Empty<string>())))
            .Where(g => g.Count() > 1);
        foreach (var group in nodesByInbound)
        {
            branchResults[$"branch-{branchIndex++}"] = group.ToList();
        }

        progress?.Report(new WorkflowCompletedEvent(
            DateTimeOffset.UtcNow,
            progress.WorkflowId,
            progress.AgentId,
            null,
            progress.Depth,
            progress.NextSequence(),
            Succeeded: succeeded,
            ErrorMessage: errorMessage,
            TotalDuration: totalDuration));

        return new DagRunResult(
            stages: stagesList,
            nodeResults: nodeResultsDict,
            branchResults: branchResults,
            totalDuration: totalDuration,
            succeeded: succeeded,
            errorMessage: errorMessage,
            exception: dagException);
    }

    private static GraphTopology DiscoverTopology(string graphName)
    {
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

        // Build edge maps keyed by agent name (Type.Name).
        var incomingTypes = new Dictionary<Type, List<Type>>();
        var inboundEdges = new Dictionary<string, List<string>>();
        var outboundEdges = new Dictionary<string, List<string>>();

        foreach (var type in allTypes)
        {
            incomingTypes[type] = [];
            inboundEdges[type.Name] = [];
            outboundEdges[type.Name] = [];
        }

        foreach (var (source, target) in edges)
        {
            incomingTypes[target].Add(source);
            inboundEdges[target.Name].Add(source.Name);
            outboundEdges[source.Name].Add(target.Name);
        }

        return new GraphTopology(
            entryType,
            allTypes,
            joinModes,
            incomingTypes,
            inboundEdges.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value),
            outboundEdges.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value));
    }

    private static IAgentFactory GetAgentFactory(IWorkflowFactory factory)
    {
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

    private sealed record GraphTopology(
        Type? EntryType,
        HashSet<Type> AllTypes,
        Dictionary<Type, GraphJoinMode> JoinModes,
        Dictionary<Type, List<Type>> IncomingTypes,
        Dictionary<string, IReadOnlyList<string>> InboundEdges,
        Dictionary<string, IReadOnlyList<string>> OutboundEdges);
}
