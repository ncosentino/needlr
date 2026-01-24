# NDLRCOR007: Intercept type must implement IMethodInterceptor

## Cause

A type used in an `[Intercept]` or `[Intercept<T>]` attribute does not implement the `IMethodInterceptor` interface.

## Rule Description

When using the `[Intercept]` attribute to apply an interceptor to a service, the specified type must implement `IMethodInterceptor`. This interface defines the `InterceptAsync` method that is called when intercepted methods are invoked.

```csharp
// ❌ NDLRCOR007: MyLogger does not implement IMethodInterceptor
[Intercept<MyLogger>]
public class OrderService : IOrderService
{
    public Order GetOrder(int id) => /* ... */;
}

public class MyLogger  // Missing IMethodInterceptor implementation
{
    public void Log(string message) => Console.WriteLine(message);
}
```

## How to Fix

Implement `IMethodInterceptor` on the interceptor class:

```csharp
// ✅ Fixed: LoggingInterceptor implements IMethodInterceptor
[Intercept<LoggingInterceptor>]
public class OrderService : IOrderService
{
    public Order GetOrder(int id) => /* ... */;
}

public class LoggingInterceptor : IMethodInterceptor
{
    public async ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
    {
        Console.WriteLine($"Calling {invocation.Method.Name}");
        return await invocation.ProceedAsync();
    }
}
```

## IMethodInterceptor Interface

```csharp
public interface IMethodInterceptor
{
    ValueTask<object?> InterceptAsync(IMethodInvocation invocation);
}
```

The interface requires a single method:
- `InterceptAsync`: Called when an intercepted method is invoked. Call `invocation.ProceedAsync()` to continue to the next interceptor or the actual method.

## When to Suppress

Do not suppress this diagnostic. Using a type that doesn't implement `IMethodInterceptor` will cause a compilation error in the generated code.

## See Also

- [Interceptors](../interceptors.md) - Full interceptor documentation
- [IMethodInterceptor](../interceptors.md#imethodinvocation-interface) - Interface details
