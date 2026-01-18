# Core Concepts

This guide explains the fundamental concepts and architecture of Needlr.

## Architecture Overview

Needlr is built around several key components that work together to provide automatic dependency injection. **Needlr is source-generation-first**: compile-time discovery is the default, with reflection available as an opt-in alternative.

```
                         ┌─────────────────────────┐
                         │   Discovery Strategy    │
                         │ (.UsingSourceGen() or   │
                         │  .UsingReflection())    │
                         └───────────┬─────────────┘
                                     │
┌─────────────┐     ┌────────────────▼───────────────┐     ┌─────────────────┐
│   Syringe   │────▶│     Assembly Provider          │────▶│ Type Discovery  │
└─────────────┘     └────────────────────────────────┘     └─────────────────┘
       │                                                           │
       ▼                                                           ▼
┌─────────────┐     ┌──────────────────┐     ┌─────────────────────────────────┐
│Type Filterer│────▶│ Type Registrar   │────▶│      Service Collection         │
└─────────────┘     └──────────────────┘     └─────────────────────────────────┘
       │                                                           │
       ▼                                                           ▼
┌─────────────┐                                        ┌─────────────────┐
│   Plugins   │───────────────────────────────────────▶│Service Provider │
└─────────────┘                                        └─────────────────┘
```

## Source Generation vs Reflection

Needlr supports two discovery strategies:

### Source Generation (Recommended)

Uses compile-time code generation to discover and register types:

```csharp
using NexusLabs.Needlr.Injection.SourceGen;

var syringe = new Syringe()
    .UsingSourceGen();  // Configures all source-gen components
```

**Benefits:**
- ✅ AOT compatible
- ✅ Trimming safe
- ✅ Faster startup (no runtime scanning)
- ✅ Compile-time error detection

**Requirements:**
- Add `NexusLabs.Needlr.Generators` as an analyzer
- Add `NexusLabs.Needlr.Generators.Attributes`
- All types must be known at compile time

### Reflection (Dynamic Scenarios)

Uses runtime reflection to discover and register types:

```csharp
using NexusLabs.Needlr.Injection.Reflection;

var syringe = new Syringe()
    .UsingReflection();  // Configures all reflection components
```

**Benefits:**
- ✅ Dynamic plugin loading
- ✅ Runtime assembly scanning
- ✅ Scrutor integration support

**Drawbacks:**
- ❌ Not AOT compatible
- ❌ Not trimming safe
- ❌ Slower startup

### Bundle (Auto-Fallback)

Automatically chooses source-gen if available, falls back to reflection:

```csharp
using NexusLabs.Needlr.Injection.Bundle;

var syringe = new Syringe()
    .UsingAutoConfiguration();  // Source-gen preferred, reflection fallback
```

## The Syringe Class

The `Syringe` class is the central configuration point for Needlr. It's an immutable record that creates new instances when configured, following a fluent API pattern.

### Key Properties

- **TypeRegistrar**: Determines how types are registered
- **TypeFilterer**: Controls which types get automatically registered
- **AssemblyProvider**: Specifies which assemblies to scan
- **PluginFactory**: Discovers and creates plugin instances
- **ServiceProviderBuilderFactory**: Creates the appropriate service provider builder

### Immutability

Each configuration method returns a new `Syringe` instance:

```csharp
var syringe1 = new Syringe();
var syringe2 = syringe1.UsingSourceGen();
// syringe1 != syringe2 (different instances)
```

This pattern helps support the fluent-builder syntax.

### Required Configuration

Unlike earlier versions, you must explicitly configure a discovery strategy:

```csharp
// ❌ This will throw an InvalidOperationException
var provider = new Syringe().BuildServiceProvider();

// ✅ Correct - specify strategy
var provider = new Syringe()
    .UsingSourceGen()
    .BuildServiceProvider();
```

## Assembly Scanning

### Assembly Provider

The `IAssemblyProvider` determines which assemblies are scanned for types:

```csharp
var provider = new AssembyProviderBuilder()
    .MatchingAssemblies(x => x.Contains("MyApp"))
    .UseLibTestEntrySorting()
    .Build();
```

### Assembly Loaders

Different loaders provide different scanning strategies:

- **DefaultAssemblyLoader**: Scans current domain assemblies
- **AllAssembliesLoader**: Scans all loaded assemblies
- **FileMatchAssemblyLoader**: Scans assemblies matching file patterns

NOTE: you can provide your own custom loaders.

### Assembly Sorters

Control the order in which assemblies are processed:

- **DefaultAssemblySorter**: No specific ordering
- **AlphabeticalAssemblySorter**: Alphabetical by name
- **LibTestEntryAssemblySorter**: Libraries first, then tests, then entry assembly

NOTE: you can provider your own custom sorters.

## Type Registration

### Type Registrars

Type registrars determine how discovered types are registered:

#### GeneratedTypeRegistrar (Source Generation)

Used with source generation strategy:
- Types discovered at compile time
- AOT and trimming compatible
- No runtime reflection

#### ReflectionTypeRegistrar (Reflection)

Used with reflection strategy:
- Types discovered at runtime
- Supports dynamic scenarios

#### ScrutorTypeRegistrar (Reflection only)

Uses Scrutor library for advanced registration scenarios (requires reflection):
- Assembly scanning with filters
- Decorator pattern support
- Advanced lifetime management

NOTE: you can provide your own custom type registrars.

### Type Filterers

Control which types are eligible for automatic registration:

#### GeneratedTypeFilterer (Source Generation)

For source-gen, filtering is done at compile time based on:
- Types with `[DoNotAutoRegister]` attribute excluded
- Types with `[DoNotInject]` attribute excluded
- Abstract classes and interfaces excluded

#### ReflectionTypeFilterer (Reflection)

For reflection, filters at runtime based on:
- Excludes types with `[DoNotAutoRegister]` attribute
- Excludes types with `[DoNotInject]` attribute
- Excludes abstract classes and interfaces
- Excludes compiler-generated types

#### Custom Type Filterers

Implement `ITypeFilterer` for custom filtering logic:

```csharp
public class MyTypeFilterer : ITypeFilterer
{
    public IEnumerable<Type> Filter(IEnumerable<Type> types)
    {
        return types.Where(t => 
            !t.Name.EndsWith("Test") && 
            t.Namespace?.StartsWith("MyCompany") == true);
    }
}
```

Note: you can provide your own custom type filterers.

## Service Collection Population

The `IServiceCollectionPopulator` orchestrates the registration process:

1. Discovers types from assemblies
2. Applies type filtering
3. Registers types using the configured registrar
4. Executes plugins

## Plugin System

### Plugin Types

#### IServiceCollectionPlugin

Configures services during the initial registration phase:

```csharp
public class MyServicePlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        options.Services.AddSingleton<IMyService, MyService>();
        options.Logger.LogInformation("Configured MyService");
    }
}
```

#### IPostBuildServiceCollectionPlugin

Executes after the main service collection is built:

```csharp
public class MyPostBuildPlugin : IPostBuildServiceCollectionPlugin
{
    public void Configure(PostBuildServiceCollectionPluginOptions options)
    {
        // Access built services
        var myService = options.Services.GetService<IMyService>();
        // Additional configuration
    }
}
```

#### IWebApplicationBuilderPlugin

Configures the WebApplicationBuilder:

```csharp
public class MyBuilderPlugin : IWebApplicationBuilderPlugin
{
    public void Configure(WebApplicationBuilderPluginOptions options)
    {
        options.Builder.Services.AddCors();
        options.Builder.Configuration.AddJsonFile("custom.json");
    }
}
```

#### IWebApplicationPlugin

Configures the WebApplication after building:

```csharp
public class MyAppPlugin : IWebApplicationPlugin
{
    public void Configure(WebApplicationPluginOptions options)
    {
        options.WebApplication.MapGet("/health", () => "Healthy");
        options.WebApplication.UseCors();
    }
}
```

### Plugin Discovery

Plugins are automatically discovered and registered:
- Scanned from assemblies like other types
- Instantiated and executed in order

NOTE: The `[DoNotAutoRegister]` attribute is used for dynamically discovering 
and registering types, but it is not applicable for plugins. In fact, by
default, all built-in plugins are marked with this attribute so that they
will not be resolvable on the dependency container itself. This attribute is
not to control the discoverability of plugins.

## Lifetime Management

### Default Lifetimes

- **Singleton**: Created once and reused (default for most types in Needlr)
- **Transient**: Created each time requested 
- **Scoped**: Created once per request/scope

### Lifetime Detection

Needlr automatically determines appropriate lifetimes based on:
- Type characteristics (stateless vs stateful)
- Interface implementations
- Decorator patterns

## Decorator Pattern

### Manual Decoration

```csharp
services.AddSingleton<IService, ServiceImpl>();
services.AddSingleton<IService>(sp => 
    new ServiceDecorator(sp.GetRequiredService<ServiceImpl>()));
```

### Using AddDecorator Extension

```csharp
new Syringe()
    .UsingSourceGen()  // or .UsingReflection()
    .AddDecorator<IService, ServiceDecorator>()
    .BuildServiceProvider();
```

### With Scrutor (Reflection only)

```csharp
services.Decorate<IService, ServiceDecorator>();
```

## Configuration Integration

### Automatic IConfiguration

Needlr automatically registers `IConfiguration`:

```csharp
var provider = new Syringe()
    .UsingSourceGen()  // or .UsingReflection()
    .UsingConfiguration()  // Adds IConfiguration support
    .BuildServiceProvider();
```

## Web Application Integration

### WebApplicationSyringe

Extends Syringe for web applications:

```csharp
var webApp = new Syringe()
    .UsingSourceGen()  // or .UsingReflection()
    .ForWebApplication()  // Returns WebApplicationSyringe
    .UsingOptions(() => CreateWebApplicationOptions.Default)
    .BuildWebApplication();
```

## Best Practices

### 1. Choose the Right Discovery Strategy

- Use **Source Generation** for AOT, trimmed, or performance-critical apps
- Use **Reflection** only when dynamic discovery is required

### 2. Use Appropriate Attributes

- `[DoNotAutoRegister]`: Exclude from automatic registration
- `[DoNotInject]`: Prevent dependency injection

### 3. Leverage Assembly Filtering

Filter assemblies to improve performance:

```csharp
.UsingAssemblyProvider(builder => builder
    .MatchingAssemblies(x => x.StartsWith("MyCompany"))
    .Build())
```

### 4. Order Matters

Use assembly sorting for predictable registration order:

```csharp
.UseLibTestEntrySorting()  // Libraries → Tests → Entry
```

### 5. Plugin Organization

- Group related configuration in plugins
- Use appropriate plugin types for timing
- Keep plugins focused and single-purpose

### 6. Immutability

Remember that Syringe is immutable:

```csharp
var configured = new Syringe()
    .UsingSourceGen()  // or .UsingReflection()
    .UsingConfiguration();
// Chain all configuration before building
```