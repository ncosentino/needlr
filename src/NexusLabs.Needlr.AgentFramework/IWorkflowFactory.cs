using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Creates MAF <see cref="Workflow"/> instances from topology declared on agent classes via
/// <see cref="AgentHandoffsToAttribute"/> and <see cref="AgentGroupChatMemberAttribute"/>.
/// Registered in DI by <c>UsingAgentFramework()</c> — inject via constructor or resolve from
/// <c>IServiceProvider</c>.
/// </summary>
/// <remarks>
/// <para>
/// When the <c>NexusLabs.Needlr.AgentFramework.Generators</c> source generator is used, strongly-typed
/// extension methods are emitted directly into the agents assembly — for example,
/// <c>CreateTriageHandoffWorkflow()</c> and <c>CreateCodeReviewGroupChatWorkflow()</c>. These generated
/// methods call the core <see cref="CreateHandoffWorkflow{TInitialAgent}"/> and
/// <see cref="CreateGroupChatWorkflow"/> methods internally, encapsulating type references and group
/// name strings so the composition root requires neither.
/// </para>
/// <para>
/// When the generator is not used, <see cref="CreateHandoffWorkflow{TInitialAgent}"/> and
/// <see cref="CreateGroupChatWorkflow"/> fall back to reading attributes via reflection.
/// </para>
/// </remarks>
public interface IWorkflowFactory
{
    /// <summary>
    /// Creates a handoff <see cref="Workflow"/> where <typeparamref name="TInitialAgent"/> is the
    /// starting agent and its <see cref="AgentHandoffsToAttribute"/> declarations determine the
    /// available handoff targets.
    /// </summary>
    /// <typeparam name="TInitialAgent">
    /// The type of the initial agent. Must be annotated with both <see cref="NeedlrAiAgentAttribute"/>
    /// and at least one <see cref="AgentHandoffsToAttribute"/>.
    /// </typeparam>
    /// <returns>A built <see cref="Workflow"/> ready to run via MAF's execution environment.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <typeparamref name="TInitialAgent"/> has no <see cref="AgentHandoffsToAttribute"/>
    /// attributes declared.
    /// </exception>
    Workflow CreateHandoffWorkflow<TInitialAgent>() where TInitialAgent : class;

    /// <summary>
    /// Creates a round-robin group chat <see cref="Workflow"/> for the named group. All agent types
    /// decorated with <c>[AgentGroupChatMember(<paramref name="groupName"/>)]</c> are included as
    /// participants.
    /// </summary>
    /// <param name="groupName">
    /// The group name. Must match the <see cref="AgentGroupChatMemberAttribute.GroupName"/> value
    /// (case-sensitive) on at least two agent types. Prefer using a generated extension method
    /// (e.g. <c>CreateCodeReviewGroupChatWorkflow()</c>) to avoid referencing this string directly.
    /// </param>
    /// <param name="maxIterations">
    /// Maximum number of round-robin turns before the workflow terminates. Defaults to 10.
    /// </param>
    /// <returns>A built <see cref="Workflow"/> ready to run via MAF's execution environment.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="groupName"/> has fewer than two registered members.
    /// </exception>
    Workflow CreateGroupChatWorkflow(string groupName, int maxIterations = 10);

    /// <summary>
    /// Creates a sequential pipeline <see cref="Workflow"/> where the output of each agent flows
    /// as input to the next agent in the sequence.
    /// </summary>
    /// <param name="agents">
    /// The agents to chain in order. Must contain at least one agent.
    /// </param>
    /// <returns>A built <see cref="Workflow"/> ready to run via MAF's execution environment.</returns>
    Workflow CreateSequentialWorkflow(params AIAgent[] agents);
}
