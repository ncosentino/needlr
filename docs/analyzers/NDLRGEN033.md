# NDLRGEN033: Provider property uses concrete type

## Diagnostic Info

| Property | Value |
|----------|-------|
| ID | NDLRGEN033 |
| Category | NexusLabs.Needlr.Generators |
| Severity | Warning |
| Enabled | Yes |

## Description

This warning is raised when a provider property uses a concrete class type instead of an interface. While this works, using interfaces is generally preferred for better testability and flexibility.

## Example

### Code that triggers the warning

```csharp
using NexusLabs.Needlr.Generators;

public class OrderRepository { }  // Concrete class

[Provider]
public interface IMyProvider
{
    // ⚠️ NDLRGEN033: Consider using an interface
    OrderRepository Repository { get; }
}
```

### How to fix

Define an interface for the service:

```csharp
using NexusLabs.Needlr.Generators;

public interface IOrderRepository { }
public class OrderRepository : IOrderRepository { }

// ✅ Correct: Uses interface type
[Provider]
public interface IMyProvider
{
    IOrderRepository Repository { get; }
}
```

## Why This Matters

Using interfaces instead of concrete types:

- **Testability**: Easy to mock in unit tests
- **Flexibility**: Can swap implementations without changing the provider
- **Dependency Inversion**: Follows SOLID principles
- **Loose Coupling**: Reduces dependencies between components

## When Concrete Types Are Acceptable

This is a warning, not an error. Concrete types are acceptable in some scenarios:

- **Value objects** that have no behavior to mock
- **Sealed utility classes** that are lightweight and deterministic
- **Third-party classes** that don't implement interfaces

To suppress the warning:

```csharp
#pragma warning disable NDLRGEN033
[Provider]
public interface IMyProvider
{
    ConcreteClass MyProperty { get; }
}
#pragma warning restore NDLRGEN033
```

## Exceptions

The warning is not raised for:

- **Interface types** (obviously)
- **Factory types** (ending with "Factory")
- **Collection types** (`IEnumerable<T>`, etc.)
- **Other providers** (nested provider references)

## See Also

- [Providers](../providers.md)
- [NDLRGEN031](NDLRGEN031.md) - Missing partial modifier
- [NDLRGEN032](NDLRGEN032.md) - Invalid interface member
