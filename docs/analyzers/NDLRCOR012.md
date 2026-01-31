# NDLRCOR012: Disposable captive dependency

## Cause

A service with a longer lifetime holds a reference to an `IDisposable` or `IAsyncDisposable` dependency with a shorter lifetime. When the shorter-lived scope ends, the dependency will be disposed while the longer-lived service still holds a reference, causing `ObjectDisposedException` at runtime.

## Rule Description

This is a more severe form of captive dependency (see [NDLRCOR005](NDLRCOR005.md)) that specifically targets disposable services. While a general captive dependency causes stale data, a **disposable captive dependency** causes runtime crashes.

```csharp
// ❌ NDLRCOR012: Singleton holds Scoped IDisposable
[Singleton]
public class CacheService
{
    private readonly MyDbContext _dbContext;
    
    public CacheService(MyDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async Task RefreshCache()
    {
        // 💥 ObjectDisposedException - dbContext was disposed when scope ended!
        var data = await _dbContext.Items.ToListAsync();
    }
}

[Scoped]
public class MyDbContext : DbContext, IDisposable
{
    // ...
}
```

**Why this is dangerous:**

1. **Runtime exceptions**: Accessing a disposed object throws `ObjectDisposedException`
2. **Unpredictable timing**: The exception may occur long after the service was created
3. **Hard to debug**: The stack trace points to the usage site, not the registration problem

## Mismatch Patterns That Trigger This Error

| Consumer | Dependency | Result |
|----------|------------|--------|
| Singleton | Scoped IDisposable | ❌ Error |
| Singleton | Transient IDisposable | ❌ Error |
| Scoped | Transient IDisposable | ❌ Error |

## How to Fix

### Option 1: Match Lifetimes

Make the consumer's lifetime equal or shorter than its dependencies:

```csharp
// ✅ Scoped depends on Scoped - OK!
[Scoped]
public class CacheService(MyDbContext dbContext) { }
```

### Option 2: Use IServiceScopeFactory

Create a new scope when you need the disposable service:

```csharp
[Singleton]
public class CacheService
{
    private readonly IServiceScopeFactory _scopeFactory;
    
    public CacheService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }
    
    public async Task RefreshCache()
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MyDbContext>();
        var data = await dbContext.Items.ToListAsync();
        // dbContext is disposed when scope ends - that's fine, we're done with it
    }
}
```

### Option 3: Use Func<T> Factory

Inject a factory that creates fresh instances:

```csharp
[Singleton]
public class CacheService(Func<MyDbContext> dbContextFactory)
{
    public async Task RefreshCache()
    {
        using var dbContext = dbContextFactory();
        var data = await dbContext.Items.ToListAsync();
    }
}
```

## Detection Limitations

This analyzer only fires when:

1. **Both types have explicit lifetime attributes** (`[Singleton]`, `[Scoped]`, `[Transient]`)
2. **The dependency is a concrete class** (not an interface)
3. **The dependency directly implements `IDisposable` or `IAsyncDisposable`**

This conservative approach ensures **zero false positives** but may miss some cases (false negatives are acceptable).

### Not Detected (by design)

```csharp
// Interface dependency - can't determine concrete type
[Singleton]
public class Service(IDbContext dbContext) { }  // No error (might not be disposable)

// No explicit lifetime attributes
public class Service(ScopedDisposable dep) { }  // No error (lifetime unknown)
```

## When to Suppress

You should rarely suppress this error. Consider suppressing only if:

1. You've implemented proper disposal handling manually
2. The disposable's `Dispose()` is a no-op or the object remains usable after disposal

```csharp
[Singleton]
#pragma warning disable NDLRCOR012
public class Service(PooledConnection connection) { }  // Connection returns to pool
#pragma warning restore NDLRCOR012
```

## See Also

- [NDLRCOR005: Lifetime mismatch](NDLRCOR005.md) - General captive dependency warning
- [NDLRCOR006: Circular dependency detected](NDLRCOR006.md)
