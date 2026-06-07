; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NDLRLOG001 | NexusLabs.Needlr.Logging | Error | NeedlrLoggerMessageAnalyzer, Method must be partial
NDLRLOG002 | NexusLabs.Needlr.Logging | Error | NeedlrLoggerMessageAnalyzer, Method must return void
NDLRLOG003 | NexusLabs.Needlr.Logging | Error | NeedlrLoggerMessageAnalyzer, Method must not be generic
NDLRLOG004 | NexusLabs.Needlr.Logging | Error | NeedlrLoggerMessageAnalyzer, Containing type must be partial
NDLRLOG005 | NexusLabs.Needlr.Logging | Error | NeedlrLoggerMessageAnalyzer, No accessible ILogger
NDLRLOG006 | NexusLabs.Needlr.Logging | Info | NeedlrLoggerMessageAnalyzer, Too many non-exception parameters for the Define fast path
