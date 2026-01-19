# Changelog

All notable changes to Needlr will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.0.2-alpha.1] - 2026-01-19

### ⚠️ Breaking Changes

This release represents a major architectural shift to **source-generation-first** design. Reflection is now an explicit opt-in alternative.

#### Package Structure Changes

| Old Pattern | New Pattern | Notes |
|-------------|-------------|-------|
| `NexusLabs.Needlr.Injection` | `NexusLabs.Needlr.Injection` | Core abstractions only |
| (implicit reflection) | `NexusLabs.Needlr.Injection.SourceGen` | **Recommended** - AOT-friendly |
| (implicit reflection) | `NexusLabs.Needlr.Injection.Reflection` | Explicit opt-in for reflection |
| (n/a) | `NexusLabs.Needlr.Injection.Bundle` | Both strategies with auto-detection |

#### Removed Interfaces

The following interfaces have been **removed** in favor of the new provider-based architecture:

- `ITypeRegistrar` → Use `IInjectableTypeProvider`
- `ITypeFilterer` → Filtering now happens at provider construction
- `IServiceCollectionPopulator` → Use `ProviderBasedServiceProviderBuilder`

#### API Changes

```csharp
// Old (0.0.1)
new Syringe()
    .UsingScrutorTypeRegistrar()
    .UsingTypeFilterer(...)
    .BuildServiceProvider();

// New (0.0.2)
new Syringe()
    .UsingScrutor()           // Simplified
    .UsingReflection()        // Explicit reflection opt-in
    .BuildServiceProvider();

// Source-gen (recommended)
new Syringe()
    .UsingSourceGeneration()  // AOT-friendly, no reflection
    .BuildServiceProvider();
```

### Added

- **`IInjectableTypeProvider`** - New interface for injectable type discovery without per-call assembly parameters
- **`IPluginTypeProvider`** - New interface for plugin discovery with `CreatePlugins<T>()` and `GetAllPluginTypes()`
- **`ProviderBasedServiceProviderBuilder`** - Unified builder using new provider interfaces
- **`GeneratedTypeProvider`** / **`GeneratedPluginProvider`** - Source-gen implementations
- **`ReflectionTypeProvider`** / **`ReflectionPluginProvider`** - Reflection implementations
- **`ScrutorTypeProvider`** - Scrutor-based scanning
- **AOT Console Example** - Demonstrates fully trimmed/AOT app with source generation
- **Bundle Sample App** - Shows auto-detection between source-gen and reflection
- **Roslyn Analyzers** - NDLR001-NDLR004 for common mistakes
- **Benchmarks** - Plugin discovery and type registration performance comparison
- **Parallel CI/CD** - AOT publishing runs in parallel with tests

### Changed

- `UsingScrutorTypeRegistrar()` → `UsingScrutor()` (simplified naming)
- Assemblies are now configured at provider construction, not per-call
- Source-gen path no longer performs any assembly filtering (fast path)
- `ActivatorUtilities.CreateInstance` used as fallback when no factory provided

### Removed

- `ITypeRegistrar` interface and all implementations
- `ITypeFilterer` interface and all implementations  
- `IServiceCollectionPopulator` interface
- `ServiceCollectionPopulator` class
- `GeneratedServiceProviderBuilder`, `ReflectionServiceProviderBuilder`, `ServiceProviderBuilder`
- `TypeFilterers/` and `TypeRegistrars/` folders from all packages

### Migration Guide

**For source-generation users (recommended):**
```csharp
// Reference: NexusLabs.Needlr.Injection.SourceGen
var sp = new Syringe()
    .UsingSourceGeneration()
    .BuildServiceProvider();
```

**For reflection users:**
```csharp
// Reference: NexusLabs.Needlr.Injection.Reflection
var sp = new Syringe()
    .UsingReflection()
    .BuildServiceProvider();
```

**For auto-detection (tries source-gen first, falls back to reflection):**
```csharp
// Reference: NexusLabs.Needlr.Injection.Bundle
var sp = new Syringe()
    .UsingAutoConfiguration()
    .BuildServiceProvider();
```

---

## [0.0.1-alpha.19] - 2026-01-18

Initial alpha releases with reflection-based architecture.
