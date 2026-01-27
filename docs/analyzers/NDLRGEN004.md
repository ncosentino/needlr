# NDLRGEN004: No factory parameters are injectable

## Diagnostic Info

| Property | Value |
|----------|-------|
| ID | NDLRGEN004 |
| Category | NexusLabs.Needlr.Generators |
| Severity | Warning |
| Enabled | Yes |

## Description

This warning is raised when a class marked with `[GenerateFactory]` has a constructor where no parameters are injectable types. The factory will still work, but it provides low value since there are no dependencies to inject - you could simply use `new` directly.

## Example

### Code that triggers the warning

```csharp
using NexusLabs.Needlr.Generators;

// ⚠️ NDLRGEN004: No parameters are injectable, factory provides low value
[GenerateFactory]
public class ConnectionString
{
    public ConnectionString(string server, int port, string database)
    {
        // All parameters are runtime - nothing to inject
    }
}
```

### How to fix

Either remove the `[GenerateFactory]` attribute and use `new` directly, or add an injectable dependency:

**Option 1: Remove the attribute and use `new`**
```csharp
// Just use new directly - no factory needed
public class ConnectionString
{
    public ConnectionString(string server, int port, string database) { }
}

// Usage
var conn = new ConnectionString("localhost", 5432, "mydb");
```

**Option 2: Add an injectable dependency**
```csharp
[GenerateFactory]
public class ConnectionString
{
    public ConnectionString(IConfiguration config, string server, int port, string database)
    {
        // Now the factory provides value - it injects IConfiguration
    }
}
```

## Why This Matters

The primary purpose of factory generation is to partition constructor parameters into those that can be auto-injected and those that must be provided at runtime. If there's nothing to inject, the factory is just an extra layer of indirection.

## When to Suppress

You might intentionally want a factory for a type with no injectable dependencies if:
- You want consistent factory patterns across your codebase
- You anticipate adding injectable dependencies later
- You prefer resolving factories from DI for consistency

```csharp
#pragma warning disable NDLRGEN004
[GenerateFactory]
public class ConnectionString { /* ... */ }
#pragma warning restore NDLRGEN004
```

## See Also

- [Factory Delegates](../factories.md)
- [NDLRGEN003](NDLRGEN003.md) - All parameters injectable
- [NDLRGEN005](NDLRGEN005.md) - Invalid generic type parameter
