namespace NexusLabs.Needlr.AgentFramework.Analyzers;

/// <summary>
/// Contains diagnostic IDs for all Needlr Agent Framework analyzers.
/// </summary>
/// <remarks>
/// Agent Framework analyzer codes use the NDLRMAF prefix.
/// </remarks>
public static class MafDiagnosticIds
{
    /// <summary>
    /// NDLRMAF001: <c>[AgentHandoffsTo(typeof(X))]</c> target type X is not decorated with <c>[NeedlrAiAgent]</c>.
    /// </summary>
    public const string HandoffsToTargetNotNeedlrAgent = "NDLRMAF001";

    /// <summary>
    /// NDLRMAF002: <c>[AgentGroupChatMember("g")]</c> group "g" has fewer than two members in this compilation.
    /// </summary>
    public const string GroupChatTooFewMembers = "NDLRMAF002";

    /// <summary>
    /// NDLRMAF003: A class has <c>[AgentHandoffsTo]</c> but is not itself decorated with <c>[NeedlrAiAgent]</c>.
    /// </summary>
    public const string HandoffsToSourceNotNeedlrAgent = "NDLRMAF003";

    /// <summary>
    /// NDLRMAF004: A cyclic handoff chain was detected (e.g. A → B → A).
    /// </summary>
    public const string CyclicHandoffChain = "NDLRMAF004";

    /// <summary>
    /// NDLRMAF005: An agent declares a <c>FunctionGroups</c> entry whose name has no matching
    /// <c>[AgentFunctionGroup("name")]</c> class in this compilation.
    /// </summary>
    public const string UnresolvedFunctionGroupReference = "NDLRMAF005";

    /// <summary>
    /// NDLRMAF006: Two or more agents in the same <c>[AgentSequenceMember]</c> pipeline declare
    /// the same <c>Order</c> value.
    /// </summary>
    public const string DuplicateSequenceOrder = "NDLRMAF006";

    /// <summary>
    /// NDLRMAF007: The <c>Order</c> values within a named <c>[AgentSequenceMember]</c> pipeline
    /// are not contiguous (a gap exists).
    /// </summary>
    public const string GapInSequenceOrder = "NDLRMAF007";

    /// <summary>
    /// NDLRMAF008: A class decorated with <c>[NeedlrAiAgent]</c> participates in no topology
    /// declaration (<c>[AgentHandoffsTo]</c>, <c>[AgentGroupChatMember]</c>, or
    /// <c>[AgentSequenceMember]</c>).
    /// </summary>
    public const string OrphanAgent = "NDLRMAF008";

    /// <summary>
    /// NDLRMAF009: <c>[WorkflowRunTerminationCondition]</c> is declared on a class that is not
    /// decorated with <c>[NeedlrAiAgent]</c>.
    /// </summary>
    public const string WorkflowRunTerminationConditionOnNonAgent = "NDLRMAF009";

    /// <summary>
    /// NDLRMAF010: The <c>conditionType</c> passed to <c>[WorkflowRunTerminationCondition]</c>
    /// or <c>[AgentTerminationCondition]</c> does not implement
    /// <c>IWorkflowTerminationCondition</c>.
    /// </summary>
    public const string TerminationConditionTypeInvalid = "NDLRMAF010";

    /// <summary>
    /// NDLRMAF011: <c>[WorkflowRunTerminationCondition]</c> is declared on a
    /// <c>[AgentGroupChatMember]</c> class; prefer <c>[AgentTerminationCondition]</c> for group
    /// chat members.
    /// </summary>
    public const string PreferAgentTerminationConditionForGroupChat = "NDLRMAF011";

    /// <summary>
    /// NDLRMAF012: A method decorated with <c>[AgentFunction]</c> has no
    /// <c>[System.ComponentModel.Description]</c> attribute.
    /// </summary>
    public const string AgentFunctionMissingDescription = "NDLRMAF012";

    /// <summary>
    /// NDLRMAF013: A parameter of an <c>[AgentFunction]</c> method (other than
    /// <c>CancellationToken</c>) has no <c>[System.ComponentModel.Description]</c> attribute.
    /// </summary>
    public const string AgentFunctionParameterMissingDescription = "NDLRMAF013";

    /// <summary>
    /// NDLRMAF014: A type listed in <c>FunctionTypes</c> on a <c>[NeedlrAiAgent]</c> has no
    /// methods decorated with <c>[AgentFunction]</c>, so the agent silently receives zero tools
    /// from that type.
    /// </summary>
    public const string AgentFunctionTypesMiswired = "NDLRMAF014";

    /// <summary>
    /// NDLRMAF015: <c>.ToString()</c> is called on <c>ToolCallResult.Result</c> or
    /// <c>FunctionResultContent.Result</c>, which are <c>object?</c> and may contain
    /// a <c>JsonElement</c>. Use <c>ToolResultSerializer.Serialize()</c> instead.
    /// </summary>
    public const string ToolResultToStringCall = "NDLRMAF015";

    /// <summary>
    /// NDLRMAF016: A cycle was detected in a named agent graph declared via
    /// <c>[AgentGraphEdge]</c> attributes.
    /// </summary>
    public const string GraphCycleDetected = "NDLRMAF016";

    /// <summary>
    /// NDLRMAF017: A named agent graph has no <c>[AgentGraphEntry]</c> declaration.
    /// </summary>
    public const string GraphNoEntryPoint = "NDLRMAF017";

    /// <summary>
    /// NDLRMAF018: A named agent graph has multiple <c>[AgentGraphEntry]</c> declarations.
    /// </summary>
    public const string GraphMultipleEntryPoints = "NDLRMAF018";

    /// <summary>
    /// NDLRMAF019: An <c>[AgentGraphEdge]</c> references a target type that is not
    /// decorated with <c>[NeedlrAiAgent]</c>.
    /// </summary>
    public const string GraphEdgeTargetNotAgent = "NDLRMAF019";

    /// <summary>
    /// NDLRMAF020: A class has <c>[AgentGraphEdge]</c> but is not itself decorated
    /// with <c>[NeedlrAiAgent]</c>.
    /// </summary>
    public const string GraphEdgeSourceNotAgent = "NDLRMAF020";

    /// <summary>
    /// NDLRMAF021: A class has <c>[AgentGraphEntry]</c> but is not itself decorated
    /// with <c>[NeedlrAiAgent]</c>.
    /// </summary>
    public const string GraphEntryPointNotAgent = "NDLRMAF021";

    /// <summary>
    /// NDLRMAF022: A named agent graph contains agents that are not reachable from
    /// the entry point.
    /// </summary>
    public const string GraphUnreachableAgent = "NDLRMAF022";

    // NDLRMAF023 was previously used for MaxSupersteps validation.
    // The MaxSupersteps property was removed from AgentGraphEntryAttribute.
    // ID is retired — do not reuse.

    /// <summary>
    /// NDLRMAF024: All outgoing edges from a fan-out node have
    /// <c>IsRequired = false</c>, meaning the graph could produce empty results if all
    /// optional branches fail.
    /// </summary>
    public const string GraphAllEdgesOptional = "NDLRMAF024";

    /// <summary>
    /// NDLRMAF027: A terminal node (a node that should have no outgoing edges) has
    /// outgoing <c>[AgentGraphEdge]</c> declarations.
    /// </summary>
    public const string GraphTerminalNodeHasOutgoingEdges = "NDLRMAF027";

    /// <summary>
    /// NDLRMAF025: <c>CreateGraphWorkflow</c> is called on a graph that contains a
    /// <c>[AgentGraphNode(JoinMode = GraphJoinMode.WaitAny)]</c> declaration.
    /// <c>CreateGraphWorkflow</c> returns a MAF <c>Workflow</c> that uses BSP execution,
    /// which does not support WaitAny. Use <c>RunGraphAsync</c> instead.
    /// </summary>
    public const string WaitAnyIncompatibleWithCreateGraphWorkflow = "NDLRMAF025";

    /// <summary>
    /// NDLRMAF028: The <c>Condition</c> property on <c>[AgentGraphEdge]</c> references
    /// a method that does not exist on the source agent, is not static, or has the
    /// wrong signature. The method must be <c>static bool MethodName(object?)</c>.
    /// </summary>
    public const string GraphConditionMethodInvalid = "NDLRMAF028";

    /// <summary>
    /// NDLRMAF029: The <c>ReducerMethod</c> property on <c>[AgentGraphReducer]</c>
    /// references a method that does not exist on the decorated type, is not static,
    /// or has the wrong signature. The method must be
    /// <c>static string MethodName(IReadOnlyList&lt;string&gt;)</c>.
    /// </summary>
    public const string GraphReducerMethodInvalid = "NDLRMAF029";
}
