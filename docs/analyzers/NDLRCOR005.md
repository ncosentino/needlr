# NDLRCOR005: Lifetime mismatch - longer-lived service depends on shorter-lived service

## Cause

A service with a longer lifetime (e.g., Singleton) has a constructor dependency on a service with a shorter lifetime (e.g., Scoped or Transient). This creates a "captive dependency" where the shorter-lived service is captured and held beyond its intended lifetime.

## Rule Description

In dependency injection, services have three standard lifetimes:

| Lifetime | Rank | Description |
|----------|------|-------------|
| **Transient** | 0 | New instance per request |
| **Scoped** | 1 | One instance per scope (e.g., per HTTP request) |
| **Singleton** | 2 | Single instance for application lifetime |

A **lifetime mismatch** occurs when a longer-lived service depends on a shorter-lived one:

```csharp
// ⚠️ NDLRCOR005: Singleton depends on Scoped
[Singleton]
public class CacheService(IUserContext userContext)  // IUserContext is Scoped
{
    private readonly IUserContext _userContext = userContext;
    
    public string GetCacheKey() => $"user_{_userContext.UserId}";
}

[Scoped]
public class UserContext : IUserContext
{
    public int UserId { get; set; }
}
```

**Why this is dangerous:**

1. **Stale data**: The Singleton captures the first `IUserContext` instance and uses it for ALL requests, even though `UserContext` was meant to change per-request.

2. **Memory leaks**: Scoped services are designed to be disposed at scope end. A Singleton holding a reference prevents garbage collection.

3. **Concurrency bugs**: Scoped services often aren't thread-safe because they're designed for single-request use. A Singleton may use them from multiple threads simultaneously.

4. **Silent failures**: No runtime exception occurs—the application works but produces incorrect results.

## Common Mismatch Patterns

### Singleton → Scoped (Most Dangerous)

```csharp
[Singleton]
public class EmailService(IDbContext dbContext) { }  // DbContext is Scoped!
```

### Singleton → Transient

```csharp
[Singleton]
public class LoggingService(ITimeProvider time) { }  // May capture stale time
```

### Scoped → Transient

```csharp
[Scoped]
public class RequestHandler(IValidator validator) { }  // Less dangerous but still problematic
```

## How to Fix

### Option 1: Match Lifetimes (Recommended)

Make the consumer's lifetime equal or shorter than its dependencies:

```csharp
// ✅ Scoped depends on Scoped - OK!
[Scoped]
public class CacheService(IUserContext userContext) { }
```

### Option 2: Use Factory Pattern

Inject a factory that creates fresh instances:

```csharp
[Singleton]
public class CacheService(IServiceScopeFactory scopeFactory)
{
    public string GetCacheKey()
    {
        using var scope = scopeFactory.CreateScope();
        var userContext = scope.ServiceProvider.GetRequiredService<IUserContext>();
        return $"user_{userContext.UserId}";
    }
}
```

### Option 3: Use Func<T> or Lazy<T>

Configure a factory delegate:

```csharp
[Singleton]
public class CacheService(Func<IUserContext> userContextFactory)
{
    public string GetCacheKey()
    {
        var userContext = userContextFactory();  // Fresh instance each call
        return $"user_{userContext.UserId}";
    }
}
```

### Option 4: Redesign the Dependency

Sometimes the design needs rethinking. If a Singleton truly needs request-specific data, consider:


- Passing the data as a method parameter
- Using `IHttpContextAccessor` (for ASP.NET Core)
- Using ambient context patterns (with caution)

## Runtime Detection

This analyzer detects mismatches at compile-time for types with Needlr lifetime attributes. For additional runtime validation:

```csharp
// Verify at startup
services.Verify(VerificationOptions.Strict);

// Or get detailed diagnostics
var result = services.VerifyWithDiagnostics();
if (!result.IsValid)
{
    Console.WriteLine(result.ToDetailedReport());
}
```

## Detection Limitations

This analyzer can only detect mismatches when:

1. Both the consumer and dependency have explicit lifetime attributes (`[Singleton]`, `[Scoped]`, `[Transient]`, or `[RegisterAs]`)
2. The dependency type is a concrete class (not an interface)

For interface dependencies, use the runtime `Verify()` method which has access to the full service collection.

## When to Suppress

Suppress this warning only if you:

1. Understand the implications and have mitigated them
2. The dependency is thread-safe and stateless
3. The dependency is intentionally shared across scopes

```csharp
[Singleton]
#pragma warning disable NDLRCOR005
public class MetricsService(ICounter counter) { }  // ICounter is thread-safe
#pragma warning restore NDLRCOR005
```

## See Also

- [Advanced Usage - Container Verification](../advanced-usage.md#container-verification)
- [NDLRCOR006: Circular dependency detected](NDLRCOR006.md)
- [Microsoft Docs: Service Lifetimes](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection#service-lifetimes)
