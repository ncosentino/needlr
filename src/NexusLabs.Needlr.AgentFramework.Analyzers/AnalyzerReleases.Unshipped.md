; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NDLRMAF001 | NexusLabs.Needlr.AgentFramework | Error | AgentHandoffsTo target type is not decorated with [NeedlrAiAgent]
NDLRMAF002 | NexusLabs.Needlr.AgentFramework | Error | AgentGroupChatMember group has fewer than two members in this compilation
NDLRMAF003 | NexusLabs.Needlr.AgentFramework | Warning | Class with [AgentHandoffsTo] is not itself decorated with [NeedlrAiAgent]
NDLRMAF004 | NexusLabs.Needlr.AgentFramework | Warning | Cyclic handoff chain detected
NDLRMAF005 | NexusLabs.Needlr.AgentFramework | Warning | FunctionGroups references a group name with no matching [AgentFunctionGroup] class
NDLRMAF006 | NexusLabs.Needlr.AgentFramework | Error | Duplicate Order value in [AgentSequenceMember] pipeline
NDLRMAF007 | NexusLabs.Needlr.AgentFramework | Warning | Gap in [AgentSequenceMember] Order sequence
NDLRMAF008 | NexusLabs.Needlr.AgentFramework | Info | Agent participates in no topology declaration
NDLRMAF009 | NexusLabs.Needlr.AgentFramework | Warning | [WorkflowRunTerminationCondition] declared on a non-agent class
NDLRMAF010 | NexusLabs.Needlr.AgentFramework | Error | Termination condition type does not implement IWorkflowTerminationCondition
NDLRMAF011 | NexusLabs.Needlr.AgentFramework | Info | Prefer [AgentTerminationCondition] over [WorkflowRunTerminationCondition] for group chat members
