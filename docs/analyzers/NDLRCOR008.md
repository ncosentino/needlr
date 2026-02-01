# NDLRCOR008: [Intercept] applied to class without interfaces

## Cause

An `[Intercept]` or `[Intercept<T>]` attribute is applied to a class that does not implement any user-defined interfaces.

## Rule Description

Interceptors work by generating a proxy class that implements the service's interfaces. When a consumer resolves `IOrderService`, they receive the proxy instead of the original implementation. The proxy intercepts method calls and forwards them through the interceptor chain.

If the class doesn't implement any interfaces, there's nothing for the proxy to implement, and the interceptor cannot be applied.

```csharp
// ⚠️ NDLRCOR008: OrderService doesn't implement any interfaces
[Intercept<LoggingInterceptor>]
public class OrderService  // No interface!
{
    public Order GetOrder(int id) => /* ... */;
}
```

## How to Fix

### Option 1: Add an Interface (Recommended)

Define an interface for your service:

```csharp
// ✅ Fixed: OrderService now implements IOrderService
public interface IOrderService
{
    Order GetOrder(int id);
}

[Intercept<LoggingInterceptor>]
public class OrderService : IOrderService
{
    public Order GetOrder(int id) => /* ... */;
}
```

### Option 2: Use a Decorator Instead

If you can't add an interface, consider using a decorator pattern with manual registration:

```csharp
public class OrderService
{
    public virtual Order GetOrder(int id) => /* ... */;
}

public class LoggingOrderService : OrderService
{
    private readonly OrderService _inner;
    
    public LoggingOrderService(OrderService inner) => _inner = inner;
    
    public override Order GetOrder(int id)
    {
        Console.WriteLine("Getting order...");
        return _inner.GetOrder(id);
    }
}
```

### Option 3: Remove the Attribute

If interception isn't needed, remove the `[Intercept]` attribute:

```csharp
// No interception
public class OrderService
{
    public Order GetOrder(int id) => /* ... */;
}
```

## Why Interfaces Are Required

The source generator creates a proxy class like this:

```csharp
// Generated proxy
internal sealed class OrderService_InterceptorProxy : IOrderService
{
    private readonly OrderService _target;
    private readonly IServiceProvider _serviceProvider;
    
    public Order GetOrder(int id)
    {
        // Interceptor chain logic...
    }
}
```

The proxy must implement an interface so that consumers can depend on the interface while receiving the proxy. Without an interface, this pattern isn't possible.

## When to Suppress

Suppress this warning if you're intentionally applying `[Intercept]` for future use when an interface will be added:

```csharp
#pragma warning disable NDLRCOR008
[Intercept<LoggingInterceptor>]  // Interface coming in next PR
public class OrderService
{
    // ...
}
#pragma warning restore NDLRCOR008
```

## See Also

- [Interceptors](../interceptors.md) - Full interceptor documentation
- [Open Generic Decorators](../open-generic-decorators.md) - Alternative for interface-less services
