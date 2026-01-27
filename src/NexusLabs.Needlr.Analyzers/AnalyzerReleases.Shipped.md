; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 0.0.2

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NDLRCOR001 | NexusLabs.Needlr | Error | ReflectionInAotProjectAnalyzer, Reflection API used in AOT project
NDLRCOR002 | NexusLabs.Needlr | Warning | PluginConstructorDependenciesAnalyzer, Plugin has constructor dependencies
NDLRCOR003 | NexusLabs.Needlr | Error | DeferToContainerInGeneratedCodeAnalyzer, [DeferToContainer] attribute in generated code is ignored
NDLRCOR004 | NexusLabs.Needlr | Warning | GlobalNamespaceTypeAnalyzer, Injectable type in global namespace may not be discovered
NDLRCOR005 | NexusLabs.Needlr | Warning | LifetimeMismatchAnalyzer, Lifetime mismatch: longer-lived service depends on shorter-lived service
NDLRCOR006 | NexusLabs.Needlr | Error | CircularDependencyAnalyzer, Circular dependency detected
NDLRCOR007 | NexusLabs.Needlr | Error | InterceptAttributeAnalyzer, Intercept type must implement IMethodInterceptor
NDLRCOR008 | NexusLabs.Needlr | Warning | InterceptAttributeAnalyzer, [Intercept] applied to class without interfaces
NDLRCOR009 | NexusLabs.Needlr | Info | LazyResolutionAnalyzer, Lazy<T> references undiscovered type
NDLRCOR010 | NexusLabs.Needlr | Info | CollectionResolutionAnalyzer, IEnumerable<T> has no discovered implementations
NDLRCOR011 | NexusLabs.Needlr | Info | KeyedServiceResolutionAnalyzer, [FromKeyedServices] references unknown key
