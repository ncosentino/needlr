; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 0.0.2

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
NDLRMAF012 | NexusLabs.Needlr.AgentFramework | Warning | [AgentFunction] method is missing a [Description] attribute
NDLRMAF013 | NexusLabs.Needlr.AgentFramework | Warning | [AgentFunction] method parameter is missing a [Description] attribute
NDLRMAF014 | NexusLabs.Needlr.AgentFramework | Warning | FunctionTypes entry has no [AgentFunction] methods
NDLRMAF015 | NexusLabs.Needlr.AgentFramework | Warning | ToolResultToStringAnalyzer, Do not call ToString() on tool result objects
NDLRMAF016 | NexusLabs.Needlr.AgentFramework | Error | AgentGraphCycleAnalyzer, Cycle detected in agent graph
NDLRMAF017 | NexusLabs.Needlr.AgentFramework | Error | AgentGraphEntryPointAnalyzer, Graph has no entry point
NDLRMAF018 | NexusLabs.Needlr.AgentFramework | Error | AgentGraphEntryPointAnalyzer, Graph has multiple entry points
NDLRMAF019 | NexusLabs.Needlr.AgentFramework | Error | AgentGraphTopologyAnalyzer, Graph edge target is not a declared agent
NDLRMAF020 | NexusLabs.Needlr.AgentFramework | Warning | AgentGraphTopologyAnalyzer, Graph edge source is not a declared agent
NDLRMAF021 | NexusLabs.Needlr.AgentFramework | Warning | AgentGraphTopologyAnalyzer, Graph entry point is not a declared agent
NDLRMAF022 | NexusLabs.Needlr.AgentFramework | Warning | AgentGraphReachabilityAnalyzer, Graph contains unreachable agents
NDLRMAF024 | NexusLabs.Needlr.AgentFramework | Warning | AgentGraphOptionalFanOutAnalyzer, All edges from fan-out node are optional
NDLRMAF025 | NexusLabs.Needlr.AgentFramework | Error | WaitAnyCreateGraphAnalyzer, CreateGraphWorkflow is incompatible with GraphJoinMode.WaitAny
NDLRMAF027 | NexusLabs.Needlr.AgentFramework | Error | AgentGraphTerminalNodeAnalyzer, Terminal node has outgoing edges
NDLRMAF028 | NexusLabs.Needlr.AgentFramework | Error | AgentGraphConditionMethodAnalyzer, Condition method not found or has wrong signature
NDLRMAF029 | NexusLabs.Needlr.AgentFramework | Error | AgentGraphReducerMethodAnalyzer, Reducer method not found or has wrong signature
