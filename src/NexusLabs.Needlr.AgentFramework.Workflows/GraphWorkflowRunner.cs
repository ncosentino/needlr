using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Iterative;

using ProgressEvents = NexusLabs.Needlr.AgentFramework.Progress;

namespace NexusLabs.Needlr.AgentFramework.Workflows;

/// <summary>
/// Executes DAG/graph workflows using either MAF's native BSP engine or the
/// Needlr-native executor, depending on declared topology.
/// </summary>
/// <remarks>
/// All dependencies are resolved via DI — no reflection into private fields.
/// </remarks>
internal sealed class GraphWorkflowRunner : IGraphWorkflowRunner
{
    private readonly IWorkflowFactory _workflowFactory;
    private readonly IAgentFactory _agentFactory;
    private readonly IChatClientAccessor _chatClientAccessor;
    private readonly IAgentDiagnosticsAccessor? _diagnosticsAccessor;
    private readonly GraphTopologyProvider _topologyProvider;
    private readonly GraphEdgeRouter _edgeRouter;

    public GraphWorkflowRunner(
        IWorkflowFactory workflowFactory,
        IAgentFactory agentFactory,
        IChatClientAccessor chatClientAccessor,
        GraphTopologyProvider topologyProvider,
        GraphEdgeRouter edgeRouter,
        IAgentDiagnosticsAccessor? diagnosticsAccessor = null)
    {
        _workflowFactory = workflowFactory;
        _agentFactory = agentFactory;
        _chatClientAccessor = chatClientAccessor;
        _topologyProvider = topologyProvider;
        _edgeRouter = edgeRouter;
        _diagnosticsAccessor = diagnosticsAccessor;
    }

    public async Task<IDagRunResult> RunGraphAsync(
        string graphName,
        string input,
        ProgressEvents.IProgressReporter? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(graphName);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var topology = _topologyProvider.GetTopology(graphName);

        if (!topology.RequiresNeedlrExecutor)
        {
            return await RunWaitAllWithDiagnosticsAsync(
                topology, graphName, input, progress, cancellationToken);
        }

        return await RunWithNeedlrExecutorAsync(
            topology, graphName, input, progress, cancellationToken);
    }

    private async Task<IDagRunResult> RunWaitAllWithDiagnosticsAsync(
        GraphTopology topology,
        string graphName,
        string input,
        ProgressEvents.IProgressReporter? progress,
        CancellationToken cancellationToken)
    {
        var workflow = _workflowFactory.CreateGraphWorkflow(graphName);
        var dagStart = Stopwatch.GetTimestamp();

        var responses = new Dictionary<string, StringBuilder>();
        var invocationTimestamps = new List<(string ExecutorId, DateTimeOffset At)>();
        bool succeeded = true;
        string? errorMessage = null;
        Exception? caughtException = null;

        var collector = _diagnosticsAccessor?.CompletionCollector;
        var toolCollector = _diagnosticsAccessor?.ToolCallCollector;
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
            IDisposable? captureScope = _diagnosticsAccessor?.BeginCapture();
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

                    progress?.Report(new ProgressEvents.AgentResponseChunkEvent(
                        DateTimeOffset.UtcNow,
                        progress.WorkflowId,
                        update.ExecutorId,
                        null,
                        progress.Depth + 1,
                        progress.NextSequence(),
                        AgentName: update.ExecutorId,
                        Text: text));
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

        var allCompletions = collector?.DrainCompletions()
            ?.OrderBy(c => c.StartedAt).ToList()
            ?? [];
        var allToolCalls = toolCollector?.DrainToolCalls()
            ?.OrderBy(t => t.StartedAt).ToList()
            ?? [];

        var invokedIds = invocationTimestamps.Select(inv => inv.ExecutorId).ToHashSet();
        var respondedIds = responses.Keys.ToHashSet();
        var agentIds = invokedIds.Union(respondedIds).Distinct().ToList();

        // Build a mapping from agent IDs (executor IDs from MAF) to their
        // corresponding Type for namespace-safe edge lookups.
        var agentIdToType = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var id in agentIds)
        {
            var matchedType = topology.AllTypes.FirstOrDefault(t =>
                id.Equals(t.Name, StringComparison.Ordinal) ||
                id.StartsWith(t.Name + "_", StringComparison.Ordinal));
            if (matchedType is not null)
            {
                agentIdToType[id] = matchedType;
            }
        }

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

            TimeSpan nodeDuration;
            DateTimeOffset nodeStartedAt;
            if (agentCompletions.Count > 0)
            {
                nodeStartedAt = agentCompletions[0].StartedAt;
                nodeDuration = agentCompletions[^1].CompletedAt - nodeStartedAt;
            }
            else
            {
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

            // Resolve Type-based edges to FullName strings for the public interface.
            var resolvedType = agentIdToType.GetValueOrDefault(agentId);
            var inEdges = resolvedType is not null
                ? topology.InboundEdges.GetValueOrDefault(resolvedType, [])
                    .Select(t => t.FullName ?? t.Name).ToList()
                : (IReadOnlyList<string>)[];
            var outEdges = resolvedType is not null
                ? topology.OutboundEdges.GetValueOrDefault(resolvedType, [])
                    .Select(t => t.FullName ?? t.Name).ToList()
                : (IReadOnlyList<string>)[];

            var nodeResult = new DagNodeResult(
                nodeId: agentId,
                agentName: agentId,
                kind: NodeKind.Agent,
                diagnostics: diag,
                finalResponse: finalResponse,
                inboundEdges: inEdges,
                outboundEdges: outEdges,
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

    private async Task<IDagRunResult> RunWithNeedlrExecutorAsync(
        GraphTopology topology,
        string graphName,
        string input,
        ProgressEvents.IProgressReporter? progress,
        CancellationToken cancellationToken)
    {
        if (topology.EntryType is null)
        {
            throw new InvalidOperationException(
                $"Cannot run graph workflow '{graphName}': no entry point found.");
        }

        var agents = new Dictionary<Type, AIAgent>();
        foreach (var type in topology.AllTypes)
        {
            agents[type] = _agentFactory.CreateAgent(type.Name);
        }

        var completionSources = new Dictionary<Type, TaskCompletionSource<string>>();
        foreach (var type in topology.AllTypes)
        {
            completionSources[type] = new TaskCompletionSource<string>();
        }

        var skippedNodes = new ConcurrentDictionary<Type, bool>();
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

        var routingChatClient = _chatClientAccessor.ChatClient;
        var nodeTasks = new List<Task>();

        // Create a linked CTS for each WaitAny join node so that remaining
        // branches can be cancelled once the first valid result arrives.
        var waitAnyCtsMap = new ConcurrentDictionary<Type, CancellationTokenSource>();
        foreach (var type in topology.AllTypes)
        {
            if (topology.JoinModes.GetValueOrDefault(type, GraphJoinMode.WaitAll) == GraphJoinMode.WaitAny)
            {
                waitAnyCtsMap[type] = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            }
        }

        // Pre-compute the effective cancellation token for each node.
        // Nodes that are dependencies of a WaitAny join use the linked token
        // so they can be cancelled when the winning branch completes.
        var nodeEffectiveTokens = new Dictionary<Type, CancellationToken>();
        foreach (var type in topology.AllTypes)
        {
            CancellationToken effectiveToken = cancellationToken;
            foreach (var (waitAnyType, cts) in waitAnyCtsMap)
            {
                var waitAnyDeps = topology.IncomingTypes.GetValueOrDefault(waitAnyType, []);
                if (waitAnyDeps.Contains(type))
                {
                    effectiveToken = cts.Token;
                    break;
                }
            }

            nodeEffectiveTokens[type] = effectiveToken;
        }

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
                    else
                    {
                        if (joinMode == GraphJoinMode.WaitAny)
                        {
                            var taskToDepType = new Dictionary<Task<string>, Type>();
                            foreach (var dep in deps)
                            {
                                taskToDepType[completionSources[dep].Task] = dep;
                            }

                            var remaining = new HashSet<Task<string>>(taskToDepType.Keys);
                            nodeInput = input;

                            while (remaining.Count > 0)
                            {
                                var first = await Task.WhenAny(remaining).WaitAsync(cancellationToken);
                                remaining.Remove(first);

                                var depType = taskToDepType[first];
                                var result = await first;

                                if (!skippedNodes.ContainsKey(depType) && !string.IsNullOrWhiteSpace(result))
                                {
                                    nodeInput = result;

                                    // Cancel remaining branches for this WaitAny scope.
                                    if (waitAnyCtsMap.TryGetValue(nodeType, out var waitAnyCts))
                                    {
                                        waitAnyCts.Cancel();
                                    }

                                    break;
                                }
                            }
                        }
                        else
                        {
                            var pendingResults = new List<string>();
                            foreach (var dep in deps)
                            {
                                if (skippedNodes.ContainsKey(dep))
                                    continue;

                                try
                                {
                                    var depResult = await completionSources[dep].Task.WaitAsync(cancellationToken);
                                    if (!string.IsNullOrEmpty(depResult))
                                        pendingResults.Add(depResult);
                                }
                                catch when (IsOptionalEdge(dep, nodeType, topology))
                                {
                                    // Optional upstream failed — treat as degraded.
                                }
                            }

                            if (pendingResults.Count >= 2 && topology.ReducerFunc is not null)
                            {
                                var reducerStart = Stopwatch.GetTimestamp();
                                nodeInput = topology.ReducerFunc(pendingResults);
                                var reducerDuration = Stopwatch.GetElapsedTime(reducerStart);

                                progress?.Report(new ProgressEvents.ReducerNodeInvokedEvent(
                                    DateTimeOffset.UtcNow,
                                    progress.WorkflowId,
                                    progress.AgentId,
                                    null,
                                    progress.Depth + 1,
                                    progress.NextSequence(),
                                    NodeId: topology.ReducerType?.FullName ?? topology.ReducerType?.Name ?? "reducer",
                                    GraphName: graphName,
                                    BranchId: null,
                                    InputBranchCount: pendingResults.Count,
                                    Duration: reducerDuration));
                            }
                            else if (pendingResults.Count > 0)
                            {
                                nodeInput = string.Join("\n\n---\n\n", pendingResults);
                            }
                            else
                            {
                                nodeInput = input;
                            }
                        }
                    }

                    if (skippedNodes.ContainsKey(nodeType))
                    {
                        return;
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
                        NodeId: nodeType.FullName ?? nodeType.Name));

                    var nodeStart = Stopwatch.GetTimestamp();
                    using var diagnosticsBuilder = AgentRunDiagnosticsBuilder.StartNew(agentName);
                    var nodeToken = nodeEffectiveTokens[nodeType];
                    var response = await agent.RunAsync(nodeInput, cancellationToken: nodeToken);
                    var nodeElapsed = Stopwatch.GetElapsedTime(nodeStart);
                    var startOffset = Stopwatch.GetElapsedTime(dagStart, nodeStart);

                    var diag = diagnosticsBuilder.Build();
                    nodeDiagnostics[nodeType] = diag;
                    nodeTimings[nodeType] = (startOffset, nodeElapsed);

                    var text = string.Join("\n", response.Messages
                        .Where(m => !string.IsNullOrEmpty(m.Text))
                        .Select(m => m.Text));

                    if (topology.OutgoingEdgesBySource.TryGetValue(nodeType, out var outEdges) && outEdges.Count > 0)
                    {
                        var conditionInput = !string.IsNullOrWhiteSpace(text) ? text : nodeInput;
                        var resolvedEdges = await _edgeRouter.ResolveOutgoingEdgesAsync(
                            nodeType, conditionInput, topology, routingChatClient, nodeToken);
                        var resolvedTargets = resolvedEdges.Select(e => e.Target).ToHashSet();

                        foreach (var edge in outEdges)
                        {
                            if (!resolvedTargets.Contains(edge.Target))
                            {
                                skippedNodes[edge.Target] = true;
                                completionSources[edge.Target].TrySetResult(string.Empty);
                            }
                        }
                    }

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
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Graceful cancellation from a WaitAny scope — treat as skip.
                    skippedNodes[nodeType] = true;
                    completionSources[nodeType].TrySetResult(string.Empty);
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

                    if (IsNodeRequiredByAllIncomingEdges(nodeType, topology))
                    {
                        completionSources[nodeType].TrySetException(ex);
                    }
                    else
                    {
                        completionSources[nodeType].TrySetResult(string.Empty);
                    }
                }
            }, cancellationToken));
        }

        Exception? dagException = null;
        bool succeeded;
        string? errorMessage = null;

        try
        {
            await Task.WhenAll(nodeTasks).WaitAsync(cancellationToken);

            var requiredFailures = nodeExceptions
                .Where(kv => IsNodeRequiredByAllIncomingEdges(kv.Key, topology))
                .ToList();

            succeeded = requiredFailures.Count == 0;
            if (!succeeded)
            {
                var firstError = requiredFailures.First().Value;
                errorMessage = firstError.Message;
                dagException = new AggregateException(requiredFailures.Select(kv => kv.Value));
            }
        }
        catch (Exception ex)
        {
            succeeded = false;
            errorMessage = ex.Message;
            dagException = ex;
        }
        finally
        {
            foreach (var cts in waitAnyCtsMap.Values)
            {
                cts.Dispose();
            }
        }

        var totalDuration = Stopwatch.GetElapsedTime(dagStart);

        var nodeResultsDict = new Dictionary<string, IDagNodeResult>();
        var stagesList = new List<IAgentStageResult>();

        foreach (var type in topology.AllTypes)
        {
            if (skippedNodes.ContainsKey(type))
                continue;

            var agentName = agents.TryGetValue(type, out var ag)
                ? ag.Name ?? type.Name
                : type.Name;
            var (startOffsetVal, duration) = nodeTimings.GetValueOrDefault(type, (TimeSpan.Zero, TimeSpan.Zero));
            var diag = nodeDiagnostics.GetValueOrDefault(type);

            ChatResponse? finalResponse = null;
            if (completionSources[type].Task.IsCompletedSuccessfully)
            {
                var text = completionSources[type].Task.Result;
                if (!string.IsNullOrEmpty(text))
                {
                    finalResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
                }
            }

            var nodeResult = new DagNodeResult(
                nodeId: type.FullName ?? type.Name,
                agentName: agentName,
                kind: NodeKind.Agent,
                diagnostics: diag,
                finalResponse: finalResponse,
                inboundEdges: topology.InboundEdges.GetValueOrDefault(type, [])
                    .Select(t => t.FullName ?? t.Name).ToList(),
                outboundEdges: topology.OutboundEdges.GetValueOrDefault(type, [])
                    .Select(t => t.FullName ?? t.Name).ToList(),
                startOffset: startOffsetVal,
                duration: duration);

            nodeResultsDict[type.FullName ?? type.Name] = nodeResult;
            stagesList.Add(new AgentStageResult(agentName, finalResponse, diag));
        }

        var branchResults = new Dictionary<string, IReadOnlyList<IAgentStageResult>>();
        var branchIndex = 0;
        var nodesByInbound = topology.AllTypes
            .Where(t => !skippedNodes.ContainsKey(t) &&
                        topology.InboundEdges.ContainsKey(t) &&
                        topology.InboundEdges[t].Count > 0)
            .GroupBy(t => string.Join(",",
                topology.InboundEdges[t]
                    .Select(dep => dep.FullName ?? dep.Name)
                    .OrderBy(n => n)))
            .Where(g => g.Count() > 1);
        foreach (var group in nodesByInbound)
        {
            var groupStages = group
                .Select(t => stagesList.FirstOrDefault(s =>
                    s.AgentName == (agents.TryGetValue(t, out var a)
                        ? a.Name ?? t.Name
                        : t.Name)))
                .Where(s => s is not null)
                .Cast<IAgentStageResult>()
                .ToList();
            if (groupStages.Count > 1)
                branchResults[$"branch-{branchIndex++}"] = groupStages;
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

    private static bool IsNodeRequiredByAllIncomingEdges(Type nodeType, GraphTopology topology)
    {
        var incomingDeps = topology.IncomingTypes.GetValueOrDefault(nodeType, []);
        if (incomingDeps.Count == 0)
            return true;

        foreach (var dep in incomingDeps)
        {
            if (topology.EdgeIsRequired.TryGetValue((dep, nodeType), out var isReq) && !isReq)
                return false;
        }

        return true;
    }

    private static bool IsOptionalEdge(Type sourceType, Type targetType, GraphTopology topology)
    {
        if (topology.EdgeIsRequired.TryGetValue((sourceType, targetType), out var isReq))
            return !isReq;
        return false;
    }
}
