; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NDLRCOR003 | NexusLabs.Needlr | Error | DeferToContainerInGeneratedCodeAnalyzer, [DeferToContainer] attribute in generated code is ignored
NDLRCOR004 | NexusLabs.Needlr | Warning | GlobalNamespaceTypeAnalyzer, Injectable type in global namespace may not be discovered
NDLRCOR005 | NexusLabs.Needlr | Warning | LifestyleMismatchAnalyzer, Lifestyle mismatch: longer-lived service depends on shorter-lived service
NDLRCOR006 | NexusLabs.Needlr | Error | CircularDependencyAnalyzer, Circular dependency detected
