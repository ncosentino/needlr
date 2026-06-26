; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NDLRGEN035 | NexusLabs.Needlr.Generators | Error | RegisterClosedOverImplementationsOfAttributeAnalyzer, [RegisterClosedOverImplementationsOf] source must be an open generic interface
NDLRGEN036 | NexusLabs.Needlr.Generators | Error | RegisterClosedOverImplementationsOfAttributeAnalyzer, [RegisterClosedOverImplementationsOf] composition must be an open generic class
NDLRGEN037 | NexusLabs.Needlr.Generators | Error | RegisterClosedOverImplementationsOfAttributeAnalyzer, [RegisterClosedOverImplementationsOf] composition must implement the As service type
NDLRGEN038 | NexusLabs.Needlr.Generators | Warning | TypeRegistryGenerator, [RegisterClosedOverImplementationsOf] discovered type argument violates composition constraints
