# NDLRGEN007: [OpenDecoratorFor] decorator must be an open generic class

## Cause

A class marked with `[OpenDecoratorFor(typeof(IInterface<>))]` is either:
1. Not a generic class, or
2. Has a different number of type parameters than the interface

## Rule Description

When using `[OpenDecoratorFor]`, the decorator class must be an open generic with the **same number of type parameters** as the interface it decorates. This is because Needlr needs to close both the interface and decorator with the same type arguments at compile time.

For example:
- `IHandler<T>` (1 type parameter) → decorator must have 1 type parameter
- `IRequestHandler<TRequest, TResponse>` (2 type parameters) → decorator must have 2 type parameters

## How to Fix

Ensure your decorator class:
1. Is a generic class
2. Has the same number of type parameters as the target interface

```csharp
// ❌ Wrong - decorator is not generic
[OpenDecoratorFor(typeof(IHandler<>))]
public class LoggingDecorator : IHandler<string> { }

// ❌ Wrong - arity mismatch (1 vs 2)
[OpenDecoratorFor(typeof(IRequestHandler<,>))]
public class LoggingDecorator<T> : IRequestHandler<T, T> { }

// ✅ Correct - same arity
[OpenDecoratorFor(typeof(IHandler<>))]
public class LoggingDecorator<T> : IHandler<T> { }

// ✅ Correct - both have 2 type parameters
[OpenDecoratorFor(typeof(IRequestHandler<,>))]
public class LoggingDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse> { }
```

## Example

### Code with Error

```csharp
using NexusLabs.Needlr.Generators;

public interface IHandler<T> { void Handle(T message); }

// NDLRGEN007: Class 'LoggingDecorator' must be an open generic class with 1 type parameter(s)
[OpenDecoratorFor(typeof(IHandler<>))]
public class LoggingDecorator : IHandler<string>
{
    private readonly IHandler<string> _inner;
    public LoggingDecorator(IHandler<string> inner) => _inner = inner;
    public void Handle(string message) => _inner.Handle(message);
}
```

### Fixed Code

```csharp
using NexusLabs.Needlr.Generators;

public interface IHandler<T> { void Handle(T message); }

// ✅ Decorator is now generic
[OpenDecoratorFor(typeof(IHandler<>))]
public class LoggingDecorator<T> : IHandler<T>
{
    private readonly IHandler<T> _inner;
    public LoggingDecorator(IHandler<T> inner) => _inner = inner;
    public void Handle(T message) => _inner.Handle(message);
}
```

## Alternative Approaches

If you want to create a non-generic decorator for a specific closed type:

```csharp
// Use [DecoratorFor<T>] for closed types
[DecoratorFor<IHandler<string>>]
public class StringHandlerDecorator : IHandler<string>
{
    private readonly IHandler<string> _inner;
    public StringHandlerDecorator(IHandler<string> inner) => _inner = inner;
    public void Handle(string message) => _inner.Handle(message);
}
```

## See Also

- [NDLRGEN006](NDLRGEN006.md) - Type argument must be an open generic interface
- [NDLRGEN008](NDLRGEN008.md) - Decorator must implement the interface
- [Open Generic Decorators Guide](../open-generic-decorators.md)
