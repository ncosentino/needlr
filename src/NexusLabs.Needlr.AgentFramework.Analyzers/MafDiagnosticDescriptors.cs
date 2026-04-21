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

    /// <summary>
    /// NDLRMAF015: <c>.ToString()</c> is called on a tool result property that may
    /// contain a <c>JsonElement</c>. Use <c>ToolResultSerializer.Serialize()</c> instead.
    /// </summary>
    public static readonly DiagnosticDescriptor ToolResultToStringCall = new(
        id: MafDiagnosticIds.ToolResultToStringCall,
        title: "Do not call ToString() on tool result objects",
        messageFormat: "'{0}.Result.ToString()' may produce a C# type name instead of JSON. Use ToolResultSerializer.Serialize() to get the correct string representation.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Tool result objects (ToolCallResult.Result, FunctionResultContent.Result) are typed as object? and may contain a System.Text.Json.JsonElement. Calling ToString() on a JsonElement does not consistently return the JSON text — for arrays and complex objects it produces the C# type name. Use ToolResultSerializer.Serialize() from NexusLabs.Needlr.AgentFramework instead.",
        helpLinkUri: HelpLinkBase + "NDLRMAF015.md");

    /// <summary>
    /// NDLRMAF016: A cycle was detected in an agent graph.
    /// </summary>
    public static readonly DiagnosticDescriptor GraphCycleDetected = new(
        id: MafDiagnosticIds.GraphCycleDetected,
        title: "Cycle detected in agent graph",
        messageFormat: "Graph '{0}' contains a cycle: {1}. Agent graphs must be directed acyclic graphs (DAGs).",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Agent graph workflows use superstep-based BSP execution which requires a directed acyclic graph (DAG). A cycle in the graph will cause infinite execution. Review the [AgentGraphEdge] declarations and break the cycle.",
        helpLinkUri: HelpLinkBase + "NDLRMAF016.md",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// NDLRMAF017: A named agent graph has no entry point.
    /// </summary>
    public static readonly DiagnosticDescriptor GraphNoEntryPoint = new(
        id: MafDiagnosticIds.GraphNoEntryPoint,
        title: "Graph has no entry point",
        messageFormat: "Graph '{0}' has [AgentGraphEdge] declarations but no [AgentGraphEntry]. Every graph must have exactly one entry point.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A named agent graph requires exactly one [AgentGraphEntry] declaration to identify the starting agent. Without an entry point, the source generator cannot emit a factory method. Add [AgentGraphEntry] to the agent that should be the starting point.",
        helpLinkUri: HelpLinkBase + "NDLRMAF017.md",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// NDLRMAF018: A named agent graph has multiple entry points.
    /// </summary>
    public static readonly DiagnosticDescriptor GraphMultipleEntryPoints = new(
        id: MafDiagnosticIds.GraphMultipleEntryPoints,
        title: "Graph has multiple entry points",
        messageFormat: "Graph '{0}' has multiple [AgentGraphEntry] declarations: {1}. A graph must have exactly one entry point.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Each named agent graph must have exactly one [AgentGraphEntry]. Multiple entry points create ambiguity about where execution begins. Remove the extra [AgentGraphEntry] attributes so only one remains for this graph name.",
        helpLinkUri: HelpLinkBase + "NDLRMAF018.md",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// NDLRMAF019: An <c>[AgentGraphEdge]</c> references a target that is not a declared agent.
    /// </summary>
    public static readonly DiagnosticDescriptor GraphEdgeTargetNotAgent = new(
        id: MafDiagnosticIds.GraphEdgeTargetNotAgent,
        title: "Graph edge target is not a declared agent",
        messageFormat: "'{0}' is referenced as a graph edge target but is not decorated with [NeedlrAiAgent]. Graph edge targets must be registered agent types.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Types used as targets in [AgentGraphEdge] must be decorated with [NeedlrAiAgent] so Needlr can register and resolve them. Add [NeedlrAiAgent] to the target type, or remove it from [AgentGraphEdge].",
        helpLinkUri: HelpLinkBase + "NDLRMAF019.md");

    /// <summary>
    /// NDLRMAF020: A class has <c>[AgentGraphEdge]</c> but is not a declared agent.
    /// </summary>
    public static readonly DiagnosticDescriptor GraphEdgeSourceNotAgent = new(
        id: MafDiagnosticIds.GraphEdgeSourceNotAgent,
        title: "Graph edge source is not a declared agent",
        messageFormat: "'{0}' has [AgentGraphEdge] but is not decorated with [NeedlrAiAgent]. The source of a graph edge must be a declared agent.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The class that carries [AgentGraphEdge] is a node in the agent graph and must be registered with Needlr via [NeedlrAiAgent]. Add [NeedlrAiAgent] to the class, or remove [AgentGraphEdge] if it was added by mistake.",
        helpLinkUri: HelpLinkBase + "NDLRMAF020.md");

    /// <summary>
    /// NDLRMAF021: A class has <c>[AgentGraphEntry]</c> but is not a declared agent.
    /// </summary>
    public static readonly DiagnosticDescriptor GraphEntryPointNotAgent = new(
        id: MafDiagnosticIds.GraphEntryPointNotAgent,
        title: "Graph entry point is not a declared agent",
        messageFormat: "'{0}' has [AgentGraphEntry] but is not decorated with [NeedlrAiAgent]. The graph entry point must be a declared agent.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The class that carries [AgentGraphEntry] is the starting node of the graph workflow and must be registered with Needlr via [NeedlrAiAgent]. Add [NeedlrAiAgent] to the class, or remove [AgentGraphEntry] if it was added by mistake.",
        helpLinkUri: HelpLinkBase + "NDLRMAF021.md");

    /// <summary>
    /// NDLRMAF022: An agent graph contains unreachable agents.
    /// </summary>
    public static readonly DiagnosticDescriptor GraphUnreachableAgent = new(
        id: MafDiagnosticIds.GraphUnreachableAgent,
        title: "Graph contains unreachable agents",
        messageFormat: "'{0}' declares edges in graph '{1}' but is not reachable from the entry point. It will never be executed.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "An agent that declares [AgentGraphEdge] for a named graph is not reachable from that graph's [AgentGraphEntry] point via any path. This usually means the agent was added to the wrong graph or a connecting edge is missing.",
        helpLinkUri: HelpLinkBase + "NDLRMAF022.md",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// NDLRMAF023: MaxSupersteps value is invalid (≤ 0).
    /// </summary>
    public static readonly DiagnosticDescriptor GraphInvalidMaxSupersteps = new(
        id: MafDiagnosticIds.GraphInvalidMaxSupersteps,
        title: "MaxSupersteps value is invalid",
        messageFormat: "MaxSupersteps on [AgentGraphEntry] for graph '{0}' is {1}, which is invalid. MaxSupersteps must be greater than zero.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The MaxSupersteps property on [AgentGraphEntry] controls the maximum number of BSP supersteps the graph executor will run. A value of zero or negative means the graph can never make progress. Set MaxSupersteps to a positive integer.",
        helpLinkUri: HelpLinkBase + "NDLRMAF023.md");

    /// <summary>
    /// NDLRMAF024: All edges from a fan-out node are optional.
    /// </summary>
    public static readonly DiagnosticDescriptor GraphAllEdgesOptional = new(
        id: MafDiagnosticIds.GraphAllEdgesOptional,
        title: "All edges from fan-out node are optional",
        messageFormat: "All {0} outgoing edges from '{1}' in graph '{2}' have IsRequired = false. If all optional branches fail, the graph produces empty results.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A fan-out node (a node with multiple outgoing edges) has all edges marked as optional (IsRequired = false). If every optional branch fails at runtime, the downstream nodes receive no input and the graph may produce empty or unexpected results. Consider making at least one edge required.",
        helpLinkUri: HelpLinkBase + "NDLRMAF024.md",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// NDLRMAF027: A terminal node has outgoing edges.
    /// </summary>
    public static readonly DiagnosticDescriptor GraphTerminalNodeHasOutgoingEdges = new(
        id: MafDiagnosticIds.GraphTerminalNodeHasOutgoingEdges,
        title: "Terminal node has outgoing edges",
        messageFormat: "'{0}' is marked as a terminal node via [AgentGraphNode(IsTerminal = true)] in graph '{1}' but also has outgoing [AgentGraphEdge] declarations. Terminal nodes must not have outgoing edges.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A node marked with IsTerminal = true on [AgentGraphNode] is expected to be a leaf node with no outgoing edges. Having both IsTerminal = true and [AgentGraphEdge] declarations is contradictory. Either remove IsTerminal = true or remove the outgoing edges.",
        helpLinkUri: HelpLinkBase + "NDLRMAF027.md",
        customTags: WellKnownDiagnosticTags.CompilationEnd);
}
