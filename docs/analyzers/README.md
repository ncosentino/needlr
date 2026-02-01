# Needlr Analyzers

Needlr includes optional Roslyn analyzers to help developers avoid common mistakes and ensure best practices.

## Core Analyzers (NexusLabs.Needlr.Analyzers)

These analyzers are included with the `NexusLabs.Needlr` package.

| Rule ID | Severity | Description |
|---------|----------|-------------|
| [NDLRCOR001](NDLRCOR001.md) | Error | Reflection API used in AOT project |
| [NDLRCOR002](NDLRCOR002.md) | Warning | Plugin has constructor dependencies |
| [NDLRCOR003](NDLRCOR003.md) | Error | [DeferToContainer] attribute in generated code is ignored |
| [NDLRCOR004](NDLRCOR004.md) | Warning | Injectable type in global namespace may not be discovered |
| [NDLRCOR005](NDLRCOR005.md) | Warning | Lifetime mismatch: longer-lived service depends on shorter-lived service |
| [NDLRCOR006](NDLRCOR006.md) | Error | Circular dependency detected |
| [NDLRCOR007](NDLRCOR007.md) | Error | Intercept type must implement IMethodInterceptor |
| [NDLRCOR008](NDLRCOR008.md) | Warning | [Intercept] applied to class without interfaces |
| [NDLRCOR009](NDLRCOR009.md) | Info | Lazy<T> references undiscovered type |
| [NDLRCOR010](NDLRCOR010.md) | Info | IEnumerable<T> has no discovered implementations |
| [NDLRCOR011](NDLRCOR011.md) | Info | [FromKeyedServices] keyed service usage tracking |
| [NDLRCOR012](NDLRCOR012.md) | Error | Disposable captive dependency |
| [NDLRCOR015](NDLRCOR015.md) | Error | [RegisterAs<T>] type T not implemented by class |

## SignalR Analyzers (NexusLabs.Needlr.SignalR.Analyzers)

These analyzers are included with the `NexusLabs.Needlr.SignalR` package.

| Rule ID | Severity | Description |
|---------|----------|-------------|
| [NDLRSIG001](NDLRSIG001.md) | Warning | HubPath must be a constant expression |
| [NDLRSIG002](NDLRSIG002.md) | Warning | HubType must be a typeof expression |

## Generator Diagnostics (NexusLabs.Needlr.Generators)

These diagnostics are emitted by the source generator to detect configuration issues at compile time.

| Rule ID | Severity | Description |
|---------|----------|-------------|
| [NDLRGEN001](NDLRGEN001.md) | Error | Internal type in referenced assembly cannot be registered |
| [NDLRGEN002](NDLRGEN002.md) | Error | Referenced assembly has internal plugin types but no type registry |
| [NDLRGEN003](NDLRGEN003.md) | Warning | [GenerateFactory] all parameters are injectable |
| [NDLRGEN004](NDLRGEN004.md) | Warning | [GenerateFactory] no parameters are injectable |
| [NDLRGEN005](NDLRGEN005.md) | Error | [GenerateFactory<T>] type T not implemented by class |
| [NDLRGEN006](NDLRGEN006.md) | Error | [OpenDecoratorFor] type must be an open generic interface |
| [NDLRGEN007](NDLRGEN007.md) | Error | [OpenDecoratorFor] decorator must be an open generic class |
| [NDLRGEN008](NDLRGEN008.md) | Error | [OpenDecoratorFor] decorator must implement the interface |
| [NDLRGEN014](NDLRGEN014.md) | Error | [Options] Validator type has no validation method |
| [NDLRGEN015](NDLRGEN015.md) | Error | [Options] Validator type mismatch |
| [NDLRGEN016](NDLRGEN016.md) | Error | [Options] Validation method not found |
| [NDLRGEN017](NDLRGEN017.md) | Error | [Options] Validation method has wrong signature |
| [NDLRGEN018](NDLRGEN018.md) | Warning | [Options] Validator won't run (ValidateOnStart is false) |
| [NDLRGEN019](NDLRGEN019.md) | Warning | [Options] Validation method won't run (ValidateOnStart is false) |
| [NDLRGEN020](NDLRGEN020.md) | Error | [Options] is not compatible with Native AOT |
| [NDLRGEN021](NDLRGEN021.md) | Warning | [Options] Positional record must be partial |
| [NDLRGEN022](NDLRGEN022.md) | Error | Disposable captive dependency (uses inferred lifetimes) |
| [NDLRGEN031](NDLRGEN031.md) | Error | [Provider] on class requires partial modifier |
| [NDLRGEN032](NDLRGEN032.md) | Error | [Provider] interface has invalid member |
| [NDLRGEN033](NDLRGEN033.md) | Warning | [Provider] property uses concrete type |
| [NDLRGEN034](NDLRGEN034.md) | Error | [Provider] circular dependency detected |

## Diagnostic ID Naming Convention

Needlr uses a component-based naming convention for diagnostic IDs:

| Component | Prefix | Example |
|-----------|--------|---------|
| Core Analyzers | `NDLRCOR` | `NDLRCOR001` |
| SignalR Analyzers | `NDLRSIG` | `NDLRSIG001` |
| Source Generators | `NDLRGEN` | `NDLRGEN001` |

## Suppressing Warnings

To suppress a specific analyzer warning, use pragma directives:

```csharp
#pragma warning disable NDLRCOR001
// Code that triggers the warning
#pragma warning restore NDLRCOR001
```

Or suppress in your project file for the entire project:

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);NDLRCOR002</NoWarn>
</PropertyGroup>
```

Or configure severity in `.editorconfig` (recommended):

```ini
# .editorconfig
[*.cs]
# Disable a diagnostic
dotnet_diagnostic.NDLRCOR009.severity = none

# Promote to warning
dotnet_diagnostic.NDLRCOR010.severity = warning

# Promote to error
dotnet_diagnostic.NDLRCOR005.severity = error
```

## Resolution Validation Analyzers

NDLRCOR009, NDLRCOR010, and NDLRCOR011 are **resolution validation analyzers** that help catch potential issues with service resolution patterns.

These analyzers:

- Only activate when `[assembly: GenerateTypeRegistry]` is present
- Default to `Info` severity (non-blocking)
- Can be promoted to `Warning` or `Error` via `.editorconfig`

| Analyzer | Purpose |
|----------|---------|
| NDLRCOR009 | Validates `Lazy<T>` references discoverable types |
| NDLRCOR010 | Validates `IEnumerable<T>` has implementations |
| NDLRCOR011 | Tracks `[FromKeyedServices]` keyed service usage |

To see which analyzers are active in your project, enable diagnostics output:

```xml
<PropertyGroup>
  <NeedlrDiagnostics>true</NeedlrDiagnostics>
</PropertyGroup>
```

This generates `AnalyzerStatus.md` in your output directory showing all analyzers and their current severity.

## Configuration

Analyzers are automatically enabled when you reference the Needlr packages. No additional configuration is required.

For AOT projects, ensure your project has the appropriate settings for the analyzers to detect AOT mode:

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <!-- or -->
  <IsAotCompatible>true</IsAotCompatible>
</PropertyGroup>
```
