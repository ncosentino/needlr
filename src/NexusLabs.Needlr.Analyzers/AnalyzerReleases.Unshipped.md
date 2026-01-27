; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NDLRCOR003 | NexusLabs.Needlr | Error | DeferToContainerInGeneratedCodeAnalyzer, [DeferToContainer] attribute in generated code is ignored
NDLRCOR004 | NexusLabs.Needlr | Warning | GlobalNamespaceTypeAnalyzer, Injectable type in global namespace may not be discovered
NDLRCOR005 | NexusLabs.Needlr | Warning | LifetimeMismatchAnalyzer, Lifetime mismatch: longer-lived service depends on shorter-lived service
NDLRCOR006 | NexusLabs.Needlr | Error | CircularDependencyAnalyzer, Circular dependency detected
NDLRCOR007 | NexusLabs.Needlr | Error | InterceptAttributeAnalyzer, Intercept type must implement IMethodInterceptor
NDLRCOR008 | NexusLabs.Needlr | Warning | InterceptAttributeAnalyzer, [Intercept] applied to class without interfaces
NDLRCOR009 | NexusLabs.Needlr | Info | LazyResolutionAnalyzer, Lazy<T> references undiscovered type
NDLRCOR010 | NexusLabs.Needlr | Info | CollectionResolutionAnalyzer, IEnumerable<T> has no discovered implementations
NDLRCOR011 | NexusLabs.Needlr | Info | KeyedServiceResolutionAnalyzer, [FromKeyedServices] references unknown key
NDLRCOR012 | NexusLabs.Needlr | Warning | GenerateFactoryAttributeAnalyzer, [GenerateFactory] unnecessary - all parameters are injectable
NDLRCOR013 | NexusLabs.Needlr | Warning | GenerateFactoryAttributeAnalyzer, [GenerateFactory] has low value - no injectable parameters
NDLRCOR014 | NexusLabs.Needlr | Error | GenerateFactoryAttributeAnalyzer, [GenerateFactory<T>] type argument not implemented
