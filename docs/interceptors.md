# Interceptors

Interceptors provide a powerful way to add cross-cutting concerns to your services without modifying their implementation. Unlike decorators, which require implementing every method of an interface, interceptors handle method invocations with a single implementation that works across any service.

> **Note**: Interceptors are a **source-generation only** feature. They are not available with reflection-based registration.

## Quick Start

### 1. Create an Interceptor

```csharp
using NexusLabs.Needlr;

public class LoggingInterceptor : IMethodInterceptor
{
    private readonly ILogger _logger;
    
    public LoggingInterceptor(ILogger<LoggingInterceptor> logger)
    {
        _logger = logger;
    }
    
    public async ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
    {
        _logger.LogInformation("Calling {Method}", invocation.Method.Name);
        
        try
        {
            var result = await invocation.ProceedAsync();
            _logger.LogInformation("{Method} completed", invocation.Method.Name);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Method} failed", invocation.Method.Name);
            throw;
        }
    }
}
```

### 2. Apply to a Service

```csharp
[Intercept<LoggingInterceptor>]
public class OrderService : IOrderService
{
    public Order GetOrder(int id) => /* ... */;
    public Task<Order> CreateOrderAsync(CreateOrderRequest request) => /* ... */;
    public void CancelOrder(int orderId) => /* ... */;
}
```

### 3. Use the Service

```csharp
var provider = new Syringe()
    .UsingSourceGen()
    .BuildServiceProvider();

var orderService = provider.GetRequiredService<IOrderService>();
orderService.GetOrder(123);  // Logging interceptor wraps this call
```

## Interceptors vs Decorators

| Feature | Decorator | Interceptor |
|---------|-----------|-------------|
| Implementation | One class per service | One class for any service |
| Method handling | Implement every interface method | Single `InterceptAsync` handles all |
| Apply to | One service type | Any service with `[Intercept]` |
| Best for | Service-specific behavior | Cross-cutting concerns |
| Reflection support | Yes | No (source-gen only) |

### When to Use Each

**Use Decorators when:**
- You need different behavior for different methods
- You're modifying only one service type
- You need reflection support

**Use Interceptors when:**
- The same logic applies to all methods (logging, timing, caching)
- You want to reuse the same interceptor across many services
- You're using source generation

## Class-Level vs Method-Level Interceptors

### Class-Level (All Methods)

Apply to all methods of a service:

```csharp
[Intercept<TimingInterceptor>]
[Intercept<LoggingInterceptor>]
public class ProductService : IProductService
{
    public Product GetProduct(int id) => /* ... */;     // Both interceptors run
    public void UpdateProduct(Product p) => /* ... */;  // Both interceptors run
}
```

### Method-Level (Selective)

Apply to specific methods only:

```csharp
public class CalculatorService : ICalculatorService
{
    public int Add(int a, int b) => a + b;  // No interceptor (direct call)
    
    [Intercept<TimingInterceptor>]
    public int Multiply(int a, int b)       // Only TimingInterceptor
    {
        Thread.Sleep(100);  // Expensive operation
        return a * b;
    }
    
    [Intercept<TimingInterceptor>]
    [Intercept<CachingInterceptor>]
    public int Divide(int a, int b)         // Both interceptors
    {
        return a / b;
    }
}
```

## Interceptor Ordering

When multiple interceptors are applied, use the `Order` property to control execution order:

```csharp
[Intercept<TimingInterceptor>(Order = 1)]    // Runs first (outermost)
[Intercept<CachingInterceptor>(Order = 2)]   // Runs second (inner)
public class DataService : IDataService { }
```

**Execution flow:**
```
TimingInterceptor.InterceptAsync
    → CachingInterceptor.InterceptAsync
        → DataService.GetData (actual method)
    ← CachingInterceptor returns
← TimingInterceptor returns
```

Lower `Order` values run first and are the outermost wrapper.

## IMethodInvocation Interface

The `IMethodInvocation` interface provides context about the intercepted call:

```csharp
public interface IMethodInvocation
{
    object Target { get; }           // The service instance
    MethodInfo Method { get; }       // Method being called
    object?[] Arguments { get; }     // Arguments (can be modified)
    Type[] GenericArguments { get; } // Generic type arguments (if any)
    
    ValueTask<object?> ProceedAsync();  // Continue to next interceptor or target
}
```

### Accessing Method Information

```csharp
public async ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
{
    // Get the service type
    var serviceType = invocation.Target.GetType().Name;
    
    // Get method name
    var methodName = invocation.Method.Name;
    
    // Get arguments
    var args = string.Join(", ", invocation.Arguments);
    
    Console.WriteLine($"Calling {serviceType}.{methodName}({args})");
    
    return await invocation.ProceedAsync();
}
```

### Modifying Arguments

```csharp
public async ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
{
    // Modify the first argument before calling
    if (invocation.Arguments.Length > 0 && invocation.Arguments[0] is string s)
    {
        invocation.Arguments[0] = s.ToUpperInvariant();
    }
    
    return await invocation.ProceedAsync();
}
```

### Modifying Return Values

```csharp
public async ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
{
    var result = await invocation.ProceedAsync();
    
    // Wrap the result
    if (result is string s)
    {
        return $"[Modified] {s}";
    }
    
    return result;
}
```

## Common Interceptor Patterns

### Timing Interceptor

```csharp
public class TimingInterceptor : IMethodInterceptor
{
    private readonly ILogger _logger;
    
    public TimingInterceptor(ILogger<TimingInterceptor> logger) => _logger = logger;
    
    public async ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
    {
        var sw = Stopwatch.StartNew();
        
        try
        {
            return await invocation.ProceedAsync();
        }
        finally
        {
            _logger.LogInformation(
                "{Type}.{Method} completed in {Elapsed}ms",
                invocation.Target.GetType().Name,
                invocation.Method.Name,
                sw.ElapsedMilliseconds);
        }
    }
}
```

### Caching Interceptor

```csharp
public class CachingInterceptor : IMethodInterceptor
{
    private readonly ConcurrentDictionary<string, object?> _cache = new();
    
    public async ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
    {
        var cacheKey = BuildCacheKey(invocation);
        
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }
        
        var result = await invocation.ProceedAsync();
        _cache[cacheKey] = result;
        return result;
    }
    
    private static string BuildCacheKey(IMethodInvocation invocation)
    {
        var args = string.Join(",", invocation.Arguments.Select(a => a?.ToString() ?? "null"));
        return $"{invocation.Target.GetType().Name}.{invocation.Method.Name}({args})";
    }
}
```

### Retry Interceptor

```csharp
public class RetryInterceptor : IMethodInterceptor
{
    private readonly ILogger _logger;
    
    public RetryInterceptor(ILogger<RetryInterceptor> logger) => _logger = logger;
    
    public async ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
    {
        const int maxRetries = 3;
        
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await invocation.ProceedAsync();
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(ex, 
                    "Attempt {Attempt}/{Max} failed for {Method}, retrying...",
                    attempt, maxRetries, invocation.Method.Name);
                    
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }
        }
        
        throw new InvalidOperationException("Should not reach here");
    }
}
```

### Validation Interceptor

```csharp
public class ValidationInterceptor : IMethodInterceptor
{
    public async ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
    {
        // Validate all arguments
        foreach (var arg in invocation.Arguments)
        {
            if (arg is null)
            {
                throw new ArgumentNullException(
                    $"Null argument passed to {invocation.Method.Name}");
            }
        }
        
        return await invocation.ProceedAsync();
    }
}
```

## Interceptor Dependencies

Interceptors are registered as services and can have their own dependencies:

```csharp
public class AuditInterceptor : IMethodInterceptor
{
    private readonly IAuditService _audit;
    private readonly ICurrentUser _user;
    
    public AuditInterceptor(IAuditService audit, ICurrentUser user)
    {
        _audit = audit;
        _user = user;
    }
    
    public async ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
    {
        await _audit.LogAsync(new AuditEntry
        {
            User = _user.Id,
            Action = invocation.Method.Name,
            Timestamp = DateTime.UtcNow
        });
        
        return await invocation.ProceedAsync();
    }
}
```

## Auto-Registration Exclusion

Interceptors are automatically excluded from Needlr's auto-registration because they implement `IMethodInterceptor`, which is marked with `[DoNotAutoRegister]`. This is by design—interceptors are resolved by the generated proxy, not by user code.

You do not need to add `[DoNotAutoRegister]` to your interceptor classes.

## Limitations

1. **Source-generation only**: Interceptors are not available with reflection-based registration.

2. **Interface-based**: The intercepted service must implement at least one interface. The proxy is registered for the interface, not the concrete type.

3. **Generic methods**: Generic methods are not currently supported for interception.

4. **Async overhead**: All interception goes through `ValueTask<object?>`, so there's boxing overhead for value types and sync methods pay the async state machine cost.

## Analyzers

Needlr provides analyzers to catch common interceptor mistakes at compile time:

| Rule ID | Severity | Description |
|---------|----------|-------------|
| [NDLRCOR007](analyzers/NDLRCOR007.md) | Error | Intercept type must implement IMethodInterceptor |
| [NDLRCOR008](analyzers/NDLRCOR008.md) | Warning | [Intercept] applied to class without interfaces |

## See Also

- [Decorators](advanced-usage.md#decorators) - For service-specific wrapping
- [Getting Started](getting-started.md) - Source generation setup
- [Example: AotSourceGenConsoleApp](../src/Examples/SourceGen/AotSourceGenConsoleApp) - Working interceptor examples
