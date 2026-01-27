; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 0.0.2

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NDLRGEN001 | NexusLabs.Needlr.Generators | Error | TypeRegistryGenerator, Internal type in referenced assembly cannot be registered
NDLRGEN002 | NexusLabs.Needlr.Generators | Error | TypeRegistryGenerator, Referenced assembly has internal plugin types but no type registry
NDLRGEN003 | NexusLabs.Needlr.Generators | Warning | GenerateFactoryAttributeAnalyzer, [GenerateFactory] unnecessary - all parameters are injectable
NDLRGEN004 | NexusLabs.Needlr.Generators | Warning | GenerateFactoryAttributeAnalyzer, [GenerateFactory] has low value - no injectable parameters
NDLRGEN005 | NexusLabs.Needlr.Generators | Error | GenerateFactoryAttributeAnalyzer, [GenerateFactory<T>] type argument not implemented
NDLRGEN006 | NexusLabs.Needlr.Generators | Error | OpenDecoratorForAttributeAnalyzer, [OpenDecoratorFor] type must be an open generic interface
NDLRGEN007 | NexusLabs.Needlr.Generators | Error | OpenDecoratorForAttributeAnalyzer, [OpenDecoratorFor] decorator must be an open generic class
NDLRGEN008 | NexusLabs.Needlr.Generators | Error | OpenDecoratorForAttributeAnalyzer, [OpenDecoratorFor] decorator must implement the interface

