using System.Reflection;

using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework;

namespace NexusLabs.Needlr.AgentFramework.Workflows;

/// <summary>
/// Evaluates edge conditions and enforces routing mode semantics to determine
/// which outgoing edges a node should follow after execution.
/// </summary>
internal sealed class GraphEdgeRouter
{
    /// <summary>
    /// Resolves which outgoing edges from a source node should be followed,
    /// based on the effective routing mode and edge conditions.
    /// </summary>
    public async Task<List<GraphEdgeDetail>> ResolveOutgoingEdgesAsync(
        Type sourceType,
        object? upstreamOutput,
        GraphTopology topology,
        IChatClient? routingChatClient,
        CancellationToken cancellationToken)
    {
        if (!topology.OutgoingEdgesBySource.TryGetValue(sourceType, out var edges) || edges.Count == 0)
            return [];

        var routingMode = topology.EffectiveRoutingModes.GetValueOrDefault(sourceType, topology.GraphRoutingMode);

        if (routingMode == GraphRoutingMode.LlmChoice)
        {
            return await ResolveLlmChoiceAsync(sourceType, edges, upstreamOutput, routingChatClient, cancellationToken);
        }

        var matchingEdges = new List<GraphEdgeDetail>();
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

    private static async Task<List<GraphEdgeDetail>> ResolveLlmChoiceAsync(
        Type sourceType,
        List<GraphEdgeDetail> edges,
        object? upstreamOutput,
        IChatClient? chatClient,
        CancellationToken cancellationToken)
    {
        if (chatClient is null)
        {
            throw new InvalidOperationException(
                $"LlmChoice routing on '{sourceType.Name}' requires an IChatClient. " +
                $"Ensure the agent framework is configured with a chat client via " +
                $"UsingAgentFramework(af => af.Configure(opts => opts.ChatClientFactory = ...)).");
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

        var result = new List<GraphEdgeDetail>(chosen);
        result.AddRange(edges.Where(e => e.Condition is null));
        return result;
    }

    /// <summary>
    /// Evaluates a condition string by looking up a static method on the source
    /// agent type that accepts <c>object?</c> and returns <c>bool</c>.
    /// </summary>
    internal static bool EvaluateCondition(Type sourceType, string conditionMethodName, object? upstreamOutput)
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
}
