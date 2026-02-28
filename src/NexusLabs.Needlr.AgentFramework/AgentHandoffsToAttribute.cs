namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Declares a handoff target for a <see cref="NeedlrAiAgentAttribute"/>-annotated agent.
/// Apply this attribute one or more times to specify which agents the decorated agent can hand off
/// to when used as the initial agent in a handoff workflow via
/// <see cref="IWorkflowFactory.CreateHandoffWorkflow{TInitialAgent}"/>.
/// </summary>
/// <remarks>
/// When the <c>NexusLabs.Needlr.AgentFramework.Generators</c> package is referenced, the source
/// generator emits a strongly-typed extension method (e.g. <c>CreateTriageHandoffWorkflow</c>)
/// on <see cref="IWorkflowFactory"/> for each agent type that carries this attribute. The generated
/// method encapsulates the agent type so the composition root requires no direct type references.
/// </remarks>
/// <example>
/// <code>
/// [NeedlrAiAgent(Instructions = "Triage incoming customer requests.")]
/// [AgentHandoffsTo(typeof(BillingAgent),  "Route billing or payment questions to the billing agent")]
/// [AgentHandoffsTo(typeof(SupportAgent),  "Route general support requests to the support agent")]
/// public class TriageAgent { }
///
/// // The source generator emits:
/// //   workflowFactory.CreateTriageAgentHandoffWorkflow()
/// // which wires all handoffs without requiring direct type references at the call site.
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class AgentHandoffsToAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="AgentHandoffsToAttribute"/>.
    /// </summary>
    /// <param name="targetAgentType">
    /// The type of the target agent. Must be annotated with <see cref="NeedlrAiAgentAttribute"/>.
    /// </param>
    /// <param name="handoffReason">
    /// A description of when the decorated agent should hand off to <paramref name="targetAgentType"/>.
    /// Passed to the underlying MAF handoff builder as the tool description. When <see langword="null"/>,
    /// MAF derives the reason from the target agent's description or name.
    /// </param>
    public AgentHandoffsToAttribute(Type targetAgentType, string? handoffReason = null)
    {
        ArgumentNullException.ThrowIfNull(targetAgentType);
        TargetAgentType = targetAgentType;
        HandoffReason = handoffReason;
    }

    /// <summary>Gets the type of the target agent to hand off to.</summary>
    public Type TargetAgentType { get; }

    /// <summary>Gets the optional reason describing when to hand off to <see cref="TargetAgentType"/>.</summary>
    public string? HandoffReason { get; }
}
