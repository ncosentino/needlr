# NDLRGEN032: Provider interface has invalid member

## Diagnostic Info

| Property | Value |
|----------|-------|
| ID | NDLRGEN032 |
| Category | NexusLabs.Needlr.Generators |
| Severity | Error |
| Enabled | Yes |

## Description

This error is raised when a `[Provider]` interface contains members other than get-only properties. Provider interfaces should only have read-only properties representing the services to be resolved.

## Example

### Code that triggers the error

```csharp
using NexusLabs.Needlr.Generators;

[Provider]
public interface IMyProvider
{
    IService Service { get; }
    
    // ❌ NDLRGEN032: Settable property not allowed
    ILogger Logger { get; set; }
    
    // ❌ NDLRGEN032: Methods not allowed
    void DoSomething();
}
```

### How to fix

Remove the invalid members or convert them to get-only properties:

```csharp
using NexusLabs.Needlr.Generators;

// ✅ Correct: Only get-only properties
[Provider]
public interface IMyProvider
{
    IService Service { get; }
    ILogger Logger { get; }
}
```

## Why This Matters

Provider interfaces are designed to be simple service containers. The generator:

1. Creates a constructor that accepts all property types
2. Assigns each service to its corresponding property

Methods and settable properties don't fit this model and would require manual implementation.

## Supported Member Types

| Member Type | Supported |
|-------------|-----------|
| Get-only property (`{ get; }`) | ✅ Yes |
| Settable property (`{ get; set; }`) | ❌ No |
| Init-only property (`{ get; init; }`) | ❌ No |
| Methods | ❌ No |
| Events | ❌ No |
| Indexers | ❌ No |

## Alternative: Use Shorthand Mode

If you need more flexibility, consider using the shorthand class mode where you control the class definition:

```csharp
[Provider(typeof(IService), typeof(ILogger))]
public partial class MyProvider
{
    // You can add your own methods here
    public void DoSomething()
    {
        Service.Process();
        Logger.LogInformation("Done");
    }
}
```

## See Also

- [Providers](../providers.md)
- [NDLRGEN031](NDLRGEN031.md) - Missing partial modifier
- [NDLRGEN033](NDLRGEN033.md) - Concrete type warning
