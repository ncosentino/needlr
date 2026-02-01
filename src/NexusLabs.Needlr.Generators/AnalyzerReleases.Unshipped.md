; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NDLRGEN022 | NexusLabs.Needlr.Generators | Error | Disposable captive dependency detected
NDLRGEN031 | NexusLabs.Needlr.Generators | Error | ProviderAttributeAnalyzer, [Provider] on class requires partial modifier
NDLRGEN032 | NexusLabs.Needlr.Generators | Error | ProviderAttributeAnalyzer, [Provider] interface has invalid member
NDLRGEN033 | NexusLabs.Needlr.Generators | Warning | ProviderAttributeAnalyzer, Provider property uses concrete type
NDLRGEN034 | NexusLabs.Needlr.Generators | Error | ProviderAttributeAnalyzer, Circular provider dependency detected
