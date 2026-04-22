; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NDLRMAF016 | NexusLabs.Needlr.AgentFramework | Error | AgentGraphCycleAnalyzer, Cycle detected in agent graph
NDLRMAF017 | NexusLabs.Needlr.AgentFramework | Error | AgentGraphEntryPointAnalyzer, Graph has no entry point
NDLRMAF018 | NexusLabs.Needlr.AgentFramework | Error | AgentGraphEntryPointAnalyzer, Graph has multiple entry points
NDLRMAF019 | NexusLabs.Needlr.AgentFramework | Error | AgentGraphTopologyAnalyzer, Graph edge target is not a declared agent
NDLRMAF020 | NexusLabs.Needlr.AgentFramework | Warning | AgentGraphTopologyAnalyzer, Graph edge source is not a declared agent
NDLRMAF021 | NexusLabs.Needlr.AgentFramework | Warning | AgentGraphTopologyAnalyzer, Graph entry point is not a declared agent
NDLRMAF022 | NexusLabs.Needlr.AgentFramework | Warning | AgentGraphReachabilityAnalyzer, Graph contains unreachable agents
NDLRMAF024 | NexusLabs.Needlr.AgentFramework | Warning | AgentGraphOptionalFanOutAnalyzer, All edges from fan-out node are optional
NDLRMAF027 | NexusLabs.Needlr.AgentFramework | Error | AgentGraphTerminalNodeAnalyzer, Terminal node has outgoing edges
NDLRMAF025 | NexusLabs.Needlr.AgentFramework | Error | WaitAnyCreateGraphAnalyzer, CreateGraphWorkflow is incompatible with GraphJoinMode.WaitAny
NDLRMAF028 | NexusLabs.Needlr.AgentFramework | Error | AgentGraphConditionMethodAnalyzer, Condition method not found or has wrong signature
NDLRMAF029 | NexusLabs.Needlr.AgentFramework | Error | AgentGraphReducerMethodAnalyzer, Reducer method not found or has wrong signature
