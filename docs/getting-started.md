# Getting Started with Needlr

Needlr is an opinionated dependency injection library for .NET that simplifies service registration and web application setup through automatic discovery and a fluent API.

## Installation

Add the Needlr packages to your project:

```xml
<!-- Core dependency injection -->
<PackageReference Include="NexusLabs.Needlr.Injection" />

<!-- For ASP.NET Core web applications -->
<PackageReference Include="NexusLabs.Needlr.AspNet" />

<!-- Optional: Scrutor-based type registration -->
<PackageReference Include="NexusLabs.Needlr.Injection.Scrutor" />

<!-- Optional: Carter framework integration -->
<PackageReference Include="NexusLabs.Needlr.Carter" />

<!-- Optional: SignalR integration -->
<PackageReference Include="NexusLabs.Needlr.SignalR" />
```

## Your First Application

### Console Application

The simplest way to use Needlr is in a console application:

```csharp
using NexusLabs.Needlr.Injection;
using Microsoft.Extensions.DependencyInjection;

// Create a service provider with automatic registration
var serviceProvider = new Syringe().BuildServiceProvider();

// Get your service (automatically registered)
var myService = serviceProvider.GetRequiredService<MyService>();
myService.DoWork();

// Your service class - automatically registered!
public class MyService
{
    public void DoWork()
    {
        Console.WriteLine("Work is being done!");
    }
}
```

### Web Application

Creating a web application is just as simple:

```csharp
using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;

// Create and run a web application
var webApplication = new Syringe().BuildWebApplication();
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

## Key Concepts

### Automatic Registration

By default, Needlr automatically registers:
- All non-nested non-abstract classes (public and internal) in scanned assemblies
- Classes as both themselves and their interfaces
- With appropriate lifetimes (Transient by default, Singleton based on filtering)

By default, Needlr automatically dots NOT register:
- Anything marked with the `[DoNotAutoRegister]` attribute
- Record types
- Nested classes
- Interfaces or abstract classes
- Types where the only constructor are non-injectable types (i.e. value types)

NOTE: there are nuances to what is automatically registered to the dependency
container by default 

### The Syringe Class

The `Syringe` class is your entry point for configuring dependency injection:

```csharp
var syringe = new Syringe()
    .UsingScrutorTypeRegistrar()         // Use Scrutor for registration
    .UsingDefaultTypeFilterer()           // Apply default filtering rules
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

## Configuration Options

### Basic Configuration

```csharp
var serviceProvider = new Syringe()
    .BuildServiceProvider();  // Uses all defaults
```

### With Configuration

```csharp
using NexusLabs.Needlr.Extensions.Configuration;

var serviceProvider = new Syringe()
    .UsingConfiguration()  // Automatically adds empty IConfiguration
    .BuildServiceProvider();
```

### Custom Assembly Scanning

```csharp
var serviceProvider = new Syringe()
    .UsingAssemblyProvider(builder => builder
        .MatchingAssemblies(x => 
            x.Contains("MyCompany") || 
            x.Contains("MyApp"))
        .UseLibTestEntrySorting()  // Sort assemblies appropriately
        .Build())
    .BuildServiceProvider();
```

## Web Application Options

### Default Web Application

```csharp
var webApplication = new Syringe()
    .BuildWebApplication();  // Uses all defaults
```

### With Custom Options

```csharp
var webApplication = new Syringe()
    .ForWebApplication()
    .UsingOptions(() => CreateWebApplicationOptions
        .Default
        .UsingStartupConsoleLogger()
        .UsingApplicationName("MyApp"))
    .BuildWebApplication();
```

### With Web Application Factory

```csharp
var webApplication = new Syringe()
    .ForWebApplication()
    .UsingWebApplicationFactory<CustomWebApplicationFactory>()
    .BuildWebApplication();
```

## Next Steps

- Learn about [Core Concepts](core-concepts.md) for deeper understanding
- Explore [Plugin Development](plugin-development.md) to extend functionality
- See [Advanced Usage](advanced-usage.md) for complex scenarios
- Check the [API Reference](api-reference.md) for detailed documentation