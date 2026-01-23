# Changelog

All notable changes to Needlr will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.0.2-alpha.13] - 2026-01-23

### Added
- **Expression-based Assembly Ordering API**: New unified API for ordering assemblies that works for both reflection and source-gen
  - `OrderAssemblies(order => order.By(...).ThenBy(...))` fluent builder
  - `AssemblyOrderBuilder` with tiered matching - assemblies go in first matching tier, unmatched always last
  - `AssemblyInfo` abstraction for expressions to work with both runtime assemblies and compile-time strings
  - Presets: `AssemblyOrder.LibTestEntry()`, `AssemblyOrder.TestsLast()`, `AssemblyOrder.Alphabetical()`
- **`UseLibTestEntryOrdering()` extension method**: Replaces `UseLibTestEntrySorting()` with consistent naming

### Changed
- **BREAKING**: `UseSorter()` removed from `IAssemblyProviderBuilder` - use `OrderAssemblies()` instead
- **BREAKING**: `UseAlphabeticalSorting()` removed - use `OrderAssemblies(order => order.By(a => true))` or `AssemblyOrder.Alphabetical()`
- **BREAKING**: `UseLibTestEntrySorting()` renamed to `UseLibTestEntryOrdering()`

### Removed
- **BREAKING**: `NeedlrAssemblyOrderAttribute` removed - assembly ordering is now configured at Syringe level
  - Was: `[assembly: NeedlrAssemblyOrder(First = new[] { "..." })]`
  - Now: `.OrderAssemblies(order => order.By(a => a.Name.StartsWith("...")))`
- **BREAKING**: `IAssemblySorter` interface and implementations removed
  - `AlphabeticalAssemblySorter`, `LibTestEntryAssemblySorter`, `ReflectionAssemblySorter` all removed

### Fixed
- **`UsingOnlyAsTransient<T>()` not working with source-gen**: `GeneratedTypeRegistrar` now respects lifetime overrides from `ITypeFilterer`
  - Added `GetEffectiveLifetime()` to `ITypeFilterer` interface
  - Source-gen registrar now checks filterer before using pre-computed lifetime

## [0.0.2-alpha.12] - 2026-01-23

### Fixed
- **Base class plugin discovery**: Source generator now discovers plugins that inherit from abstract base classes (not just interfaces)
  - `GetPluginInterfaces()` now walks the base class hierarchy in addition to interfaces
  - Fixes issue where `CacheConfiguration` and similar abstract record-based plugins were not discovered

## [0.0.2-alpha.11] - 2026-01-22

### Fixed  
- **`Except<T>()` not working with source-gen**: `GeneratedTypeRegistrar` now checks `IsTypeExcluded()` on the type filterer
  - Added `IsTypeExcluded()` method to `ITypeFilterer` interface
  - All registrars (Generated, Reflection, Scrutor) now respect type exclusions
  - Fixes duplicate route registration when using `Except<ICarterModule>()`

## [0.0.2-alpha.10] - 2026-01-22

### Added
- **`IPluginFactory` in plugin options**: All plugin option types now include access to `IPluginFactory`
  - `WebApplicationBuilderPluginOptions.PluginFactory`
  - `ServiceCollectionPluginOptions.PluginFactory`
  - Enables plugins to discover and instantiate other plugins at configuration time

## [0.0.2-alpha.9] - 2026-01-22

### Added
- **Duplicate Plugin Prevention**: Framework-level deduplication ensures each plugin type executes only once, even if discovered multiple times
  - Applies to all plugin types: `IWebApplicationPlugin`, `IWebApplicationBuilderPlugin`, `IServiceCollectionPlugin`, `IPostBuildServiceCollectionPlugin`, and `IHostApplicationBuilderPlugin`
  - Prevents duplicate route registration, middleware configuration, and service registration
  - Tests added to verify deduplication behavior

### Fixed
- **Carter routes registered twice**: Removed Carter-specific idempotency guards since framework now handles deduplication
  - `CarterWebApplicationBuilderPlugin` and `CarterWebApplicationPlugin` simplified back to single responsibility

## [0.0.2-alpha.8] - 2026-01-22

### Added
- **Carter end-to-end HTTP tests**: Added `CarterEndToEndTests` for full HTTP request/response testing of Carter modules

## [0.0.2-alpha.7] - 2026-01-22

### Added
- **Global Namespace Support**: Empty string `""` in `IncludeNamespacePrefixes` now explicitly includes types in the global namespace
  - Allows source generation to discover types without a `namespace` declaration
  - Example: `[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "MyCompany", "" })]`
- **NDLRCOR004 Analyzer**: Warns when injectable types in the global namespace may not be discovered
  - Detects types in global namespace that implement interfaces, have DI attributes, or have dependency constructors
  - Reports warning with guidance to either add a namespace or include `""` in `IncludeNamespacePrefixes`
  - Automatically suppressed when type has `[DoNotInject]` or `[DoNotAutoRegister]`

### Fixed
- **Needlr.Carter and Needlr.SignalR plugins not discovered**: Added `[GenerateTypeRegistry]` to Carter and SignalR packages
  - `CarterWebApplicationBuilderPlugin` and `CarterWebApplicationPlugin` now register automatically
  - `SignalRWebApplicationBuilderPlugin` now registers automatically
  - These packages now have their own TypeRegistry and module initializer for source generation compatibility

## [0.0.2-alpha.6] - 2026-01-21

### Added
- **Automatic Assembly Force-Loading**: Source generator now automatically discovers all referenced assemblies with `[GenerateTypeRegistry]` and emits `typeof()` calls to ensure they are loaded at startup
  - Solves the issue where transitive dependencies with plugins were not discovered because their assemblies never loaded
  - Generated `ForceLoadReferencedAssemblies()` method uses `typeof(AssemblyName.Generated.TypeRegistry).Assembly` for AOT-safe assembly loading
  - Assemblies are loaded in alphabetical order by default
- **`[NeedlrAssemblyOrder]` Attribute**: Optional attribute to control the order assemblies are loaded during startup
  - `First` property: Array of assembly names to load first (in order)
  - `Last` property: Array of assembly names to load last (in order)
  - All other assemblies load alphabetically between First and Last
  - Useful when plugins have dependencies on other plugins being registered first
- **NDLRCOR003 Analyzer**: Detects when `[DeferToContainer]` attribute is placed in generated code (which won't work due to source generator isolation)
  - Reports error with guidance to move the attribute to original hand-written source file

### Fixed
- **Plugin Discovery Bug**: Fixed issue where plugins in transitive dependencies were not discovered when using source generation
  - Root cause: Module initializers only run when their assembly is loaded by the CLR
  - If no code directly references types from an assembly, it never loads
  - Solution: Auto-discovery and force-loading ensures all Needlr-enabled assemblies load

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
