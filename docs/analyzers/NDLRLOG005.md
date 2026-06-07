# NDLRLOG005: No accessible ILogger

## Cause

A method marked with `[NeedlrLoggerMessage]` has no logger to use.

## Rule Description

The generated body needs an `ILogger` (or `ILogger<T>`) to write to:

- An **instance** method must have an `ILogger` **field or property** on its containing type (or a base type).
- A **static** method must take an `ILogger` **parameter**.

When neither is present, the generator cannot emit a working body.

## How to Fix

```csharp
public partial class OrderService
{
    // WRONG: no ILogger field, so an instance logging method has nothing to write to
    [NeedlrLoggerMessage(Level = LogLevel.Warning, Message = "Failed {OrderId}")]
    public partial void LogFailedWrong(int orderId, Exception error);
}

public partial class OrderServiceFixed
{
    private readonly ILogger _logger;          // CORRECT: instance method uses this field

    public OrderServiceFixed(ILogger<OrderServiceFixed> logger) => _logger = logger;

    [NeedlrLoggerMessage(Level = LogLevel.Warning, Message = "Failed {OrderId}")]
    public partial void LogFailed(int orderId, Exception error);
}

public static partial class Log
{
    // CORRECT: static method takes an ILogger parameter
    [NeedlrLoggerMessage(Level = LogLevel.Warning, Message = "Failed {OrderId}")]
    public static partial void Failed(ILogger logger, int orderId, Exception error);
}
```

## See Also

- [Cancellation-Aware Logging](../cancellation-aware-logging.md)
