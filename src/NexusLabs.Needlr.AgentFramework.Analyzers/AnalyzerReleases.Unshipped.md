; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NDLRMAF001 | NexusLabs.Needlr.AgentFramework | Error | AgentHandoffsTo target type is not decorated with [NeedlrAiAgent]
NDLRMAF002 | NexusLabs.Needlr.AgentFramework | Error | AgentGroupChatMember group has fewer than two members in this compilation
NDLRMAF003 | NexusLabs.Needlr.AgentFramework | Warning | Class with [AgentHandoffsTo] is not itself decorated with [NeedlrAiAgent]
NDLRMAF004 | NexusLabs.Needlr.AgentFramework | Warning | Cyclic handoff chain detected
