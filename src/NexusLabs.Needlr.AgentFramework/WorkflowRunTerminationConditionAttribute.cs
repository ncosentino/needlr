namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Declares a termination condition that is evaluated by Needlr's <c>RunAsync</c> execution
/// helpers after each completed agent turn (Layer 2). Unlike
/// <see cref="AgentTerminationConditionAttribute"/>, this works with all workflow types
/// (group chat, handoff, sequential).
/// </summary>
/// <remarks>
/// <para>
/// Apply to any agent class. Multiple conditions may be stacked; OR semantics apply (first
/// match stops the workflow). Conditions from all agents in the workflow are merged; a condition
/// fires only when the agent it is declared on produces the matching response.
/// </para>
/// <para>
/// The <c>conditionType</c> must implement <see cref="IWorkflowTerminationCondition"/>.
/// Constructor arguments are passed via <c>ctorArgs</c>.
/// </para>
/// <para>
/// For group chat workflows, prefer <see cref="AgentTerminationConditionAttribute"/> — it fires
/// before the next turn starts, which is a cleaner stop than Layer 2 (which fires after the
/// response is fully emitted).
/// </para>
/// <example>
/// <code>
/// // Stop a sequential pipeline early on extraction failure
/// [AgentSequenceMember("content-pipeline", Order = 1)]
/// [WorkflowRunTerminationCondition(typeof(KeywordTerminationCondition), "EXTRACTION_FAILED")]
/// public class ContentExtractorAgent { }
/// </code>
/// </example>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class WorkflowRunTerminationConditionAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="WorkflowRunTerminationConditionAttribute"/>.
    /// </summary>
    /// <param name="conditionType">
    /// The type that implements <see cref="IWorkflowTerminationCondition"/>. An instance is
    /// created at workflow construction time via
    /// <see cref="Activator.CreateInstance(Type, object[])"/> with <paramref name="ctorArgs"/>.
    /// </param>
    /// <param name="ctorArgs">
    /// Arguments forwarded to the condition's constructor. May be empty if the condition has a
    /// parameterless constructor.
    /// </param>
    public WorkflowRunTerminationConditionAttribute(Type conditionType, params object[] ctorArgs)
    {
        ArgumentNullException.ThrowIfNull(conditionType);
        ConditionType = conditionType;
        CtorArgs = ctorArgs ?? [];
    }

    /// <summary>Gets the type of the termination condition to instantiate.</summary>
    public Type ConditionType { get; }

    /// <summary>Gets the constructor arguments forwarded when instantiating the condition.</summary>
    public object[] CtorArgs { get; }
}
