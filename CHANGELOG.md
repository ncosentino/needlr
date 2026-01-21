# Changelog

All notable changes to Needlr will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.0.2-alpha.5] - 2026-01-21

### Added
- **Generic Host Support**: New `NexusLabs.Needlr.Hosting` package for non-web .NET applications
  - `IHostApplicationBuilderPlugin` for configuring `HostApplicationBuilder`
  - `IHostPlugin` for configuring `IHost` after build
  - `ForHost()` and `ForHostApplicationBuilder()` extension methods on Syringe
  - Works with both source generation and reflection strategies
- **`[DeferToContainer]` Attribute**: Enables source generation for partial classes that receive constructors from other source generators
  - Declare expected constructor parameter types that another generator will add
  - Needlr generates correct factory code based on the declaration
  - Compile-time validation ensures declared types match actual generated constructor
- **Hosting Example Projects**: 
  - `WorkerServiceExample` (reflection) - Background worker service
  - `HostBuilderIntegrationExample` (reflection) - Console app with hosted services
  - `WorkerServiceSourceGen` (source generation) - AOT-compatible worker service
  - `HostBuilderIntegrationSourceGen` (source generation) - AOT-compatible console app
- **Comprehensive XML Documentation**: All public types and methods now have XML doc comments
- **Open Generic Type Tests**: Test coverage for generic type handling parity between reflection and source generation

### Fixed
- **Open Generic Types Bug**: Source generator now correctly excludes open generic type definitions (e.g., `JobScheduler<TJob>`) matching reflection behavior
  - Previously generated invalid `typeof(JobScheduler<TJob>)` syntax
  - Now correctly excluded since open generics cannot be instantiated
  - `GetFullyQualifiedName` outputs valid open generic syntax (`MyClass<>`) if ever needed elsewhere

### Changed
- Example projects reorganized: `Hosting/Reflection/` and `Hosting/SourceGen/` subfolders
- Documentation updated with "Working with Other Source Generators" section

## [0.0.2-alpha.1] - 2026-01-19

### ⚠️ Breaking Changes
- Removed `IAssembyProviderBuilder` interface

### Added
- Source-generation type providers (`GeneratedTypeProvider`, `GeneratedPluginProvider`)
- Module initializer bootstrap for zero-reflection startup
- Source-generation plugin factory (`GeneratedPluginFactory`)
- Reflection-based type providers (`ReflectionTypeProvider`, `ReflectionPluginProvider`)
- Bundle package with auto-configuration (source-gen first, reflection fallback)
- Roslyn analyzers for common Needlr mistakes
- Source-generation support for SignalR hub registration
- Source-generation support for Semantic Kernel plugin discovery
- AOT/Trimming example applications (console and web)
- Bundle auto-configuration example
- Performance benchmarks comparing source-gen vs reflection
- Parallel CI/CD with AOT publish validation
- Changelog generator agent skill

### Changed
- `AssembyProviderBuilder` renamed to `AssemblyProviderBuilder`
- `IAssembyProviderBuilderExtensions` renamed to `IAssemblyProviderBuilderExtensions`
- `PluginFactory` renamed to `ReflectionPluginFactory`
- `DefaultTypeRegistrar` renamed to `ReflectionTypeRegistrar`
- `PluginFactoryTests` renamed to `ReflectionPluginFactoryTests`
- Source generation is now the default pattern (reflection is opt-in)
- SignalR now accepts `IPluginFactory` via dependency injection (no hardcoded reflection)
- Semantic Kernel now accepts `IPluginFactory` via dependency injection
- ASP.NET package decoupled from Bundle (explicit strategy choice required)
- Simplified Syringe API with provider-based architecture
- `UsingScrutorTypeRegistrar()` renamed to `UsingScrutor()`

### Removed
- `DefaultAssemblyLoader` (assembly loading now handled by providers)
- `DefaultAssemblySorter` (assembly sorting no longer needed)
- `SyringeExtensions` (consolidated or moved)

_(249 files changed, +13355/-545 lines)_

## [0.0.1-alpha.19] - 2026-01-18

Initial alpha releases with reflection-based architecture.
