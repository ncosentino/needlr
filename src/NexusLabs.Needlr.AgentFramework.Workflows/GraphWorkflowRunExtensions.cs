using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text;

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Diagnostics;

using ProgressEvents = NexusLabs.Needlr.AgentFramework.Progress;

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
    /// emits <see cref="ProgressEvents.AgentInvokedEvent"/> and
    /// <see cref="ProgressEvents.AgentCompletedEvent"/> for each node, plus
    /// <see cref="ProgressEvents.WorkflowStartedEvent"/> and
    /// <see cref="ProgressEvents.WorkflowCompletedEvent"/> at workflow boundaries.
    /// </param>
    /// <param name="diagnosticsAccessor">
    /// Optional diagnostics accessor for capturing per-node token usage and LLM
    /// call details. Required for the WaitAll (MAF BSP) path to produce non-null
    /// <see cref="IDagNodeResult.Diagnostics"/>. The WaitAny path captures
    /// diagnostics via <see cref="AgentRunDiagnosticsBuilder"/> regardless.
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
        ProgressEvents.IProgressReporter? progress = null,
        IAgentDiagnosticsAccessor? diagnosticsAccessor = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(graphName);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var hasWaitAny = HasWaitAnyNodes(graphName);

        if (!hasWaitAny)
        {
            return await RunWaitAllWithDiagnosticsAsync(
                factory, graphName, input, progress, diagnosticsAccessor, cancellationToken);
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
        ProgressEvents.IProgressReporter? progress,
        IAgentDiagnosticsAccessor? diagnosticsAccessor,
        CancellationToken cancellationToken)
    {
        var workflow = factory.CreateGraphWorkflow(graphName);
        var topology = DiscoverTopology(graphName);
        var dagStart = Stopwatch.GetTimestamp();

        // Per-agent state accumulated from the MAF event stream.
        var responses = new Dictionary<string, StringBuilder>();
        var invocationTimestamps = new List<(string ExecutorId, DateTimeOffset At)>();
        bool succeeded = true;
        string? errorMessage = null;
        Exception? caughtException = null;

        // Drain stale diagnostics from previous runs.
        var collector = diagnosticsAccessor?.CompletionCollector;
        var toolCollector = diagnosticsAccessor?.ToolCallCollector;
        collector?.DrainCompletions();
        toolCollector?.DrainToolCalls();

        progress?.Report(new ProgressEvents.WorkflowStartedEvent(
            DateTimeOffset.UtcNow,
            progress.WorkflowId,
            progress.AgentId,
            null,
            progress.Depth,
            progress.NextSequence()));

        try
        {
            IDisposable? captureScope = diagnosticsAccessor?.BeginCapture();
            try
            {
                await using var run = await InProcessExecution.RunStreamingAsync(
                    workflow,
                    new ChatMessage(ChatRole.User, input),
                    cancellationToken: cancellationToken);

                await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

                await using var budgetReg = cancellationToken.CanBeCanceled
                    ? cancellationToken.Register(() => _ = run.CancelRunAsync())
                    : default(CancellationTokenRegistration?);

                await foreach (var evt in run.WatchStreamAsync(cancellationToken))
                {
                    if (evt is ExecutorInvokedEvent invoked)
                    {
                        var id = invoked.ExecutorId ?? "unknown";
                        invocationTimestamps.Add((id, DateTimeOffset.UtcNow));

                        progress?.Report(new ProgressEvents.AgentInvokedEvent(
                            DateTimeOffset.UtcNow,
                            progress.WorkflowId,
                            id,
                            null,
                            progress.Depth + 1,
                            progress.NextSequence(),
                            AgentName: id,
                            GraphName: graphName,
                            NodeId: id));
                        continue;
                    }

                    if (evt is ExecutorFailedEvent executorFailed)
                    {
                        succeeded = false;
                        errorMessage = executorFailed.Data?.Message;
                        var failedId = executorFailed.ExecutorId ?? "unknown";

                        progress?.Report(new ProgressEvents.AgentFailedEvent(
                            DateTimeOffset.UtcNow,
                            progress.WorkflowId,
                            failedId,
                            null,
                            progress.Depth + 1,
                            progress.NextSequence(),
                            AgentName: failedId,
                            ErrorMessage: executorFailed.Data?.Message ?? "unknown error"));
                        continue;
                    }

                    if (evt is WorkflowErrorEvent workflowError)
                    {
                        succeeded = false;
                        errorMessage = workflowError.Exception?.Message;
                        continue;
                    }

                    if (evt is not AgentResponseUpdateEvent update
                        || update.ExecutorId is null
                        || update.Data is null)
                    {
                        continue;
                    }

                    var text = update.Data.ToString();
                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    if (!responses.TryGetValue(update.ExecutorId, out var sb))
                    {
                        responses[update.ExecutorId] = sb = new StringBuilder();
                    }

                    sb.Append(text);
                }
            }
            finally
            {
                captureScope?.Dispose();
            }
        }
        catch (Exception ex)
        {
            succeeded = false;
            errorMessage = ex.Message;
            caughtException = ex;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var totalDuration = Stopwatch.GetElapsedTime(dagStart);

        // Drain completions and tool calls captured by middleware during BSP execution.
        var allCompletions = collector?.DrainCompletions()
            ?.OrderBy(c => c.StartedAt).ToList()
            ?? [];
        var allToolCalls = toolCollector?.DrainToolCalls()
            ?.OrderBy(t => t.StartedAt).ToList()
            ?? [];

        // Include agents that were invoked OR produced response text.
        var invokedIds = invocationTimestamps.Select(inv => inv.ExecutorId).ToHashSet();
        var respondedIds = responses.Keys.ToHashSet();
        var agentIds = invokedIds.Union(respondedIds).Distinct().ToList();

        // Partition completions by agent name, matching on the AgentName field
        // that the diagnostics middleware stamps onto each ChatCompletionDiagnostics.
        var completionsByAgent = new Dictionary<string, List<ChatCompletionDiagnostics>>();
        var toolCallsByAgent = new Dictionary<string, List<ToolCallDiagnostics>>();
        foreach (var id in agentIds)
        {
            completionsByAgent[id] = [];
            toolCallsByAgent[id] = [];
        }

        foreach (var c in allCompletions)
        {
            var matched = agentIds.FirstOrDefault(id =>
                c.AgentName is not null &&
                (id.Equals(c.AgentName, StringComparison.Ordinal) ||
                 id.StartsWith(c.AgentName + "_", StringComparison.Ordinal)));
            if (matched is not null)
            {
                completionsByAgent[matched].Add(c);
            }
        }

        foreach (var tc in allToolCalls)
        {
            var matched = agentIds.FirstOrDefault(id =>
                tc.AgentName is not null &&
                (id.Equals(tc.AgentName, StringComparison.Ordinal) ||
                 id.StartsWith(tc.AgentName + "_", StringComparison.Ordinal)));
            if (matched is not null)
            {
                toolCallsByAgent[matched].Add(tc);
            }
        }

        // Build per-node results.
        var nodeResults = new Dictionary<string, IDagNodeResult>();
        var stages = new List<IAgentStageResult>();
        var branchResults = new Dictionary<string, IReadOnlyList<IAgentStageResult>>();

        foreach (var agentId in agentIds)
        {
            var responseText = responses.TryGetValue(agentId, out var respSb)
                ? respSb.ToString()
                : string.Empty;

            ChatResponse? finalResponse = !string.IsNullOrEmpty(responseText)
                ? new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText))
                : null;

            var agentCompletions = completionsByAgent.GetValueOrDefault(agentId, []);
            var agentToolCalls = toolCallsByAgent.GetValueOrDefault(agentId, []);

            // Compute per-node timing from completion timestamps when available.
            TimeSpan nodeDuration;
            DateTimeOffset nodeStartedAt;
            if (agentCompletions.Count > 0)
            {
                nodeStartedAt = agentCompletions[0].StartedAt;
                nodeDuration = agentCompletions[^1].CompletedAt - nodeStartedAt;
            }
            else
            {
                // Fallback: use invocation event timestamp + aggregate duration estimate.
                var invTs = invocationTimestamps
                    .FirstOrDefault(x => x.ExecutorId == agentId).At;
                nodeStartedAt = invTs != default ? invTs : DateTimeOffset.UtcNow;
                nodeDuration = totalDuration / Math.Max(agentIds.Count, 1);
            }

            var dagStartTime = DateTimeOffset.UtcNow - totalDuration;
            var startOffset = nodeStartedAt - dagStartTime;
            if (startOffset < TimeSpan.Zero)
            {
                startOffset = TimeSpan.Zero;
            }

            var tokenUsage = new TokenUsage(
                InputTokens: agentCompletions.Sum(c => c.Tokens.InputTokens),
                OutputTokens: agentCompletions.Sum(c => c.Tokens.OutputTokens),
                TotalTokens: agentCompletions.Sum(c => c.Tokens.TotalTokens),
                CachedInputTokens: agentCompletions.Sum(c => c.Tokens.CachedInputTokens),
                ReasoningTokens: agentCompletions.Sum(c => c.Tokens.ReasoningTokens));

            IAgentRunDiagnostics diag = new AgentRunDiagnostics(
                AgentName: agentId,
                TotalDuration: nodeDuration,
                AggregateTokenUsage: tokenUsage,
                ChatCompletions: agentCompletions,
                ToolCalls: agentToolCalls,
                TotalInputMessages: 0,
                TotalOutputMessages: 0,
                InputMessages: [],
                OutputResponse: null,
                Succeeded: true,
                ErrorMessage: null,
                StartedAt: nodeStartedAt,
                CompletedAt: nodeStartedAt + nodeDuration);

            var nodeResult = new DagNodeResult(
                nodeId: agentId,
                agentName: agentId,
                kind: NodeKind.Agent,
                diagnostics: diag,
                finalResponse: finalResponse,
                inboundEdges: topology.InboundEdges.GetValueOrDefault(agentId, []),
                outboundEdges: topology.OutboundEdges.GetValueOrDefault(agentId, []),
                startOffset: startOffset,
                duration: nodeDuration);
            nodeResults[agentId] = nodeResult;

            var stageResult = new AgentStageResult(agentId, finalResponse, diag);
            stages.Add(stageResult);

            progress?.Report(new ProgressEvents.AgentCompletedEvent(
                DateTimeOffset.UtcNow,
                progress.WorkflowId,
                agentId,
                null,
                progress.Depth + 1,
                progress.NextSequence(),
                AgentName: agentId,
                Duration: nodeDuration,
                TotalTokens: tokenUsage.TotalTokens,
                InputTokens: tokenUsage.InputTokens,
                OutputTokens: tokenUsage.OutputTokens));
        }

        // Group parallel branches: nodes sharing the same inbound edge source.
        var branchIndex = 0;
        var nodesByInbound = stages
            .Where(s => nodeResults.ContainsKey(s.AgentName))
            .GroupBy(s => string.Join(",", nodeResults[s.AgentName].InboundEdges))
            .Where(g => g.Count() > 1);
        foreach (var group in nodesByInbound)
        {
            branchResults[$"branch-{branchIndex++}"] = group.ToList();
        }

        progress?.Report(new ProgressEvents.WorkflowCompletedEvent(
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
            stages: stages,
            nodeResults: nodeResults,
            branchResults: branchResults,
            totalDuration: totalDuration,
            succeeded: succeeded,
            errorMessage: errorMessage,
            exception: caughtException);
    }

    private static async Task<IDagRunResult> RunWithWaitAnyAsync(
        IWorkflowFactory factory,
        string graphName,
        string input,
        ProgressEvents.IProgressReporter? progress,
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

        progress?.Report(new ProgressEvents.WorkflowStartedEvent(
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

                    progress?.Report(new ProgressEvents.AgentInvokedEvent(
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
                    progress?.Report(new ProgressEvents.AgentCompletedEvent(
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

                    progress?.Report(new ProgressEvents.AgentFailedEvent(
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

        progress?.Report(new ProgressEvents.WorkflowCompletedEvent(
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
