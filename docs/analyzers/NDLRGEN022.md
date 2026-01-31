# NDLRGEN022: Disposable captive dependency detected

| Property | Value |
|----------|-------|
| **Diagnostic ID** | NDLRGEN022 |
| **Severity** | Error |
| **Category** | NexusLabs.Needlr.Generators |
| **Default** | Enabled |

## Summary

A longer-lived service captures a shorter-lived service that implements `IDisposable` or `IAsyncDisposable`. This is a "captive dependency" anti-pattern that can cause `ObjectDisposedException` at runtime.

## Description

When a Singleton or Scoped service has a constructor dependency on a service with a shorter lifetime (Scoped or Transient respectively) that implements `IDisposable`, the disposable will be disposed when its scope ends while the consuming service continues to hold a reference.

This diagnostic uses Needlr's **inferred lifetimes** from convention-based discovery, not just explicit attributes. This means it works for the majority of Needlr users who rely on automatic lifetime inference.

### Lifetime Violations Detected

| Consumer Lifetime | Dependency Lifetime | Violation? |
|-------------------|---------------------|------------|
| Singleton | Scoped | ✅ Yes |
| Singleton | Transient | ✅ Yes |
| Scoped | Transient | ✅ Yes |
| Singleton | Singleton | ❌ No |
| Scoped | Scoped | ❌ No |
| Scoped | Singleton | ❌ No |
| Transient | Any | ❌ No |

### Related Diagnostic

[NDLRCOR012](NDLRCOR012.md) is a standalone Roslyn analyzer that performs the same check but **only** when both types have explicit lifetime attributes (`[Singleton]`, `[Scoped]`, `[Transient]`). NDLRGEN022 is more comprehensive because it uses Needlr's inferred lifetimes.

## Example

### ❌ Violation

```csharp
[Scoped]
public class DbContext : IDisposable
{
    public void Dispose() { }
}

[Singleton]
public class CacheService
{
    private readonly DbContext _context;
    
    // NDLRGEN022: CacheService (Singleton) depends on DbContext (Scoped)
    // which implements IDisposable
    public CacheService(DbContext context) => _context = context;
}
```

### ✅ Fix: Use Factory Pattern

```csharp
[Singleton]
public class CacheService
{
    private readonly Func<DbContext> _contextFactory;
    
    public CacheService(Func<DbContext> contextFactory) => _contextFactory = contextFactory;
    
    public void DoWork()
    {
        using var context = _contextFactory();
        // Use context within its proper scope
    }
}
```

### ✅ Fix: Use IServiceScopeFactory

```csharp
[Singleton]
public class CacheService
{
    private readonly IServiceScopeFactory _scopeFactory;
    
    public CacheService(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;
    
    public void DoWork()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbContext>();
        // Use context within its proper scope
    }
}
```

## Safe Patterns (Not Flagged)

The diagnostic recognizes these factory patterns and does **not** flag them:

- `Func<T>` - Creates new instances on demand
- `Lazy<T>` - Deferred creation
- `IServiceScopeFactory` - Creates new scopes
- `IServiceProvider` - Dynamic service resolution

## How Lifetimes Are Determined

NDLRGEN022 uses Needlr's convention-based lifetime inference:

1. **Explicit attributes** (`[Singleton]`, `[Scoped]`, `[Transient]`) take precedence
2. **Hosted services** (`BackgroundService`, `IHostedService`) are always Singleton
3. **Injectable types** with parameterless or all-injectable constructors default to Singleton

For detailed information about lifetime inference, see the [Needlr documentation](https://github.com/nexus-labs/needlr).

## Suppression

To suppress this diagnostic:

```csharp
#pragma warning disable NDLRGEN022
[Singleton]
public class KnownCaptiveDependency
{
    public KnownCaptiveDependency(ScopedDisposable dep) { }
}
#pragma warning restore NDLRGEN022
```

## See Also

- [NDLRCOR012](NDLRCOR012.md) - Standalone analyzer for explicit attributes only
- [Captive Dependency anti-pattern](https://blog.ploeh.dk/2014/06/02/captive-dependency/)
