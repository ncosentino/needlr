; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 0.0.2-alpha.18

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NDLRGEN014 | NexusLabs.Needlr.Generators | Error | OptionsAttributeAnalyzer, Validator type has no validation method
NDLRGEN015 | NexusLabs.Needlr.Generators | Error | OptionsAttributeAnalyzer, Validator type mismatch
NDLRGEN016 | NexusLabs.Needlr.Generators | Error | OptionsAttributeAnalyzer, Validation method not found
NDLRGEN017 | NexusLabs.Needlr.Generators | Error | OptionsAttributeAnalyzer, Validation method has wrong signature
NDLRGEN018 | NexusLabs.Needlr.Generators | Warning | OptionsAttributeAnalyzer, Validator won't run
NDLRGEN019 | NexusLabs.Needlr.Generators | Warning | OptionsAttributeAnalyzer, Validation method won't run
NDLRGEN020 | NexusLabs.Needlr.Generators | Error | TypeRegistryGenerator, [Options] is not compatible with AOT
NDLRGEN021 | NexusLabs.Needlr.Generators | Warning | TypeRegistryGenerator, Positional record must be partial for [Options]
NDLRGEN030 | NexusLabs.Needlr.Generators | Warning | UnsupportedDataAnnotationAnalyzer, DataAnnotation cannot be source-generated

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

