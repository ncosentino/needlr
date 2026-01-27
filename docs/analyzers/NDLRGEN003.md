# NDLRGEN003: All factory parameters are injectable

## Diagnostic Info

| Property | Value |
|----------|-------|
| ID | NDLRGEN003 |
| Category | NexusLabs.Needlr.Generators |
| Severity | Warning |
| Enabled | Yes |

## Description

This warning is raised when a class marked with `[GenerateFactory]` has a constructor where all parameters are injectable types (interfaces or classes). In this case, the factory provides no additional value since the type could be auto-registered normally.

## Example

### Code that triggers the warning

```csharp
using NexusLabs.Needlr.Generators;

// ⚠️ NDLRGEN003: All parameters are injectable, factory provides no value
[GenerateFactory]
public class MyService
{
    public MyService(ILogger<MyService> logger, IConfiguration config)
    {
        // Both parameters are injectable - no need for a factory
    }
}
```

### How to fix

Either remove the `[GenerateFactory]` attribute (the type will be auto-registered normally), or add a runtime parameter that justifies the factory:

**Option 1: Remove the attribute**
```csharp
// Let Needlr auto-register this normally
public class MyService
{
    public MyService(ILogger<MyService> logger, IConfiguration config)
    {
    }
}
```

**Option 2: Add a runtime parameter**
```csharp
[GenerateFactory]
public class MyService
{
    public MyService(ILogger<MyService> logger, IConfiguration config, string tenantId)
    {
        // Now the factory makes sense - tenantId must be provided at runtime
    }
}
```

## Why This Matters

Factory generation adds code to your assembly. If all parameters can be auto-injected, the factory is unnecessary overhead. The type can simply be registered directly with the container.

## See Also

- [Factory Delegates](../factories.md)
- [NDLRGEN004](NDLRGEN004.md) - No injectable parameters
- [NDLRGEN005](NDLRGEN005.md) - Invalid generic type parameter
