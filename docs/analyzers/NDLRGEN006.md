# NDLRGEN006: [OpenDecoratorFor] type argument must be an open generic interface

## Cause

The type argument passed to `[OpenDecoratorFor]` is not an open generic interface (unbound generic type).

## Rule Description

The `[OpenDecoratorFor]` attribute is designed to create decorators for all closed implementations of an open generic interface. The type argument must be:

1. **An interface** (not a class or struct)
2. **An open/unbound generic** using the `typeof(IInterface<>)` syntax

Common mistakes include:
- Passing a closed generic type like `typeof(IHandler<string>)`
- Passing a non-generic interface like `typeof(IService)`
- Passing a class instead of an interface

## How to Fix

Use the open generic `typeof()` syntax with empty angle brackets:

```csharp
// ❌ Wrong - closed generic
[OpenDecoratorFor(typeof(IHandler<string>))]

// ❌ Wrong - non-generic
[OpenDecoratorFor(typeof(IService))]

// ✅ Correct - open generic interface
[OpenDecoratorFor(typeof(IHandler<>))]
```

## Example

### Code with Error

```csharp
using NexusLabs.Needlr.Generators;

public interface IHandler<T> { void Handle(T message); }

// NDLRGEN006: Type argument 'IHandler<string>' is not an open generic interface
[OpenDecoratorFor(typeof(IHandler<string>))]
public class LoggingDecorator<T> : IHandler<T>
{
    private readonly IHandler<T> _inner;
    public LoggingDecorator(IHandler<T> inner) => _inner = inner;
    public void Handle(T message) => _inner.Handle(message);
}
```

### Fixed Code

```csharp
using NexusLabs.Needlr.Generators;

public interface IHandler<T> { void Handle(T message); }

// ✅ Using open generic syntax
[OpenDecoratorFor(typeof(IHandler<>))]
public class LoggingDecorator<T> : IHandler<T>
{
    private readonly IHandler<T> _inner;
    public LoggingDecorator(IHandler<T> inner) => _inner = inner;
    public void Handle(T message) => _inner.Handle(message);
}
```

## Alternative Approaches

If you want to decorate a **specific closed type** instead of all implementations:

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

- [NDLRGEN007](NDLRGEN007.md) - Decorator class must be an open generic
- [NDLRGEN008](NDLRGEN008.md) - Decorator must implement the interface
- [Open Generic Decorators Guide](../open-generic-decorators.md)
