---
description: Register and resolve multiple implementations of the same interface in .NET 8+ using Needlr's Keyed attribute -- keyed service support with source generation and reflection.
---

# Keyed Services

Keyed services allow multiple implementations of the same interface to be registered with different keys, enabling consumers to resolve specific implementations.

## Registering Keyed Services

Use the `[Keyed]` attribute to register a service with a specific key:

```csharp
public interface ICacheProvider
{
    string Name { get; }
}

[Keyed("redis")]
public sealed class RedisCacheProvider : ICacheProvider
{
    public string Name => "redis";
}

[Keyed("memory")]
public sealed class MemoryCacheProvider : ICacheProvider
{
    public string Name => "memory";
}
```

This generates registrations equivalent to:

```csharp
services.AddKeyedSingleton<ICacheProvider, RedisCacheProvider>("redis");
services.AddKeyedSingleton<ICacheProvider, MemoryCacheProvider>("memory");
```

## Consuming Keyed Services

Use the `[FromKeyedServices]` attribute (from `Microsoft.Extensions.DependencyInjection`) on constructor parameters to resolve keyed services:

```csharp
public sealed class CacheManager
{
    private readonly ICacheProvider _primaryCache;
    private readonly ICacheProvider _fallbackCache;

    public CacheManager(
        [FromKeyedServices("redis")] ICacheProvider primaryCache,
        [FromKeyedServices("memory")] ICacheProvider fallbackCache)
    {
        _primaryCache = primaryCache;
        _fallbackCache = fallbackCache;
    }
}
```

## Mixing Keyed and Regular Dependencies

You can combine keyed and regular (unkeyed) dependencies in the same constructor:

```csharp
public sealed class OrderService
{
    public OrderService(
        [FromKeyedServices("primary")] IPaymentProcessor processor,
        ILogger<OrderService> logger)  // Regular dependency
    {
        // ...
    }
}
```

## Registering via Plugin

For more complex registration scenarios, use `IServiceCollectionPlugin`:

```csharp
public sealed class PaymentServicesPlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        options.Services.AddKeyedSingleton<IPaymentProcessor, StripeProcessor>("stripe");
        options.Services.AddKeyedSingleton<IPaymentProcessor, PayPalProcessor>("paypal");
    }
}
```

## Notes

- `[Keyed]` can be applied multiple times to register the same class with different keys
- Keyed services respect the same lifetime rules as regular services
- The key is a string value passed to `[FromKeyedServices("key")]` when resolving
- This feature requires .NET 8+ (where keyed services were introduced)
- Both source-gen and reflection paths support keyed services

!!! info "Read More on Dev Leader"
    - [Keyed Services in Needlr: Managing Multiple Implementations](https://www.devleader.ca/2026/02/19/keyed-services-in-needlr-managing-multiple-implementations)
