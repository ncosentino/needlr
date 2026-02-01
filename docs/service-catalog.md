# Service Catalog

The Service Catalog provides compile-time metadata about all services discovered and registered by Needlr. It allows you to query your DI registrations at runtime without reflection.

## Overview

When you use `[GenerateTypeRegistry]`, Needlr generates a `ServiceCatalog` class that implements `IServiceCatalog`. This catalog is automatically registered as a singleton in your DI container and can be resolved like any other service.

```csharp
var catalog = serviceProvider.GetRequiredService<IServiceCatalog>();
```

## What's in the Catalog

The `IServiceCatalog` interface exposes collections for each type of registration Needlr discovers:

- **Services** - All standard service registrations with their lifetime, interfaces, and constructor parameters
- **Decorators** - All decorators with their target service type and order
- **HostedServices** - All discovered `BackgroundService`/`IHostedService` implementations
- **InterceptedServices** - Services with `[Intercept]` applied
- **Options** - Configuration bindings via `[Options]`
- **Plugins** - Discovered Needlr plugins

Each entry includes the type name, assembly, and source file path (when available).

## Usage Examples

### Resolving the Catalog

```csharp
var provider = new Syringe()
    .UsingSourceGen()
    .BuildServiceProvider();

var catalog = provider.GetRequiredService<IServiceCatalog>();
```

### Finding Services by Interface

```csharp
// Find all services implementing a specific interface
var handlers = catalog.Services
    .Where(s => s.Interfaces.Any(i => i.Contains("ICommandHandler")))
    .ToList();

foreach (var handler in handlers)
{
    Console.WriteLine($"{handler.ShortTypeName} ({handler.Lifetime})");
}
```

### Listing Decorators in Order

```csharp
// Find all decorators for IHostedService in decoration order
var hostedServiceDecorators = catalog.Decorators
    .Where(d => d.ServiceTypeName.Contains("IHostedService"))
    .OrderBy(d => d.Order)
    .ToList();

foreach (var decorator in hostedServiceDecorators)
{
    Console.WriteLine($"Order {decorator.Order}: {decorator.ShortDecoratorTypeName}");
}
```

### Inspecting Hosted Services

```csharp
foreach (var hostedService in catalog.HostedServices)
{
    Console.WriteLine($"Hosted Service: {hostedService.ShortTypeName}");
    
    foreach (var param in hostedService.ConstructorParameters)
    {
        Console.WriteLine($"  - {param.Name}: {param.TypeName}");
    }
}
```

### Querying Options Configuration

```csharp
// Find options with validation enabled
var validatedOptions = catalog.Options
    .Where(o => o.ValidateOnStart || o.HasValidator || o.HasDataAnnotations)
    .ToList();

foreach (var opt in validatedOptions)
{
    Console.WriteLine($"{opt.ShortTypeName} -> Section: {opt.SectionName}");
}
```

## Use Cases

- **Debugging**: Inspect what Needlr discovered and registered at runtime
- **Documentation**: Generate API documentation from registration metadata
- **Health Checks**: Verify expected services are registered
- **Admin Endpoints**: Expose service registration info in admin/diagnostic APIs
- **Testing**: Assert specific registrations exist in integration tests

## Notes

- The catalog is generated at compile time - it reflects what the source generator discovered
- Types excluded with `[DoNotAutoRegister]` or `[DoNotInject]` will not appear in the catalog
- The catalog is registered as a singleton and is the same instance across resolutions
- Interface names in the catalog include the `global::` prefix for fully qualified names

## API Reference

See the source code for full type definitions:
- [IServiceCatalog Interface](https://github.com/ncosentino/needlr/blob/main/src/NexusLabs.Needlr/Catalog/IServiceCatalog.cs)
- [Catalog Entry Types](https://github.com/ncosentino/needlr/tree/main/src/NexusLabs.Needlr/Catalog/)
