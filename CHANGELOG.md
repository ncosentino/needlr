# Changelog

All notable changes to Needlr will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.0.2-alpha.19] - 2026-03-01

### Added

- **Microsoft Agent Framework (MAF) Integration**: First-class support for building multi-agent orchestrations with `NexusLabs.Needlr.AgentFramework`
  - `[NeedlrAiAgent]` attribute for named agent declarations with source-generated bootstrap
  - `IAgentFactory.CreateAgent<T>()` / `CreateAgent(string name)` for DI-wired agent construction
  - `IWorkflowFactory` with `CreateHandoffWorkflow<T>()`, `CreateSequentialWorkflow(name)`, `CreateGroupChatWorkflow(name)`
  - `UsingAgentFramework()` / `UsingChatClient()` Syringe extensions for fluent MAF wiring

- **MAF Topology Declarations**: Compile-time workflow structure via attributes
  - `[AgentHandoffsTo(typeof(T))]` — declares agent handoff edges
  - `[AgentSequenceMember(pipeline, order)]` — declares sequential pipeline membership
  - `[AgentGroupChatMember(groupName)]` — declares group chat membership
  - `[WorkflowRunTerminationCondition]` — marks methods as early-exit predicates for pipeline workflows
  - `[AgentFunctionGroup]` — scopes AI functions to a named group for multi-agent isolation

- **MAF Workflow Execution Helpers** (`NexusLabs.Needlr.AgentFramework.Workflows`)
  - `StreamingRunWorkflowExtensions.RunAsync()` — executes a workflow and collects all agent responses as `IReadOnlyDictionary<string, string>`
  - Source-generated `WorkflowFactoryExtensions` with typed `Create*Workflow()` and `Run*WorkflowAsync()` methods per declared workflow

- **MAF Source Generator** (`NexusLabs.Needlr.AgentFramework.Generators`)
  - `AgentFrameworkBootstrapGenerator`: emits `AgentFrameworkGeneratedBootstrap` with handoff, sequential, and group-chat topology tables registered via `[ModuleInitializer]`
  - `AgentFrameworkFunctionRegistryGenerator`: emits `GeneratedAIFunctionProvider` — AOT-safe, source-generated `IAIFunctionProvider` using `IServiceProvider.GetRequiredService<T>()` for instance creation
  - `WorkflowExtensionsGenerator`: emits typed `WorkflowFactoryExtensions` per workflow with full XML doc comments per agent
  - `AgentTopologyExportGenerator`: emits `AgentTopologyGraph.md` when `NeedlrDiagnostics=true`

- **MAF Analyzers** (12 new diagnostics)
  - `NDLRMAF001`: Missing `[AgentHandoffsTo]` target registration
  - `NDLRMAF002`: Circular handoff chain detected
  - `NDLRMAF003`: Unreachable agent (no inbound handoff edges)
  - `NDLRMAF004`: `[AgentSequenceMember]` order gap or duplicate
  - `NDLRMAF005`: `[AgentFunctionGroup]` declared on non-injectable type
  - `NDLRMAF006`: Agent function group not registered in any agent
  - `NDLRMAF007`: `[NeedlrAiAgent]` missing required `IChatClient` dependency
  - `NDLRMAF008`: Duplicate agent name
  - `NDLRMAF009`: `[WorkflowRunTerminationCondition]` on wrong return type
  - `NDLRMAF010`: `[WorkflowRunTerminationCondition]` on non-sequential-member class
  - `NDLRMAF011`: Multiple termination conditions on same sequential pipeline
  - `NDLRMAF012`–`NDLRMAF014`: Function provider, group membership, and agent registration cross-checks
  - Code fix providers for `NDLRMAF001` and `NDLRMAF003` (add missing attribute scaffolding)

- **AOT-Safe MAF** (`AotAgentFrameworkApp` example)
  - NativeAOT proof with `IlcDisableReflection=true`, full `IL2026`/`IL3050` `WarningsAsErrors`, `TrimMode=full`
  - `[DoNotAutoRegister]` on reflection-based scanners prevents ILC tracing their `[RequiresDynamicCode]` constructors
  - `[UnconditionalSuppressMessage]` on `AgentFactory.BuildFunctionsForType` and `WorkflowFactory.Resolve*` fallback callers — reflection branches are unreachable when the source-gen bootstrap is registered
  - Demonstrates handoff workflow, sequential pipeline, and termination condition under NativeAOT
  - CI job `aot-agent-app` publishes for `linux-x64` in parallel with existing AOT jobs

- **Plugin assembly example**: `[NeedlrAiAgent]` source gen across assembly boundaries via separate `.Agents` library pattern

- **SemanticKernel / SignalR XML documentation enrichment**: All public types have comprehensive XML doc comments

### Changed

- **`IAIFunctionProvider.TryGetFunctions` signature**: Parameter changed from `object? instance` to `IServiceProvider serviceProvider`
  - Generated provider now calls `serviceProvider.GetRequiredService<T>()` (AOT-safe) instead of casting the passed instance
  - `AgentFactory.BuildFunctionsForType` checks the generated provider *before* calling `ActivatorUtilities.CreateInstance`

### Fixed

- MAF diagnostics now output to entry-point exe bin directory (not the `.Agents` library bin)
- `NeedlrExportGraph` defaults to `false` to match props file expectation

### Documentation

- AI integrations page covering the two-layer discovery model
- MAF feature page with topology declarations, workflow execution, and source generation guide
- NDLRMAF001–014 analyzer reference pages
- `llms.txt` for AI tool accessibility
- Open Graph / Twitter Card meta tags, BreadcrumbList and SoftwareSourceCode structured data
- Per-page descriptions and cross-references to published articles

_(366 files changed, +39873/-3108 lines)_

## [0.0.2-alpha.18] - 2026-01-30

### Added
- **AOT-Compatible Options - Full Parity**: Complete feature parity with non-AOT configuration binding
  - Primitive types, nullable primitives, and enums using `Enum.TryParse<T>()`
  - Init-only properties via factory pattern with object initializers
  - Positional records via factory pattern with constructor invocation
  - Nested objects with recursive property binding
  - Arrays, Lists, and Dictionaries (`Dictionary<string, T>`)
  - Circular reference protection with visited type tracking
  - Named options with `[Options("Section", Name = "Named")]` pattern

- **DataAnnotations Source Generation**: Validation attributes source-generated as `IValidateOptions<T>` implementations
  - Supported: `[Required]`, `[Range]`, `[StringLength]`, `[MinLength]`, `[MaxLength]`, `[RegularExpression]`, `[EmailAddress]`, `[Phone]`, `[Url]`
  - Non-AOT keeps `.ValidateDataAnnotations()` fallback for unsupported attributes

- **NDLRGEN030: Unsupported DataAnnotation Warning**: Analyzer warns when [Options] classes use DataAnnotation attributes that cannot be source-generated
  - Warns about: `[CustomValidation]`, `[CreditCard]`, `[FileExtensions]`, and custom `ValidationAttribute` subclasses
  - Guides users to use custom `Validate()` method instead

- **[RegisterAs<T>] Attribute**: Explicit interface registration for DI
  - Apply `[RegisterAs<IService>]` to control which interface a class is registered as
  - NDLRCOR015 analyzer validates the type argument is implemented by the class

- **FluentValidation Integration**: New `NexusLabs.Needlr.FluentValidation` package with source generator extension
  - Auto-discovers and registers FluentValidation validators for `[Options]` classes
  - Extension framework for custom validator integrations

- **Enhanced Diagnostic Output**
  - `OptionsSummary.md` shows all discovered options with their configuration
  - Consolidated Factory Services section in diagnostics

- **Enhanced Dependency Graph Visualization**: The `DependencyGraph.md` diagnostic output now includes additional sections
  - Decorator Chains, Keyed Service Clusters, Plugin Assembly Boundaries
  - Factory Services, Interface Mapping, Complexity Metrics

- **Open Generic Decorators**: New `[OpenDecoratorFor(typeof(IInterface<>))]` attribute for source-gen only
  - Automatically decorates all closed implementations of an open generic interface
  - Compile-time validation with NDLRGEN006, NDLRGEN007, NDLRGEN008 analyzers

- **Positional Record Support for [Options]**: Positional records now work with `[Options]` when declared as `partial`
  - The generator emits a parameterless constructor that chains to the primary constructor
  - NDLRGEN021 warning emitted for non-partial positional records

### Changed
- **BREAKING: `[GenerateFactory]` namespace change**: Moved from `NexusLabs.Needlr` to `NexusLabs.Needlr.Generators`
  - Update imports: `using NexusLabs.Needlr;` → `using NexusLabs.Needlr.Generators;`

- **BREAKING: Factory analyzer diagnostic IDs renamed**: Factory-related analyzers moved to generators project with new IDs
  - `NDLRCOR012` → `NDLRGEN003`, `NDLRCOR013` → `NDLRGEN004`, `NDLRCOR014` → `NDLRGEN005`

- **BREAKING: ConfiguredSyringe API**: `Syringe` no longer has `BuildServiceProvider()`. You must call a strategy method first:
  - `new Syringe().UsingReflection()` / `.UsingSourceGen()` / `.UsingAutoConfiguration()` → returns `ConfiguredSyringe`
  - This prevents runtime crashes from misconfigured syringes by making incorrect usage a compile-time error

- **Refactored TypeRegistryGenerator**: Decomposed into smaller focused generators
  - `OptionsCodeGenerator`, `FactoryCodeGenerator`, `InterceptorCodeGenerator`
  - `DiagnosticsGenerator`, `SignalRCodeGenerator`, `SemanticKernelCodeGenerator`

- **SignalR and SemanticKernel code generation moved to dedicated packages**

### Fixed
- `GetShortTypeName` correctly handles generic types
- Attribute detection in DataAnnotations uses namespace/typeName matching (more reliable than ToDisplayString)

_(176 files changed, +23041/-2816 lines)_

## [0.0.2-alpha.16] - 2026-01-24

### Added
- **Source-gen assembly ordering**: `OrderAssemblies(order => order.By().ThenBy())` now works for source-gen path
  - Same fluent API as reflection path for full parity
  - `SyringeSourceGenExtensions.OrderAssemblies()` extension method
  - Assemblies sorted by tiered predicates using `AssemblyOrderBuilder`
- **6 parity tests**: Verifying reflection and source-gen assembly ordering produce identical behavior

### Fixed
- **TypeRegistry generator not detecting `[GenerateTypeRegistry]` attribute**: `ForAttributeWithMetadataName` doesn't work for assembly-level attributes. Changed to use `compilation.Assembly.GetAttributes()` directly, which correctly detects `[assembly: GenerateTypeRegistry(...)]` attributes.
- **`AssemblyOrderBuilderTests`**: Moved from incorrectly named "parity" tests to unit test location

## [0.0.2-alpha.15] - 2026-01-23

### Fixed
- **Plugin discovery**: Records with parameterless constructors ARE now discoverable via `IPluginFactory.CreatePluginsFromAssemblies<T>()` (fixes CacheConfiguration pattern)
- **Auto-registration**: Records are still correctly excluded from `IsInjectableType` (not auto-registered into DI container)

## [0.0.2-alpha.14] - 2026-01-23

### Added
- **`[DecoratorFor<TService>]` attribute**: Automatic decorator wiring without manual plugin registration
  - Apply to a class to automatically configure it as a decorator for the specified service
  - `Order` property controls decoration sequence (lower = closer to original service)
  - Works with both source generation and reflection
  - Multiple decorators per service supported with ordering
  - A class can decorate multiple services using multiple attributes
- **Non-generic `AddDecorator(Type, Type)` overload**: For runtime decorator application

### Fixed
- **Plugin discovery**: Records with parameterless constructors ARE now discoverable via `IPluginFactory.CreatePluginsFromAssemblies<T>()` (fixes CacheConfiguration pattern)
- **Auto-registration**: Records are still correctly excluded from `IsInjectableType` (not auto-registered into DI container)
- **Source generation**: Types with `required` members are excluded from automatic registration (cannot be instantiated by DI container without setter access)
- **Source generation**: Types with `[SetsRequiredMembers]` constructor are still included (constructor satisfies all required members)

### Changed
- **CI/CD**: Added GitHub-native code coverage reporting with badges and GitHub Pages reports

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
