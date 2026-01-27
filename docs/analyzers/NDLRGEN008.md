# NDLRGEN008: [OpenDecoratorFor] decorator must implement the interface

## Cause

A class marked with `[OpenDecoratorFor(typeof(IInterface<>))]` does not implement the open generic interface specified in the attribute.

## Rule Description

For the decorator pattern to work, the decorator class must implement the same interface as the services it wraps. This allows the decorator to be substituted for the original service in the DI container.

When using `[OpenDecoratorFor(typeof(IHandler<>))]`, your decorator class must implement `IHandler<T>`.

## How to Fix

Add the interface implementation to your decorator class:

```csharp
// ❌ Wrong - doesn't implement IHandler<T>
[OpenDecoratorFor(typeof(IHandler<>))]
public class LoggingDecorator<T>
{
    // Missing : IHandler<T>
}

// ✅ Correct - implements IHandler<T>
[OpenDecoratorFor(typeof(IHandler<>))]
public class LoggingDecorator<T> : IHandler<T>
{
    private readonly IHandler<T> _inner;
    public LoggingDecorator(IHandler<T> inner) => _inner = inner;
    public void Handle(T message) => _inner.Handle(message);
}
```

## Example

### Code with Error

```csharp
using NexusLabs.Needlr.Generators;

public interface IHandler<T> { void Handle(T message); }

// NDLRGEN008: Class 'LoggingDecorator' has [OpenDecoratorFor(IHandler<>)] 
//             but does not implement 'IHandler<>'
[OpenDecoratorFor(typeof(IHandler<>))]
public class LoggingDecorator<T>
{
    private readonly IHandler<T> _inner;
    
    public LoggingDecorator(IHandler<T> inner) => _inner = inner;
    
    public void Handle(T message)
    {
        Console.WriteLine("Logging...");
        _inner.Handle(message);
    }
}
```

### Fixed Code

```csharp
using NexusLabs.Needlr.Generators;

public interface IHandler<T> { void Handle(T message); }

// ✅ Now implements IHandler<T>
[OpenDecoratorFor(typeof(IHandler<>))]
public class LoggingDecorator<T> : IHandler<T>  // Added interface
{
    private readonly IHandler<T> _inner;
    
    public LoggingDecorator(IHandler<T> inner) => _inner = inner;
    
    public void Handle(T message)
    {
        Console.WriteLine("Logging...");
        _inner.Handle(message);
    }
}
```

## Why This Matters

The decorator pattern relies on polymorphism - the decorator must be usable wherever the original interface is expected:

```csharp
IHandler<Order> handler = new OrderHandler();
IHandler<Order> decorated = new LoggingDecorator<Order>(handler);

// The container resolves IHandler<Order> → LoggingDecorator<Order>
// LoggingDecorator<Order> wraps the original OrderHandler
```

If the decorator doesn't implement the interface, this substitution is impossible.

## See Also

- [NDLRGEN006](NDLRGEN006.md) - Type argument must be an open generic interface
- [NDLRGEN007](NDLRGEN007.md) - Decorator class must be an open generic
- [Open Generic Decorators Guide](../open-generic-decorators.md)
