; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 0.0.3

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
NDLRGEN014 | NexusLabs.Needlr.Generators | Error | OptionsAttributeAnalyzer, Validator type has no validation method
NDLRGEN015 | NexusLabs.Needlr.Generators | Error | OptionsAttributeAnalyzer, Validator type mismatch
NDLRGEN016 | NexusLabs.Needlr.Generators | Error | OptionsAttributeAnalyzer, Validation method not found
NDLRGEN017 | NexusLabs.Needlr.Generators | Error | OptionsAttributeAnalyzer, Validation method has wrong signature
NDLRGEN018 | NexusLabs.Needlr.Generators | Warning | OptionsAttributeAnalyzer, Validator won't run
NDLRGEN019 | NexusLabs.Needlr.Generators | Warning | OptionsAttributeAnalyzer, Validation method won't run
NDLRGEN020 | NexusLabs.Needlr.Generators | Error | TypeRegistryGenerator, [Options] is not compatible with AOT
NDLRGEN021 | NexusLabs.Needlr.Generators | Warning | TypeRegistryGenerator, Positional record must be partial for [Options]
NDLRGEN022 | NexusLabs.Needlr.Generators | Error | Disposable captive dependency detected
NDLRGEN030 | NexusLabs.Needlr.Generators | Warning | UnsupportedDataAnnotationAnalyzer, DataAnnotation cannot be source-generated
NDLRGEN031 | NexusLabs.Needlr.Generators | Error | ProviderAttributeAnalyzer, [Provider] on class requires partial modifier
NDLRGEN032 | NexusLabs.Needlr.Generators | Error | ProviderAttributeAnalyzer, [Provider] interface has invalid member
NDLRGEN033 | NexusLabs.Needlr.Generators | Warning | ProviderAttributeAnalyzer, Provider property uses concrete type
NDLRGEN034 | NexusLabs.Needlr.Generators | Error | ProviderAttributeAnalyzer, Circular provider dependency detected
NDLRGEN035 | NexusLabs.Needlr.Generators | Error | RegisterClosedOverImplementationsOfAttributeAnalyzer, [RegisterClosedOverImplementationsOf] source must be an open generic interface
NDLRGEN036 | NexusLabs.Needlr.Generators | Error | RegisterClosedOverImplementationsOfAttributeAnalyzer, [RegisterClosedOverImplementationsOf] composition must be an open generic class
NDLRGEN037 | NexusLabs.Needlr.Generators | Error | RegisterClosedOverImplementationsOfAttributeAnalyzer, [RegisterClosedOverImplementationsOf] composition must implement the As service type
NDLRGEN038 | NexusLabs.Needlr.Generators | Warning | TypeRegistryGenerator, [RegisterClosedOverImplementationsOf] discovered type argument violates composition constraints
NDLRHTTP001 | NexusLabs.Needlr.Generators | Error | HttpClientOptionsAnalyzer, [HttpClientOptions] target must implement INamedHttpClientOptions
NDLRHTTP002 | NexusLabs.Needlr.Generators | Error | HttpClientOptionsAnalyzer, HttpClient name sources conflict
NDLRHTTP003 | NexusLabs.Needlr.Generators | Error | HttpClientOptionsAnalyzer, ClientName property body is not a literal expression
NDLRHTTP004 | NexusLabs.Needlr.Generators | Error | HttpClientOptionsAnalyzer, Resolved HttpClient name is empty
NDLRHTTP005 | NexusLabs.Needlr.Generators | Error | HttpClientOptionsAnalyzer, Duplicate HttpClient name
NDLRHTTP006 | NexusLabs.Needlr.Generators | Error | HttpClientOptionsAnalyzer, ClientName property has wrong shape
