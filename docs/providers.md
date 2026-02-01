# Providers (Typed Service Locators)

Providers offer a strongly-typed alternative to the service locator pattern (`IServiceProvider.GetService<T>()`). Instead of relying on runtime type resolution, providers give you compile-time guarantees and IntelliSense for the services you need.

## Why Use Providers?

The service locator pattern is considered an anti-pattern because it:

- Hides dependencies (not visible in constructor signature)
- Fails at runtime instead of compile time
- Makes code harder to test
- Provides no IntelliSense for available services

Providers solve these problems by generating strongly-typed interfaces with compile-time validation.

## Basic Usage

### Interface Mode

Define an interface decorated with `[Provider]`:

```csharp
using NexusLabs.Needlr.Generators;

[Provider]
public interface IOrderServicesProvider
{
    IOrderRepository Repository { get; }
    IOrderValidator Validator { get; }
    IOrderNotifier Notifier { get; }
}
```

The source generator creates:

- **`OrderServicesProvider`** - Implementation class in `{AssemblyName}.Generated` namespace
- **Singleton registration** - Provider is registered and resolved as a singleton

### Shorthand Mode

For quick provider definitions, use the shorthand syntax on a partial class:

```csharp
[Provider(typeof(IOrderRepository), typeof(IOrderValidator))]
public partial class OrderProvider { }
```

The generator creates:

- **`IOrderProvider`** - Generated interface with properties for each type
- **Partial class implementation** - Completes your class with constructor and properties

## Resolving Providers

```csharp
// Get the provider (singleton)
var orderServices = serviceProvider.GetRequiredService<IOrderServicesProvider>();

// Use strongly-typed properties
var order = orderServices.Repository.GetById(orderId);
if (orderServices.Validator.Validate(order))
{
    orderServices.Notifier.NotifyCustomer(order);
}
```

## Optional Services

Use nullable types for optional dependencies:

```csharp
[Provider]
public interface IServicesProvider
{
    IRequiredService Required { get; }
    IOptionalService? Optional { get; }  // May be null if not registered
}
```

Or with shorthand:

```csharp
[Provider(
    Required = new[] { typeof(IRequiredService) },
    Optional = new[] { typeof(IOptionalService) })]
public partial class ServicesProvider { }
```

Optional services are resolved with `GetService<T>()` and may be null. The generated constructor uses optional parameters with default null values.

## Collection Services

Resolve all implementations of a service:

```csharp
[Provider]
public interface IHandlersProvider
{
    IEnumerable<IEventHandler> EventHandlers { get; }
}
```

Or with shorthand:

```csharp
[Provider(Collections = new[] { typeof(IEventHandler) })]
public partial class HandlersProvider { }
```

The property name is automatically pluralized (`IEventHandler` → `EventHandlers`).

## Factory Services

For services that need runtime parameters, use the `Factories` parameter:

```csharp
[GenerateFactory]
public class ReportGenerator
{
    public ReportGenerator(ILogger<ReportGenerator> logger, string reportTitle) { }
}

[Provider(Factories = new[] { typeof(ReportGenerator) })]
public partial class ReportingProvider { }
```

This generates a property for `IReportGeneratorFactory`:

```csharp
// Generated interface includes factory
public interface IReportingProvider
{
    IReportGeneratorFactory ReportGeneratorFactory { get; }
}

// Usage
var reporting = serviceProvider.GetRequiredService<IReportingProvider>();
var report = reporting.ReportGeneratorFactory.Create("Monthly Sales Report");
```

## Nested Providers

Providers can reference other providers for modular service organization:

```csharp
[Provider]
public interface IOrderServicesProvider
{
    IOrderRepository Repository { get; }
    IOrderValidator Validator { get; }
}

[Provider]
public interface IShippingServicesProvider
{
    IShippingCalculator Calculator { get; }
    ICarrierService Carrier { get; }
}

[Provider]
public interface IEcommerceProvider
{
    IOrderServicesProvider Orders { get; }
    IShippingServicesProvider Shipping { get; }
}
```

Nested providers maintain singleton semantics - the same instance is shared.

## Mixed Property Kinds

Combine all property kinds in a single provider:

```csharp
[Provider]
public interface IApplicationServicesProvider
{
    // Required - must be registered
    IUserService UserService { get; }
    
    // Optional - may be null
    IAnalyticsService? Analytics { get; }
    
    // Collection - all implementations
    IEnumerable<IHealthCheck> HealthChecks { get; }
}
```

Or with shorthand:

```csharp
[Provider(
    Required = new[] { typeof(IUserService) },
    Optional = new[] { typeof(IAnalyticsService) },
    Collections = new[] { typeof(IHealthCheck) },
    Factories = new[] { typeof(BackgroundJob) })]
public partial class ApplicationProvider { }
```

## Singleton Behavior

**Providers are always registered as Singletons.** This means:

- All services are resolved at construction time (fail-fast)
- The same provider instance is reused throughout the application
- Service properties return the same instances on every access

For new instances on demand, use factory properties instead of direct service references.

## Analyzers

Needlr includes analyzers to validate provider usage at compile time:

| Diagnostic | Severity | Description |
|------------|----------|-------------|
| [NDLRGEN031](analyzers/NDLRGEN031.md) | Error | `[Provider]` on class requires `partial` modifier |
| [NDLRGEN032](analyzers/NDLRGEN032.md) | Error | `[Provider]` interface has invalid member (methods, settable properties) |
| [NDLRGEN033](analyzers/NDLRGEN033.md) | Warning | Provider property uses concrete type instead of interface |
| [NDLRGEN034](analyzers/NDLRGEN034.md) | Error | Circular provider dependency detected |

## Namespace

Generated provider implementations are placed in:

- **Interface mode**: `{AssemblyName}.Generated` namespace
- **Shorthand mode**: Same namespace as your partial class

```csharp
using MyApp.Generated;  // Contains OrderServicesProvider, etc.
```

## When to Use Providers

**Use Providers when:**

- You need to access multiple related services together
- You want compile-time validation of service dependencies
- You're replacing `IServiceProvider.GetService<T>()` calls
- You want better IntelliSense and discoverability
- You're organizing services into logical groups

**Consider Alternatives when:**

- You only need one service (use constructor injection directly)
- You need to create scoped instances (use `IServiceScopeFactory`)
- You need runtime service resolution based on conditions (use `IServiceProvider`)

## Complete Example

```csharp
using NexusLabs.Needlr.Generators;
using MyApp.Generated;

// Define your services
public interface IUserRepository { User GetById(Guid id); }
public interface IEmailService { Task SendAsync(string to, string subject, string body); }
public interface IAuditLogger { void Log(string action); }

// Define a provider for user-related services
[Provider]
public interface IUserServicesProvider
{
    IUserRepository Repository { get; }
    IEmailService Email { get; }
    IAuditLogger? Audit { get; }  // Optional
}

// Use in your application
public class UserController
{
    private readonly IUserServicesProvider _services;
    
    public UserController(IUserServicesProvider services)
    {
        _services = services;
    }
    
    public async Task<IActionResult> NotifyUser(Guid userId, string message)
    {
        var user = _services.Repository.GetById(userId);
        
        await _services.Email.SendAsync(user.Email, "Notification", message);
        
        // Audit is optional - check for null
        _services.Audit?.Log($"Notified user {userId}");
        
        return Ok();
    }
}
```
