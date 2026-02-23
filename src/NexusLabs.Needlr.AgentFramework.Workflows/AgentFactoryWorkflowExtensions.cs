using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace NexusLabs.Needlr.AgentFramework.Workflows;

/// <summary>
/// Extension methods on <see cref="IAgentFactory"/> for building MAF handoff workflow topologies.
/// </summary>
public static class AgentFactoryWorkflowExtensions
{
    /// <summary>
    /// Builds a handoff workflow where <paramref name="initialAgent"/> routes to one of the
    /// <paramref name="handoffTargets"/> based on LLM-driven tool-call decisions.
    /// </summary>
    /// <remarks>
    /// This is an ergonomic wrapper over the raw MAF handoff builder, which requires passing
    /// <paramref name="initialAgent"/> twice — once to <c>CreateHandoffBuilderWith</c> and again
    /// as the <c>from</c> argument in <c>WithHandoffs</c>.
    /// </remarks>
    public static Workflow BuildHandoffWorkflow(
        this IAgentFactory agentFactory,
        AIAgent initialAgent,
        params AIAgent[] handoffTargets)
    {
        ArgumentNullException.ThrowIfNull(agentFactory);
        ArgumentNullException.ThrowIfNull(initialAgent);
        ArgumentNullException.ThrowIfNull(handoffTargets);

        return agentFactory.BuildHandoffWorkflow(
            initialAgent,
            handoffTargets.Select(t => (t, (string?)null)).ToArray());
    }

    /// <summary>
    /// Builds a handoff workflow where <paramref name="initialAgent"/> routes to one of the
    /// <paramref name="handoffTargets"/> based on LLM-driven tool-call decisions.
    /// Each target is paired with an optional routing reason provided to the LLM when deciding
    /// which agent to hand off to. Targets with a null or empty reason use the no-reason handoff.
    /// </summary>
    public static Workflow BuildHandoffWorkflow(
        this IAgentFactory agentFactory,
        AIAgent initialAgent,
        params (AIAgent Target, string? Reason)[] handoffTargets)
    {
        ArgumentNullException.ThrowIfNull(agentFactory);
        ArgumentNullException.ThrowIfNull(initialAgent);
        ArgumentNullException.ThrowIfNull(handoffTargets);

        var builder = AgentWorkflowBuilder.CreateHandoffBuilderWith(initialAgent);

        var withoutReason = handoffTargets
            .Where(t => string.IsNullOrEmpty(t.Reason))
            .Select(t => t.Target)
            .ToArray();

        if (withoutReason.Length > 0)
            builder.WithHandoffs(initialAgent, withoutReason);

        foreach (var (target, reason) in handoffTargets.Where(t => !string.IsNullOrEmpty(t.Reason)))
            builder.WithHandoff(initialAgent, target, reason!);

        return builder.Build();
    }
}
