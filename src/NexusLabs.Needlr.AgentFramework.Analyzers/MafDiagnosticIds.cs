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
}
