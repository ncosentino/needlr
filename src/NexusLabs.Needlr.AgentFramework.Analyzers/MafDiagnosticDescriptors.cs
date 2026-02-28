using Microsoft.CodeAnalysis;

namespace NexusLabs.Needlr.AgentFramework.Analyzers;

/// <summary>
/// Contains diagnostic descriptors for all Needlr Agent Framework analyzers.
/// </summary>
public static class MafDiagnosticDescriptors
{
    private const string Category = "NexusLabs.Needlr.AgentFramework";
    private const string HelpLinkBase = "https://github.com/nexus-labs/needlr/blob/main/docs/analyzers/";

    /// <summary>
    /// NDLRMAF001: <c>[AgentHandoffsTo(typeof(X))]</c> target type X is not decorated with <c>[NeedlrAiAgent]</c>.
    /// </summary>
    public static readonly DiagnosticDescriptor HandoffsToTargetNotNeedlrAgent = new(
        id: MafDiagnosticIds.HandoffsToTargetNotNeedlrAgent,
        title: "[AgentHandoffsTo] target type is not a declared agent",
        messageFormat: "'{0}' is referenced as a handoff target but is not decorated with [NeedlrAiAgent]. Handoff targets must be registered agent types.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Types used as handoff targets in [AgentHandoffsTo] must also be decorated with [NeedlrAiAgent] so Needlr can register and resolve them. Add [NeedlrAiAgent] to the target type, or remove it from [AgentHandoffsTo].",
        helpLinkUri: HelpLinkBase + "NDLRMAF001.md");

    /// <summary>
    /// NDLRMAF002: <c>[AgentGroupChatMember("g")]</c> group "g" has fewer than two members.
    /// </summary>
    public static readonly DiagnosticDescriptor GroupChatTooFewMembers = new(
        id: MafDiagnosticIds.GroupChatTooFewMembers,
        title: "Group chat has fewer than two members",
        messageFormat: "Group chat '{0}' has only {1} member(s) in this compilation. A group chat requires at least two participants. Add [AgentGroupChatMember(\"{0}\")] to another agent class.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "IWorkflowFactory.CreateGroupChatWorkflow() throws at runtime when fewer than two agents are registered for the group. Declare at least two agent classes with the same [AgentGroupChatMember] group name.",
        helpLinkUri: HelpLinkBase + "NDLRMAF002.md",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// NDLRMAF003: A class has <c>[AgentHandoffsTo]</c> but is not decorated with <c>[NeedlrAiAgent]</c>.
    /// </summary>
    public static readonly DiagnosticDescriptor HandoffsToSourceNotNeedlrAgent = new(
        id: MafDiagnosticIds.HandoffsToSourceNotNeedlrAgent,
        title: "[AgentHandoffsTo] source class is not a declared agent",
        messageFormat: "'{0}' has [AgentHandoffsTo] but is not decorated with [NeedlrAiAgent]. The source agent of a handoff workflow must itself be a declared agent.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The class that carries [AgentHandoffsTo] is the initial agent of a handoff workflow and must also be registered with Needlr via [NeedlrAiAgent]. Add [NeedlrAiAgent] to the class, or remove [AgentHandoffsTo] if it was added by mistake.",
        helpLinkUri: HelpLinkBase + "NDLRMAF003.md");

    /// <summary>
    /// NDLRMAF004: A cyclic handoff chain was detected.
    /// </summary>
    public static readonly DiagnosticDescriptor CyclicHandoffChain = new(
        id: MafDiagnosticIds.CyclicHandoffChain,
        title: "Cyclic handoff chain detected",
        messageFormat: "'{0}' participates in a cyclic handoff chain: {1}. This may cause infinite routing loops at runtime.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A handoff cycle exists where an agent can eventually hand off back to itself. While MAF may handle termination conditions in practice, this is usually a topology design error. Review the [AgentHandoffsTo] declarations and break the cycle.",
        helpLinkUri: HelpLinkBase + "NDLRMAF004.md",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// NDLRMAF005: An agent declares a FunctionGroups entry with no matching [AgentFunctionGroup] class.
    /// </summary>
    public static readonly DiagnosticDescriptor UnresolvedFunctionGroupReference = new(
        id: MafDiagnosticIds.UnresolvedFunctionGroupReference,
        title: "FunctionGroups references an unregistered group name",
        messageFormat: "'{0}' declares FunctionGroups entry '{1}' but no class decorated with [AgentFunctionGroup(\"{1}\")] exists in this compilation. The agent will silently receive zero tools from this group at runtime.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When an agent declares FunctionGroups = new[] { \"name\" }, a class with [AgentFunctionGroup(\"name\")] must exist in the same compilation. If no such class is registered, the group resolves to zero tools and the agent silently loses access to those functions. Check for typos or register the missing function group class.",
        helpLinkUri: HelpLinkBase + "NDLRMAF005.md",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// NDLRMAF006: Duplicate Order value within the same [AgentSequenceMember] pipeline.
    /// </summary>
    public static readonly DiagnosticDescriptor DuplicateSequenceOrder = new(
        id: MafDiagnosticIds.DuplicateSequenceOrder,
        title: "Duplicate Order value in sequential pipeline",
        messageFormat: "Pipeline '{0}' has a duplicate Order value ({1}) on '{2}'. Each agent in a sequential pipeline must have a unique Order.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Two or more agents in the same [AgentSequenceMember] pipeline declare the same Order value. This is ambiguous and will cause incorrect pipeline ordering or runtime errors. Assign a unique Order to each agent in the pipeline.",
        helpLinkUri: HelpLinkBase + "NDLRMAF006.md",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// NDLRMAF007: Gap in Order sequence within the same [AgentSequenceMember] pipeline.
    /// </summary>
    public static readonly DiagnosticDescriptor GapInSequenceOrder = new(
        id: MafDiagnosticIds.GapInSequenceOrder,
        title: "Gap in sequential pipeline Order values",
        messageFormat: "Pipeline '{0}' has a gap in its Order sequence — Order {1} is missing. Use contiguous Order values to avoid potential agent omission.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The Order values declared via [AgentSequenceMember] for this pipeline are not contiguous. This is not necessarily an error but may indicate an unregistered agent has been accidentally omitted from the pipeline. Review the pipeline members and ensure no agent is missing.",
        helpLinkUri: HelpLinkBase + "NDLRMAF007.md",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// NDLRMAF008: Agent participates in no topology declaration.
    /// </summary>
    public static readonly DiagnosticDescriptor OrphanAgent = new(
        id: MafDiagnosticIds.OrphanAgent,
        title: "Agent participates in no topology declaration",
        messageFormat: "'{0}' is decorated with [NeedlrAiAgent] but does not appear in any topology declaration ([AgentHandoffsTo], [AgentGroupChatMember], or [AgentSequenceMember]). It will not be wired into any generated workflow.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "An agent registered with [NeedlrAiAgent] is not referenced in any topology. It will not appear in any source-generated workflow method. This may be intentional (e.g. the agent is used programmatically), but is often an oversight. Add a topology attribute or remove [NeedlrAiAgent] if the class is not an agent.",
        helpLinkUri: HelpLinkBase + "NDLRMAF008.md",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// NDLRMAF009: <c>[WorkflowRunTerminationCondition]</c> declared on a non-agent class.
    /// </summary>
    public static readonly DiagnosticDescriptor WorkflowRunTerminationConditionOnNonAgent = new(
        id: MafDiagnosticIds.WorkflowRunTerminationConditionOnNonAgent,
        title: "[WorkflowRunTerminationCondition] declared on a non-agent class",
        messageFormat: "'{0}' has [WorkflowRunTerminationCondition] but is not decorated with [NeedlrAiAgent]. Termination conditions are only evaluated for registered agents.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A class carries [WorkflowRunTerminationCondition] but is not decorated with [NeedlrAiAgent], so it will never be part of a generated workflow and the condition will never be evaluated. Either add [NeedlrAiAgent] to make it a registered agent, or remove [WorkflowRunTerminationCondition].",
        helpLinkUri: HelpLinkBase + "NDLRMAF009.md");

    /// <summary>
    /// NDLRMAF010: Condition type does not implement <c>IWorkflowTerminationCondition</c>.
    /// </summary>
    public static readonly DiagnosticDescriptor TerminationConditionTypeInvalid = new(
        id: MafDiagnosticIds.TerminationConditionTypeInvalid,
        title: "Termination condition type does not implement IWorkflowTerminationCondition",
        messageFormat: "'{0}' does not implement IWorkflowTerminationCondition and cannot be used as a termination condition on '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The conditionType argument passed to [WorkflowRunTerminationCondition] or [AgentTerminationCondition] must be a class that implements IWorkflowTerminationCondition. The condition will be instantiated at runtime via Activator.CreateInstance and cast to IWorkflowTerminationCondition; using an incompatible type will throw at runtime.",
        helpLinkUri: HelpLinkBase + "NDLRMAF010.md");

    /// <summary>
    /// NDLRMAF011: Prefer <c>[AgentTerminationCondition]</c> over
    /// <c>[WorkflowRunTerminationCondition]</c> for group chat members.
    /// </summary>
    public static readonly DiagnosticDescriptor PreferAgentTerminationConditionForGroupChat = new(
        id: MafDiagnosticIds.PreferAgentTerminationConditionForGroupChat,
        title: "Prefer [AgentTerminationCondition] over [WorkflowRunTerminationCondition] for group chat members",
        messageFormat: "'{0}' is an [AgentGroupChatMember] with [WorkflowRunTerminationCondition]. Consider using [AgentTerminationCondition] instead, which fires before the next agent turn (Layer 1) rather than after the response is emitted (Layer 2).",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "For group chat agents, [AgentTerminationCondition] is evaluated inside MAF's group chat loop before the next turn begins, allowing a clean early exit. [WorkflowRunTerminationCondition] fires after the full response is emitted (Layer 2). Both work, but [AgentTerminationCondition] provides a more immediate stop for group chat workflows.",
        helpLinkUri: HelpLinkBase + "NDLRMAF011.md");

    /// <summary>
    /// NDLRMAF012: <c>[AgentFunction]</c> method has no <c>[Description]</c> attribute.
    /// </summary>
    public static readonly DiagnosticDescriptor AgentFunctionMissingDescription = new(
        id: MafDiagnosticIds.AgentFunctionMissingDescription,
        title: "[AgentFunction] method is missing a [Description] attribute",
        messageFormat: "'{0}' is decorated with [AgentFunction] but has no [Description] attribute. Without a description, the LLM cannot determine when to use this tool.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The LLM uses the [Description] text to decide when to invoke an agent function tool. A method without [Description] will appear as an unlabelled tool and the LLM may ignore it or call it incorrectly. Add [Description(\"...\")].",
        helpLinkUri: HelpLinkBase + "NDLRMAF012.md");

    /// <summary>
    /// NDLRMAF013: Parameter of an <c>[AgentFunction]</c> method is missing a <c>[Description]</c> attribute.
    /// </summary>
    public static readonly DiagnosticDescriptor AgentFunctionParameterMissingDescription = new(
        id: MafDiagnosticIds.AgentFunctionParameterMissingDescription,
        title: "[AgentFunction] method parameter is missing a [Description] attribute",
        messageFormat: "Parameter '{0}' on [AgentFunction] method '{1}' has no [Description] attribute. Without a description, the LLM may misuse this parameter.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Each parameter of an [AgentFunction] method should carry a [Description] attribute so the LLM knows what value to supply. CancellationToken parameters are exempt. Add [Description(\"...\")] to the parameter.",
        helpLinkUri: HelpLinkBase + "NDLRMAF013.md");

    /// <summary>
    /// NDLRMAF014: A type in <c>FunctionTypes</c> on <c>[NeedlrAiAgent]</c> has no
    /// <c>[AgentFunction]</c> methods.
    /// </summary>
    public static readonly DiagnosticDescriptor AgentFunctionTypesMiswired = new(
        id: MafDiagnosticIds.AgentFunctionTypesMiswired,
        title: "FunctionTypes entry has no [AgentFunction] methods",
        messageFormat: "'{0}' is listed in FunctionTypes on '{1}' but has no methods decorated with [AgentFunction]. The agent will receive zero tools from this type at runtime.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A type referenced in FunctionTypes must contain at least one method decorated with [AgentFunction]; otherwise the agent silently receives no tools from it. Either add [AgentFunction] to a method on the type, or remove it from FunctionTypes.",
        helpLinkUri: HelpLinkBase + "NDLRMAF014.md");
}
