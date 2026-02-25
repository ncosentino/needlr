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
}
