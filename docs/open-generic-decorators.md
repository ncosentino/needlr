# Open Generic Decorators

Open generic decorators allow you to define a single decorator class that automatically wraps **all** closed implementations of an open generic interface. This is a **source-generation-only** feature.

## Overview

When you have a generic interface like `IHandler<T>` with multiple implementations (`OrderHandler : IHandler<Order>`, `PaymentHandler : IHandler<Payment>`, etc.), you typically want to apply cross-cutting concerns (logging, validation, metrics) to all of them.

With `[OpenDecoratorFor]`, you define the decorator once and Needlr generates the decorator registrations for every closed implementation at compile time.

## Basic Usage

```csharp
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

// Define your open generic interface
public interface IHandler<T>
{
    Task HandleAsync(T message);
}

// Define concrete handlers
[Singleton]
public class OrderHandler : IHandler<Order>
{
    public Task HandleAsync(Order message) { /* ... */ }
}

[Singleton]
public class PaymentHandler : IHandler<Payment>
{
    public Task HandleAsync(Payment message) { /* ... */ }
}

// Define an open generic decorator
[OpenDecoratorFor(typeof(IHandler<>))]
public class LoggingDecorator<T> : IHandler<T>
{
    private readonly IHandler<T> _inner;
    private readonly ILogger<LoggingDecorator<T>> _logger;

    public LoggingDecorator(IHandler<T> inner, ILogger<LoggingDecorator<T>> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task HandleAsync(T message)
    {
        _logger.LogInformation("Handling {MessageType}", typeof(T).Name);
        await _inner.HandleAsync(message);
        _logger.LogInformation("Handled {MessageType}", typeof(T).Name);
    }
}
```

## What Gets Generated

At compile time, Needlr discovers all closed implementations of `IHandler<T>` and generates decorator registrations for each:

```csharp
// Generated code (simplified)
services.AddDecorator<IHandler<Order>, LoggingDecorator<Order>>();
services.AddDecorator<IHandler<Payment>, LoggingDecorator<Payment>>();
```

## Ordering Multiple Decorators

Use the `Order` property to control decorator application order (lower = closer to the original service):

```csharp
[OpenDecoratorFor(typeof(IHandler<>), Order = 1)]
public class LoggingDecorator<T> : IHandler<T> { /* ... */ }

[OpenDecoratorFor(typeof(IHandler<>), Order = 2)]
public class MetricsDecorator<T> : IHandler<T> { /* ... */ }

// Result chain: MetricsDecorator → LoggingDecorator → Handler
```

## Multi-Parameter Generics

Open generic decorators work with interfaces that have multiple type parameters:

```csharp
public interface IRequestHandler<TRequest, TResponse>
{
    TResponse Handle(TRequest request);
}

[OpenDecoratorFor(typeof(IRequestHandler<,>))]
public class ValidationDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
{
    private readonly IRequestHandler<TRequest, TResponse> _inner;
    
    public ValidationDecorator(IRequestHandler<TRequest, TResponse> inner) => _inner = inner;
    
    public TResponse Handle(TRequest request)
    {
        Validate(request);
        return _inner.Handle(request);
    }
}
```

## Comparison with `[DecoratorFor<T>]`

| Feature | `[DecoratorFor<T>]` | `[OpenDecoratorFor]` |
|---------|---------------------|----------------------|
| Supports closed types | ✅ | ❌ |
| Supports open generics | ❌ | ✅ |
| Works with reflection | ✅ | ❌ |
| Works with source-gen | ✅ | ✅ |
| Attribute location | `NexusLabs.Needlr` | `NexusLabs.Needlr.Generators` |

**Use `[DecoratorFor<T>]`** when:
- You're decorating a specific closed type (e.g., `IOrderService`)
- You need reflection path compatibility

**Use `[OpenDecoratorFor]`** when:
- You want to decorate all implementations of a generic interface
- You're using source generation (required)
- You want compile-time expansion

## Validation

Needlr provides compile-time analyzers to catch configuration errors:

| Diagnostic | Description |
|------------|-------------|
| [NDLRGEN006](analyzers/NDLRGEN006.md) | Type argument must be an open generic interface |
| [NDLRGEN007](analyzers/NDLRGEN007.md) | Decorator class must be an open generic with matching arity |
| [NDLRGEN008](analyzers/NDLRGEN008.md) | Decorator must implement the interface it decorates |

### Example Errors

```csharp
// NDLRGEN006: Must use typeof(IHandler<>), not typeof(IHandler<string>)
[OpenDecoratorFor(typeof(IHandler<string>))]  // ❌ Error
public class BadDecorator<T> : IHandler<T> { }

// NDLRGEN007: Decorator must be generic with same parameter count
[OpenDecoratorFor(typeof(IHandler<>))]
public class BadDecorator : IHandler<string> { }  // ❌ Error - not generic

// NDLRGEN008: Decorator must implement the interface
[OpenDecoratorFor(typeof(IHandler<>))]
public class BadDecorator<T> { }  // ❌ Error - doesn't implement IHandler<T>
```

## Namespace Import

The `[OpenDecoratorFor]` attribute is in the `NexusLabs.Needlr.Generators` namespace:

```csharp
using NexusLabs.Needlr.Generators;
```

This is intentional - it signals that the feature is source-generation-only.

## Common Patterns

### CQRS Command/Query Decorators

```csharp
public interface ICommandHandler<TCommand> where TCommand : ICommand
{
    Task HandleAsync(TCommand command, CancellationToken ct);
}

public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken ct);
}

// Apply validation to all commands
[OpenDecoratorFor(typeof(ICommandHandler<>), Order = 1)]
public class ValidationDecorator<T> : ICommandHandler<T> where T : ICommand
{
    private readonly ICommandHandler<T> _inner;
    private readonly IValidator<T> _validator;
    
    public ValidationDecorator(ICommandHandler<T> inner, IValidator<T> validator)
    {
        _inner = inner;
        _validator = validator;
    }
    
    public async Task HandleAsync(T command, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(command, ct);
        await _inner.HandleAsync(command, ct);
    }
}
```

### Retry/Resilience Patterns

```csharp
[OpenDecoratorFor(typeof(IHandler<>), Order = 0)]  // Outermost
public class RetryDecorator<T> : IHandler<T>
{
    private readonly IHandler<T> _inner;
    private readonly IAsyncPolicy _policy;
    
    public RetryDecorator(IHandler<T> inner, IAsyncPolicy policy)
    {
        _inner = inner;
        _policy = policy;
    }
    
    public Task HandleAsync(T message) => 
        _policy.ExecuteAsync(() => _inner.HandleAsync(message));
}
```

## Limitations

1. **Source-generation only**: This feature is not available in the reflection path
2. **Compile-time discovery**: Only implementations visible at compile time are decorated
3. **Same assembly or referenced assemblies**: Implementations must be discoverable by the source generator
