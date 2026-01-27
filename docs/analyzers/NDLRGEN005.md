# NDLRGEN005: Factory generic type not implemented by class

## Diagnostic Info

| Property | Value |
|----------|-------|
| ID | NDLRGEN005 |
| Category | NexusLabs.Needlr.Generators |
| Severity | Error |
| Enabled | Yes |

## Description

This error is raised when a class uses `[GenerateFactory<T>]` with a type parameter `T` that the class does not implement. The generic type must be an interface or base class that the decorated class implements or inherits from.

## Example

### Code that triggers the error

```csharp
using NexusLabs.Needlr.Generators;

public interface IRequestHandler { }
public interface IMessageHandler { }

// ❌ NDLRGEN005: MyHandler does not implement IMessageHandler
[GenerateFactory<IMessageHandler>]
public class MyHandler : IRequestHandler
{
    public MyHandler(ILogger logger, Guid correlationId) { }
}
```

### How to fix

Use an interface that the class actually implements:

```csharp
// ✅ Correct - MyHandler implements IRequestHandler
[GenerateFactory<IRequestHandler>]
public class MyHandler : IRequestHandler
{
    public MyHandler(ILogger logger, Guid correlationId) { }
}
```

Or implement the interface you want to use:

```csharp
// ✅ Correct - MyHandler now implements IMessageHandler
[GenerateFactory<IMessageHandler>]
public class MyHandler : IRequestHandler, IMessageHandler
{
    public MyHandler(ILogger logger, Guid correlationId) { }
}
```

Or use the non-generic attribute to return the concrete type:

```csharp
// ✅ Correct - returns concrete MyHandler type
[GenerateFactory]
public class MyHandler : IRequestHandler
{
    public MyHandler(ILogger logger, Guid correlationId) { }
}
```

## Why This Matters

The `[GenerateFactory<T>]` attribute changes the return type of the factory's `Create()` method from the concrete class to `T`. If the class doesn't implement `T`, the generated code would have an invalid cast and fail to compile.

## Generated Code Comparison

**Non-generic `[GenerateFactory]`:**
```csharp
public interface IMyHandlerFactory
{
    MyHandler Create(Guid correlationId);  // Returns concrete type
}
```

**Generic `[GenerateFactory<IRequestHandler>]`:**
```csharp
public interface IMyHandlerFactory
{
    IRequestHandler Create(Guid correlationId);  // Returns interface
}
```

## See Also

- [Factory Delegates](../factories.md)
- [NDLRGEN003](NDLRGEN003.md) - All parameters injectable
- [NDLRGEN004](NDLRGEN004.md) - No injectable parameters
