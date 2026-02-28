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
}
