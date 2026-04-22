using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
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
        var requiresNeedlrExecutor = hasWaitAny || RequiresNeedlrExecutor(graphName);

        if (!requiresNeedlrExecutor)
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

    /// <summary>
    /// Returns true when the graph uses routing modes that require the Needlr-native
    /// executor because MAF's BSP engine cannot handle them (e.g. LlmChoice).
    /// </summary>
    private static bool RequiresNeedlrExecutor(string graphName)
    {
        var graphMode = GetGraphRoutingMode(graphName);
        if (graphMode == GraphRoutingMode.LlmChoice)
        {
            return true;
        }

        // Check for per-node LlmChoice overrides
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch { continue; }

            foreach (var type in types)
            {
                foreach (var attr in type.GetCustomAttributes<AgentGraphEdgeAttribute>())
                {
                    if (string.Equals(attr.GraphName, graphName, StringComparison.Ordinal) &&
                        attr.HasNodeRoutingMode &&
                        attr.NodeRoutingMode == GraphRoutingMode.LlmChoice)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static GraphRoutingMode GetGraphRoutingMode(string graphName)
    {
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
                        return attr.RoutingMode;
                    }
                }
            }
        }

        return GraphRoutingMode.Deterministic;
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

        // Track which nodes were skipped by condition routing so they don't
        // block downstream WaitAll joins.
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
                    // Wait for this node to become "ready" — either because it's the entry,
                    // or because its upstream deps complete. During this wait, another task
                    // may mark this node as skipped (condition routing).
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
                        // Wait for upstream dependencies based on join mode.

                        if (joinMode == GraphJoinMode.WaitAny)
                        {
                            // WaitAny: proceed when the first non-skipped dependency completes
                            // with a non-empty result.
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
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // WaitAll: wait for all non-skipped dependencies.
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
                                    // Optional upstream failed — treat as degraded, continue.
                                }
                            }

                            // If a reducer is registered and this is a fan-in point,
                            // invoke it instead of concatenating.
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
                                    NodeId: topology.ReducerType?.Name ?? "reducer",
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

                    // After dependencies resolve, re-check if this node was skipped
                    // by upstream condition routing.
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

                    // Evaluate conditions on outgoing edges. Use the agent's output
                    // text when available; fall back to the node's input if the agent
                    // produced no text output (common with mocked chat clients).
                    if (topology.OutgoingEdgesBySource.TryGetValue(nodeType, out var outEdges) && outEdges.Count > 0)
                    {
                        var conditionInput = !string.IsNullOrWhiteSpace(text) ? text : nodeInput;
                        var chatClient = GetChatClient(agentFactory);
                        var resolvedEdges = await ResolveOutgoingEdgesAsync(
                            nodeType, conditionInput, topology, chatClient, cancellationToken);
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

                    // IsRequired check: if all edges leading to this node are optional,
                    // mark as degraded but don't propagate the exception to downstream nodes.
                    if (IsNodeRequiredByAllIncomingEdges(nodeType, topology))
                    {
                        completionSources[nodeType].TrySetException(ex);
                    }
                    else
                    {
                        // Optional node failed — set empty result so downstream can continue.
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

            // Filter out exceptions from optional (non-required) nodes.
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

        var totalDuration = Stopwatch.GetElapsedTime(dagStart);

        // Build per-node results.
        var nodeResultsDict = new Dictionary<string, IDagNodeResult>();
        var stagesList = new List<IAgentStageResult>();

        foreach (var type in topology.AllTypes)
        {
            if (skippedNodes.ContainsKey(type))
                continue;

            var agentName = agents.TryGetValue(type, out var ag)
                ? ag.Name ?? type.Name
                : type.Name;
            var (startOffset, duration) = nodeTimings.GetValueOrDefault(type, (TimeSpan.Zero, TimeSpan.Zero));
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

    /// <summary>
    /// Determines if a node is required by checking all incoming edges. If ALL
    /// edges leading to this node are <c>IsRequired = true</c>, the node is required.
    /// If any edge is optional, the node is considered optional.
    /// </summary>
    private static bool IsNodeRequiredByAllIncomingEdges(Type nodeType, GraphTopology topology)
    {
        var incomingDeps = topology.IncomingTypes.GetValueOrDefault(nodeType, []);
        if (incomingDeps.Count == 0)
            return true; // Entry node or root — always required.

        foreach (var dep in incomingDeps)
        {
            if (topology.EdgeIsRequired.TryGetValue((dep, nodeType), out var isReq) && !isReq)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if the edge from <paramref name="sourceType"/> to <paramref name="targetType"/>
    /// is optional (IsRequired = false).
    /// </summary>
    private static bool IsOptionalEdge(Type sourceType, Type targetType, GraphTopology topology)
    {
        if (topology.EdgeIsRequired.TryGetValue((sourceType, targetType), out var isReq))
            return !isReq;
        return false;
    }

    private static GraphTopology DiscoverTopology(string graphName)
    {
        Type? entryType = null;
        GraphRoutingMode graphRoutingMode = GraphRoutingMode.Deterministic;
        var edgeDetails = new List<EdgeDetail>();
        var joinModes = new Dictionary<Type, GraphJoinMode>();
        var allTypes = new HashSet<Type>();
        Func<IReadOnlyList<string>, string>? reducerFunc = null;
        Type? reducerType = null;

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
                        graphRoutingMode = attr.RoutingMode;
                        allTypes.Add(type);
                    }
                }

                foreach (var attr in type.GetCustomAttributes<AgentGraphEdgeAttribute>())
                {
                    if (string.Equals(attr.GraphName, graphName, StringComparison.Ordinal))
                    {
                        edgeDetails.Add(new EdgeDetail(
                            type,
                            attr.TargetAgentType,
                            attr.Condition,
                            attr.IsRequired,
                            attr.HasNodeRoutingMode ? attr.NodeRoutingMode : null));
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

                    if (method is not null && method.ReturnType == typeof(string))
                    {
                        reducerType = type;
                        var captured = method;
                        reducerFunc = inputs => (string)captured.Invoke(null, [inputs])!;
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

        foreach (var edge in edgeDetails)
        {
            incomingTypes[edge.Target].Add(edge.Source);
            inboundEdges[edge.Target.Name].Add(edge.Source.Name);
            outboundEdges[edge.Source.Name].Add(edge.Target.Name);
        }

        // Build per-source outgoing edge details for routing decisions.
        var outgoingEdgesBySource = new Dictionary<Type, List<EdgeDetail>>();
        foreach (var edge in edgeDetails)
        {
            if (!outgoingEdgesBySource.TryGetValue(edge.Source, out var list))
            {
                list = [];
                outgoingEdgesBySource[edge.Source] = list;
            }

            list.Add(edge);
        }

        // Determine per-node effective routing mode (node override > graph-wide).
        var effectiveRoutingModes = new Dictionary<Type, GraphRoutingMode>();
        foreach (var (sourceType, sourceEdges) in outgoingEdgesBySource)
        {
            var nodeOverride = sourceEdges
                .Select(e => e.NodeRoutingModeOverride)
                .FirstOrDefault(m => m is not null);
            effectiveRoutingModes[sourceType] = nodeOverride ?? graphRoutingMode;
        }

        // Build IsRequired lookup per edge (source → target).
        var edgeIsRequired = new Dictionary<(Type Source, Type Target), bool>();
        foreach (var edge in edgeDetails)
        {
            edgeIsRequired[(edge.Source, edge.Target)] = edge.IsRequired;
        }

        return new GraphTopology(
            entryType,
            allTypes,
            joinModes,
            incomingTypes,
            inboundEdges.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value),
            outboundEdges.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value),
            graphRoutingMode,
            outgoingEdgesBySource,
            effectiveRoutingModes,
            edgeIsRequired,
            reducerFunc,
            reducerType);
    }

    /// <summary>
    /// Evaluates which outgoing edges from a source node should be followed,
    /// based on the effective routing mode and edge conditions.
    /// </summary>
    private static async Task<List<EdgeDetail>> ResolveOutgoingEdgesAsync(
        Type sourceType,
        object? upstreamOutput,
        GraphTopology topology,
        IChatClient? chatClient,
        CancellationToken cancellationToken)
    {
        if (!topology.OutgoingEdgesBySource.TryGetValue(sourceType, out var edges) || edges.Count == 0)
            return [];

        var routingMode = topology.EffectiveRoutingModes.GetValueOrDefault(sourceType, topology.GraphRoutingMode);

        if (routingMode == GraphRoutingMode.LlmChoice)
        {
            return await ResolveLlmChoiceAsync(sourceType, edges, upstreamOutput, chatClient, cancellationToken);
        }

        var matchingEdges = new List<EdgeDetail>();
        foreach (var edge in edges)
        {
            if (edge.Condition is null)
            {
                matchingEdges.Add(edge);
                continue;
            }

            if (EvaluateCondition(sourceType, edge.Condition, upstreamOutput))
            {
                matchingEdges.Add(edge);
            }
        }

        switch (routingMode)
        {
            case GraphRoutingMode.Deterministic:
            case GraphRoutingMode.AllMatching:
                return matchingEdges;

            case GraphRoutingMode.FirstMatching:
                return matchingEdges.Count > 0 ? [matchingEdges[0]] : [];

            case GraphRoutingMode.ExclusiveChoice:
                if (matchingEdges.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"ExclusiveChoice routing on '{sourceType.Name}': no edge condition matched. " +
                        $"Exactly one must match.");
                }

                if (matchingEdges.Count > 1)
                {
                    var names = string.Join(", ", matchingEdges.Select(e => e.Target.Name));
                    throw new InvalidOperationException(
                        $"ExclusiveChoice routing on '{sourceType.Name}': {matchingEdges.Count} edges matched " +
                        $"({names}). Exactly one must match.");
                }

                return matchingEdges;

            default:
                return matchingEdges;
        }
    }

    /// <summary>
    /// LLM-driven routing: sends the upstream output and edge condition strings
    /// to the LLM as a routing prompt. The LLM picks which edge(s) to follow
    /// by naming the condition string in its response.
    /// </summary>
    private static async Task<List<EdgeDetail>> ResolveLlmChoiceAsync(
        Type sourceType,
        List<EdgeDetail> edges,
        object? upstreamOutput,
        IChatClient? chatClient,
        CancellationToken cancellationToken)
    {
        if (chatClient is null)
        {
            throw new InvalidOperationException(
                $"LlmChoice routing on '{sourceType.Name}' requires an IChatClient, " +
                $"but none is available. Ensure the agent framework is configured with a chat client.");
        }

        var conditionalEdges = edges.Where(e => e.Condition is not null).ToList();
        if (conditionalEdges.Count == 0)
        {
            return edges.ToList();
        }

        var options = string.Join("\n", conditionalEdges.Select(
            (e, i) => $"  {i + 1}. {e.Condition} → {e.Target.Name}"));

        var routingPrompt = $"""
            You are a routing agent. Based on the input below, choose which route to take.

            Input:
            {upstreamOutput}

            Available routes:
            {options}

            Respond with ONLY the exact condition text of the route you choose. Nothing else.
            """;

        var response = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.User, routingPrompt)],
            cancellationToken: cancellationToken);

        var chosenText = response.Text?.Trim() ?? string.Empty;

        var chosen = conditionalEdges
            .Where(e => chosenText.Contains(e.Condition!, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (chosen.Count == 0)
        {
            var unconditional = edges.Where(e => e.Condition is null).ToList();
            return unconditional.Count > 0 ? unconditional : [conditionalEdges[0]];
        }

        var result = new List<EdgeDetail>(chosen);
        result.AddRange(edges.Where(e => e.Condition is null));
        return result;
    }

    /// <summary>
    /// Evaluates a condition string by looking up a static method on the source
    /// agent type that accepts <c>object?</c> and returns <c>bool</c>.
    /// </summary>
    private static bool EvaluateCondition(Type sourceType, string conditionMethodName, object? upstreamOutput)
    {
        // Try to find the method with object parameter first, then try with no specific type.
        var method = sourceType.GetMethod(
            conditionMethodName,
            BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic,
            null,
            [typeof(object)],
            null);

        if (method is null)
        {
            // Try finding any static method with the given name that takes one parameter.
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

    private static IChatClient? GetChatClient(IAgentFactory agentFactory)
    {
        // AgentFactory stores its configured options in _lazyConfiguredOptions.
        // We access it via reflection to extract the ChatClientFactory.
        var lazyField = agentFactory.GetType().GetField(
            "_lazyConfiguredOptions",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (lazyField?.GetValue(agentFactory) is Lazy<AgentFrameworkConfigureOptions> lazy)
        {
            var opts = lazy.Value;
            if (opts.ChatClientFactory is { } factory)
            {
                var spField = agentFactory.GetType().GetField(
                    "_serviceProvider",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var sp = spField?.GetValue(agentFactory) as IServiceProvider;
                return sp is not null ? factory(sp) : null;
            }
        }

        return null;
    }

    private sealed record EdgeDetail(
        Type Source,
        Type Target,
        string? Condition,
        bool IsRequired,
        GraphRoutingMode? NodeRoutingModeOverride);

    private sealed record GraphTopology(
        Type? EntryType,
        HashSet<Type> AllTypes,
        Dictionary<Type, GraphJoinMode> JoinModes,
        Dictionary<Type, List<Type>> IncomingTypes,
        Dictionary<string, IReadOnlyList<string>> InboundEdges,
        Dictionary<string, IReadOnlyList<string>> OutboundEdges,
        GraphRoutingMode GraphRoutingMode,
        Dictionary<Type, List<EdgeDetail>> OutgoingEdgesBySource,
        Dictionary<Type, GraphRoutingMode> EffectiveRoutingModes,
        Dictionary<(Type Source, Type Target), bool> EdgeIsRequired,
        Func<IReadOnlyList<string>, string>? ReducerFunc,
        Type? ReducerType);
}
