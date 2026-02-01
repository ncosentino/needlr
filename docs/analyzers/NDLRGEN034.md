# NDLRGEN034: Circular provider dependency detected

## Diagnostic Info

| Property | Value |
|----------|-------|
| ID | NDLRGEN034 |
| Category | NexusLabs.Needlr.Generators |
| Severity | Error |
| Enabled | Yes |

## Description

This error is raised when providers reference each other in a way that creates a circular dependency. This would cause a stack overflow at runtime when attempting to construct the providers.

## Example

### Code that triggers the error

```csharp
using NexusLabs.Needlr.Generators;

// ❌ NDLRGEN034: Circular dependency detected
[Provider]
public interface IProviderA
{
    IProviderB ProviderB { get; }  // References B
}

[Provider]
public interface IProviderB
{
    IProviderA ProviderA { get; }  // References A → Cycle!
}
```

### How to fix

Break the circular dependency by restructuring your providers:

**Option 1: Remove one of the references**
```csharp
[Provider]
public interface IProviderA
{
    IServiceFromB Service { get; }  // Reference the service directly, not the provider
}

[Provider]
public interface IProviderB
{
    IProviderA ProviderA { get; }
}
```

**Option 2: Create a third provider for shared services**
```csharp
// Shared services used by both
[Provider]
public interface ISharedProvider
{
    ISharedService SharedService { get; }
}

[Provider]
public interface IProviderA
{
    ISharedProvider Shared { get; }
    IServiceA ServiceA { get; }
}

[Provider]
public interface IProviderB
{
    ISharedProvider Shared { get; }
    IServiceB ServiceB { get; }
}
```

**Option 3: Use factory for deferred resolution**
```csharp
[Provider]
public interface IProviderA
{
    Func<IProviderB> ProviderBFactory { get; }  // Lazy resolution breaks the cycle
}
```

## Why This Matters

Circular dependencies in constructor injection cause infinite recursion:

1. Constructing `ProviderA` requires `ProviderB`
2. Constructing `ProviderB` requires `ProviderA`
3. Constructing `ProviderA` requires `ProviderB`
4. ... stack overflow

The analyzer detects these cycles at compile time before they become runtime failures.

## Detection Scope

The analyzer detects:

- **Direct cycles**: A → B → A
- **Indirect cycles**: A → B → C → A

## See Also

- [Providers](../providers.md)
- [NDLRGEN031](NDLRGEN031.md) - Missing partial modifier
- [Factories](../factories.md) - Alternative for breaking cycles
