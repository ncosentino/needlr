; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NDLRCOR012 | NexusLabs.Needlr | Error | Disposable captive dependency - longer-lived service holds IDisposable with shorter lifetime
NDLRCOR016 | NexusLabs.Needlr | Warning | [DoNotAutoRegister] applied directly to a plugin class is redundant
