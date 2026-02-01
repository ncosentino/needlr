# Getting Started with Needlr

Needlr is an opinionated dependency injection library for .NET that simplifies service registration and web application setup through automatic discovery and a fluent API.

**Needlr is source-generation-first**: The recommended approach uses compile-time source generation for AOT compatibility. Both source-gen and reflection require explicit opt-in via `.UsingSourceGen()` or `.UsingReflection()`.

## Installation

Add the Needlr packages to your project. Choose your discovery strategy:

### Option 1: Source Generation (Recommended)

Best for AOT-compiled applications, trimmed deployments, and optimal startup performance:

```xml
<!-- Core dependency injection -->
<PackageReference Include="NexusLabs.Needlr.Injection" />
<PackageReference Include="NexusLabs.Needlr.Injection.SourceGen" />

<!-- Source generator (runs at compile time) -->
<PackageReference Include="NexusLabs.Needlr.Generators" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
<PackageReference Include="NexusLabs.Needlr.Generators.Attributes" />

<!-- For ASP.NET Core web applications -->
<PackageReference Include="NexusLabs.Needlr.AspNet" />
```

### Option 2: Reflection (Dynamic Scenarios)

For applications that need runtime type discovery or dynamic plugin loading:

```xml
<!-- Core dependency injection -->
<PackageReference Include="NexusLabs.Needlr.Injection" />
<PackageReference Include="NexusLabs.Needlr.Injection.Reflection" />

<!-- For ASP.NET Core web applications -->
<PackageReference Include="NexusLabs.Needlr.AspNet" />

<!-- Optional: Scrutor-based type registration -->
<PackageReference Include="NexusLabs.Needlr.Injection.Scrutor" />
```

### Option 3: Bundle (Auto-Fallback)

Includes both strategies with automatic fallback from source-gen to reflection:

```xml
<!-- Includes both source-gen and reflection with auto-detection -->
<PackageReference Include="NexusLabs.Needlr.Injection.Bundle" />

<!-- For ASP.NET Core web applications -->
<PackageReference Include="NexusLabs.Needlr.AspNet" />
```

## Your First Application

### Console Application (Source Generation)

```csharp
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;
using Microsoft.Extensions.DependencyInjection;

// Create a service provider with source-gen discovery
var serviceProvider = new Syringe()
    .UsingSourceGen()
    .BuildServiceProvider();

// Get your service (automatically registered at compile time)
var myService = serviceProvider.GetRequiredService<MyService>();
myService.DoWork();

// Your service class - automatically discovered by source generator
public class MyService
{
    public void DoWork()
    {
        Console.WriteLine("Work is being done!");
    }
}
```

### Console Application (Reflection)

```csharp
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using Microsoft.Extensions.DependencyInjection;

// Create a service provider with reflection-based discovery
var serviceProvider = new Syringe()
    .UsingReflection()
    .BuildServiceProvider();

// Get your service (automatically registered at runtime)
var myService = serviceProvider.GetRequiredService<MyService>();
myService.DoWork();
```

### Web Application (Source Generation)

```csharp
using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

// Create and run a web application
var webApplication = new Syringe()
    .UsingSourceGen()
    .ForWebApplication()
    .BuildWebApplication();

await webApplication.RunAsync();

// Add a minimal API endpoint using a plugin
internal sealed class HelloWorldPlugin : IWebApplicationPlugin
{
    public void Configure(WebApplicationPluginOptions options)
    {
        options.WebApplication.MapGet("/", () => "Hello, World!");
    }
}
```

### Web Application (Reflection)

```csharp
using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

// Create and run a web application with reflection
var webApplication = new Syringe()
    .UsingReflection()
    .ForWebApplication()
    .BuildWebApplication();

await webApplication.RunAsync();
```

## Key Concepts

### Automatic Registration

By default, Needlr automatically registers:

- All non-nested non-abstract classes (public and internal) in scanned assemblies
- Classes as both themselves and their interfaces
- With Singleton lifetime by default (use `[Transient]` or `[Scoped]` attributes to override)

By default, Needlr automatically dots NOT register:

- Anything marked with the `[DoNotAutoRegister]` attribute
- Record types
- Nested classes
- Interfaces or abstract classes
- Types where the only constructor are non-injectable types (i.e. value types)

NOTE: there are nuances to what is automatically registered to the dependency
container by default 

### The Syringe Class

The `Syringe` class is your entry point for configuring dependency injection. You must configure a discovery strategy:

```csharp
// Source generation (recommended for AOT)
var syringe = new Syringe()
    .UsingSourceGen()
    .UsingAssemblyProvider(builder => builder
        .MatchingAssemblies(x => x.Contains("MyApp"))
        .Build());

// Reflection with Scrutor (for dynamic scenarios)
var syringe = new Syringe()
    .UsingReflection()
    .UsingScrutorTypeRegistrar()
    .UsingAssemblyProvider(builder => builder
        .MatchingAssemblies(x => x.Contains("MyApp"))
        .Build());
```

### Preventing Auto-Registration

Use the `[DoNotAutoRegister]` attribute to exclude types:

```csharp
[DoNotAutoRegister]
public class ManuallyRegisteredService
{
    // This won't be automatically registered
}
```

### Controlling Interface Registration

By default, classes are registered as all their interfaces. Use `[RegisterAs<T>]` for explicit control:

```csharp
public interface IReader { }
public interface IWriter { }
public interface ILogger { }

// Only registered as IReader, not IWriter or ILogger
[RegisterAs<IReader>]
public class FileService : IReader, IWriter, ILogger { }
```

See [RegisterAs Documentation](register-as.md) for more details.

## Configuration Options

### Source Generation Configuration

```csharp
using NexusLabs.Needlr.Injection.SourceGen;

var serviceProvider = new Syringe()
    .UsingSourceGen()
    .BuildServiceProvider();
```

### Reflection Configuration

```csharp
using NexusLabs.Needlr.Injection.Reflection;

var serviceProvider = new Syringe()
    .UsingReflection()
    .BuildServiceProvider();
```

### Auto-Configuration (Bundle)

```csharp
using NexusLabs.Needlr.Injection.Bundle;

var serviceProvider = new Syringe()
    .UsingAutoConfiguration()  // Tries source-gen first, falls back to reflection
    .BuildServiceProvider();
```

### Custom Assembly Scanning

```csharp
var serviceProvider = new Syringe()
    .UsingSourceGen()  // or .UsingReflection()
    .UsingAssemblyProvider(builder => builder
        .MatchingAssemblies(x => 
            x.Contains("MyCompany") || 
            x.Contains("MyApp"))
        .UseLibTestEntryOrdering()  // Sort assemblies appropriately
        .Build())
    .BuildServiceProvider();
```

## Web Application Options

### Source Generation Web Application

```csharp
using NexusLabs.Needlr.Injection.SourceGen;

var webApplication = new Syringe()
    .UsingSourceGen()
    .ForWebApplication()
    .BuildWebApplication();
```

### Reflection Web Application

```csharp
using NexusLabs.Needlr.Injection.Reflection;

var webApplication = new Syringe()
    .UsingReflection()
    .ForWebApplication()
    .BuildWebApplication();
```

### With Custom Options

```csharp
var webApplication = new Syringe()
    .UsingSourceGen()  // or .UsingReflection()
    .ForWebApplication()
    .UsingOptions(() => CreateWebApplicationOptions
        .Default
        .UsingStartupConsoleLogger()
        .UsingApplicationName("MyApp"))
    .BuildWebApplication();
```

### With Configuration Callback

The `UsingConfigurationCallback` method allows you to customize the WebApplicationBuilder before the application is built:

```csharp
var webApplication = new Syringe()
    .UsingSourceGen()  // or .UsingReflection()
    .ForWebApplication()
    .UsingConfigurationCallback((builder, options) =>
    {
        // Customize configuration sources
        builder.Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.local.json", optional: true)
            .AddEnvironmentVariables("MYAPP_");
        
        // Add services before plugin registration
        builder.Services.AddSingleton<ICustomService, CustomService>();
        
        // Configure logging
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
    })
    .BuildWebApplication();
```

### With Web Application Factory

```csharp
var webApplication = new Syringe()
    .UsingSourceGen()  // or .UsingReflection()
    .ForWebApplication()
    .UsingWebApplicationFactory<CustomWebApplicationFactory>()
    .BuildWebApplication();
```

## Choosing Source Generation vs Reflection

| Feature | Source Generation | Reflection |
|---------|-------------------|------------|
| **AOT Compatible** | ✅ Yes | ❌ No |
| **Trimming Safe** | ✅ Yes | ❌ No |
| **Startup Performance** | ✅ Faster | ⚠️ Slower |
| **Dynamic Plugin Loading** | ❌ No | ✅ Yes |
| **Runtime Assembly Scanning** | ❌ No | ✅ Yes |
| **Scrutor Support** | ❌ No | ✅ Yes |

**Use Source Generation when:**

- Building AOT-compiled applications
- Targeting trimmed/self-contained deployments
- You want faster startup times
- All plugins are known at compile time

**Use Reflection when:**

- Loading plugins dynamically at runtime
- Scanning assemblies not known at compile time
- Using Scrutor for advanced registration patterns

## Next Steps

- Learn about [Core Concepts](core-concepts.md) for deeper understanding
- Explore [Plugin Development](plugin-development.md) to extend functionality
- Discover [Factory Delegates](factories.md) for types with runtime parameters
- Read about [Interceptors](interceptors.md) for cross-cutting concerns
- See [Advanced Usage](advanced-usage.md) for complex scenarios