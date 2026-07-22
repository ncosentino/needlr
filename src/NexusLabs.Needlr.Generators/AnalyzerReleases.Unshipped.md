; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NDLRGEN039 | NexusLabs.Needlr.Generators | Error | GeneratedConstructorAnalyzer, Generated-constructor type must be partial
NDLRGEN040 | NexusLabs.Needlr.Generators | Error | GeneratedConstructorAnalyzer, Generated-constructor type shape is unsupported (record or nested type)
NDLRGEN041 | NexusLabs.Needlr.Generators | Error | GeneratedConstructorAnalyzer, Generated-constructor conflicts with an explicit constructor
NDLRGEN042 | NexusLabs.Needlr.Generators | Error | GeneratedConstructorAnalyzer, Generated-constructor base type requires a parameterless constructor
NDLRGEN043 | NexusLabs.Needlr.Generators | Error | GeneratedConstructorAnalyzer, No eligible field for generated-constructor generation
NDLRGEN044 | NexusLabs.Needlr.Generators | Error | GeneratedConstructorAnalyzer, Generated-constructor parameter names collide
NDLRGEN045 | NexusLabs.Needlr.Generators | Warning | GeneratedConstructorAnalyzer, Constructor guard attribute has no effect
NDLRGEN046 | NexusLabs.Needlr.Generators | Error | GeneratedConstructorAnalyzer, Constructor guard attribute applied to an ineligible field
NDLRGEN047 | NexusLabs.Needlr.Generators | Error | GeneratedConstructorAnalyzer, Invalid constructor guard enum value
NDLRGEN048 | NexusLabs.Needlr.Generators | Error | GeneratedConstructorAnalyzer, Constructor guard incompatible with field type
NDLRGEN049 | NexusLabs.Needlr.Generators | Error | GeneratedConstructorAnalyzer, Custom constructor guard type is invalid
NDLRGEN050 | NexusLabs.Needlr.Generators | Error | GeneratedConstructorAnalyzer, Custom constructor guard method name is invalid
NDLRGEN051 | NexusLabs.Needlr.Generators | Error | GeneratedConstructorAnalyzer, Custom constructor guard method is invalid
NDLRGEN052 | NexusLabs.Needlr.Generators | Error | GeneratedConstructorAnalyzer, Custom constructor guard method is ambiguous
NDLRGEN053 | NexusLabs.Needlr.Generators | Error | GeneratedConstructorAnalyzer, [ConstructorGuardDefinition] target is invalid
NDLRGEN054 | NexusLabs.Needlr.Generators | Error | GeneratedConstructorAnalyzer, [ConstructorGuardDefinition] guard contract is unresolved
NDLRGEN055 | NexusLabs.Needlr.Generators | Error | GeneratedConstructorAnalyzer, Constructor guard alias usage argument is unsupported
NDLRGEN056 | NexusLabs.Needlr.Generators | Error | GeneratedConstructorAnalyzer, Custom constructor guard method is incompatible with forwarded alias arguments
