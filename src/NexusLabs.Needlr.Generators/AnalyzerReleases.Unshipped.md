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
NDLRHTTP001 | NexusLabs.Needlr.Generators | Error | HttpClientOptionsAnalyzer, [HttpClientOptions] target must implement INamedHttpClientOptions
NDLRHTTP002 | NexusLabs.Needlr.Generators | Error | HttpClientOptionsAnalyzer, HttpClient name sources conflict
NDLRHTTP003 | NexusLabs.Needlr.Generators | Error | HttpClientOptionsAnalyzer, ClientName property body is not a literal expression
NDLRHTTP004 | NexusLabs.Needlr.Generators | Error | HttpClientOptionsAnalyzer, Resolved HttpClient name is empty
NDLRHTTP005 | NexusLabs.Needlr.Generators | Error | HttpClientOptionsAnalyzer, Duplicate HttpClient name
NDLRHTTP006 | NexusLabs.Needlr.Generators | Error | HttpClientOptionsAnalyzer, ClientName property has wrong shape
