![Needlr](needlr.webp)

# Needlr

Needlr is an opinionated fluent dependency injection library for .NET that provides automatic service registration and web application setup through a simple, discoverable API. It's designed to minimize boilerplate code by defaulting to registering types from scanned assemblies automatically.

## Features

- **Automatic Service Discovery**: Automatically registers services from assemblies using conventions
- **Fluent API**: Chain-able configuration methods for clean, readable setup
- **ASP.NET Core Integration**: Seamless web application creation and configuration
- **Plugin System**: Extensible architecture for modular applications
- **Multiple Type Registrars**: Built-in support for default registration and Scrutor-based scanning
- **Flexible Filtering**: Control which types get registered automatically

## Quick Start

### Basic Web Application

```csharp
using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;

var webApplication = new Syringe().BuildWebApplication();
await webApplication.RunAsync();
```

### Advanced Configuration with Scrutor

```csharp
using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Scrutor;

var webApplication = new Syringe()
    .UsingScrutorTypeRegistrar()
    .UsingAssemblyProvider(builder => builder
        .MatchingAssemblies(x => x.Contains("MyApp"))
        .Build())
    .ForWebApplication()
    .BuildWebApplication();

await webApplication.RunAsync();
```

## Installation

**// TODO: COMING SOON** Add the core package and any additional packages you need:

```xml
<!-- Core dependency injection -->
<PackageReference Include="NexusLabs.Needlr.Injection" />

<!-- ASP.NET Core web applications -->
<PackageReference Include="NexusLabs.Needlr.AspNet" />

<!-- Scrutor-based type registration -->
<PackageReference Include="NexusLabs.Needlr.Injection.Scrutor" />

<!-- Carter framework integration -->
<PackageReference Include="NexusLabs.Needlr.Carter" />

<!-- SignalR integration -->
<PackageReference Include="NexusLabs.Needlr.SignalR" />
```

## Core Concepts

### Syringe

The `Syringe` class is the main entry point for configuring dependency injection in Needlr. It provides a fluent API for setting up:

- **Type Registrars**: How services are registered (default or Scrutor-based)
- **Type Filterers**: Which types should be registered automatically
- **Assembly Providers**: Which assemblies to scan for services

```csharp
var syringe = new Syringe()
    .UsingScrutorTypeRegistrar()
    .UsingDefaultTypeFilterer()
    .UsingAssemblyProvider(builder => builder
        .MatchingAssemblies(x => x.Contains("MyApp"))
        .Build());
```

### WebApplicationSyringe

For web applications, use `ForWebApplication()` to transition to web-specific configuration:

```csharp
var webAppSyringe = new Syringe()
    .UsingScrutorTypeRegistrar()
    .ForWebApplication()
    .UsingOptions(() => CreateWebApplicationOptions.Default)
    .BuildWebApplication();
```

## Service Registration

### Automatic Registration

Services are automatically registered based on conventions. By default, Needlr will:

- Register classes as both themselves and their interfaces
- Use appropriate lifetimes (Transient/Singleton based on type filtering)
- Skip types marked with `[DoNotAutoRegister]`

### Preventing Auto-Registration

Use the `[DoNotAutoRegister]` attribute to exclude types from automatic registration. This is typically done when you need manual control over service registration:

```csharp
[DoNotAutoRegister]
public class ManuallyRegisteredService
{
    // This won't be automatically registered
}
```

### Custom Services

By default, a custom class you create (public or internal) will get picked up automatically and be available on the dependency container:

```csharp
internal class WeatherProvider
{
    private readonly IConfiguration _config;
    
    public WeatherProvider(IConfiguration config)
    {
        _config = config;
    }
    
    public WeatherData GetWeather()
    {
        // Implementation
    }
}
```

The above class would be available for use in minimal APIs and can be injected into other types resolved from the dependency container.

## Manual Service Registration

While Needlr automatically registers services by convention, you may need to manually register services for more complex scenarios like decorator patterns, conditional registration, or when you need precise control over service lifetimes and configurations.

### Preventing Auto-Registration

Use the `[DoNotAutoRegister]` attribute to exclude types from automatic registration:

```csharp
using NexusLabs.Needlr;

[DoNotAutoRegister]
public sealed class MyService : IMyService
{
    public void DoSomething()
    {
        Console.WriteLine("Hello, from Dev Leader!");
    }
}
```

### Manual Registration with IServiceCollectionPlugin

Create a plugin that implements `IServiceCollectionPlugin` to manually configure services:

```csharp
using Microsoft.Extensions.DependencyInjection;
using NexusLabs.Needlr;

internal sealed class MyPlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        // Register services manually
        options.Services.AddSingleton<MyService>();
        options.Services.AddSingleton<IMyService, MyDecorator>(s => 
            new MyDecorator(s.GetRequiredService<MyService>()));
    }
}
```

### Decorator Pattern Example

Here's a complete example showing manual registration with a decorator pattern:

```csharp
using Microsoft.Extensions.DependencyInjection;
using NexusLabs.Needlr;
using NexusLabs.Needlr.Injection;

// Interface
public interface IMyService
{
    void DoSomething();
}

// Base service implementation
[DoNotAutoRegister]
public sealed class MyService : IMyService
{
    public void DoSomething()
    {
        Console.WriteLine("Hello, from Dev Leader!");
    }
}

// Decorator that adds additional behavior
[DoNotAutoRegister]
public sealed class MyDecorator(IMyService wrapped) : IMyService
{
    public void DoSomething()
    {
        Console.WriteLine("---BEFORE---");
        wrapped.DoSomething();
        Console.WriteLine("---AFTER---");
    }
}

// Plugin for manual registration
internal sealed class MyPlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        options.Services.AddSingleton<MyService>();
        options.Services.AddSingleton<IMyService, MyDecorator>(s => 
            new MyDecorator(s.GetRequiredService<MyService>()));
    }
}

// Usage
var serviceProvider = new Syringe().BuildServiceProvider();
serviceProvider.GetRequiredService<IMyService>().DoSomething();
// Output:
// ---BEFORE---
// Hello, from Dev Leader!
// ---AFTER---
```

The `IServiceCollectionPlugin` is automatically discovered and registered by Needlr, so you don't need to manually register the plugin itself.

## Plugin System

Needlr supports a plugin architecture for modular applications:

### Web Application Plugins

```csharp
internal sealed class WeatherPlugin : IWebApplicationPlugin
{
    public void Configure(WebApplicationPluginOptions options)
    {
        options.WebApplication.MapGet("/weather", (WeatherProvider weatherProvider) =>
        {
            return Results.Ok(weatherProvider.GetWeather());
        });
    }
}
```

### Web Application Builder Plugins

```csharp
public sealed class CarterWebApplicationBuilderPlugin : IWebApplicationBuilderPlugin
{
    public void Configure(WebApplicationBuilderPluginOptions options)
    {
        options.Logger.LogInformation("Configuring Carter services...");
        options.Builder.Services.AddCarter();
    }
}
```

## Examples

### Minimal Web API

The following example has a custom type automatically registered and a minimal API that will consume it:

```csharp
using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;

var webApplication = new Syringe().BuildWebApplication();
await webApplication.RunAsync();

internal sealed class WeatherPlugin : IWebApplicationPlugin
{
    public void Configure(WebApplicationPluginOptions options)
    {
        options.WebApplication.MapGet("/weather", (WeatherProvider weatherProvider) =>
        {
            return Results.Ok(weatherProvider.GetWeather());
        });
    }
}

internal sealed class WeatherProvider(IConfiguration config)
{
    public object GetWeather()
    {
        var weatherConfig = config.GetSection("Weather");
        return new
        {
            TemperatureC = weatherConfig.GetValue<double>("TemperatureCelsius"),
            Summary = weatherConfig.GetValue<string>("Summary"),
        };
    }
}
```

### Fluent Configuration

```csharp
using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Scrutor;

var webApplication = new Syringe()
    .UsingScrutorTypeRegistrar()
    .UsingAssemblyProvider(builder => builder
        .MatchingAssemblies(x =>
            x.Contains("NexusLabs", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("MyApp", StringComparison.OrdinalIgnoreCase))
        .UseLibTestEntrySorting()
        .Build())
    .UsingAdditionalAssemblies(additionalAssemblies: [])
    .ForWebApplication()
    .UsingOptions(() => CreateWebApplicationOptions
        .Default
        .UsingStartupConsoleLogger())
    .BuildWebApplication();

await webApplication.RunAsync();
```

## Requirements

- .NET 9 or later
- C# 13.0 or later
