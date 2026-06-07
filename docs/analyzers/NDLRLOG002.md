# NDLRLOG002: Method must return void

## Cause

A method marked with `[NeedlrLoggerMessage]` returns something other than `void`.

## Rule Description

A logging method records an event; it has no result to return. Like the built-in `[LoggerMessage]`,
`[NeedlrLoggerMessage]` only supports methods that return `void`.

## How to Fix

```csharp
public partial class OrderService
{
    private readonly ILogger _logger;

    // WRONG: returns a value
    [NeedlrLoggerMessage(Level = LogLevel.Warning, Message = "Failed {OrderId}")]
    public partial bool LogFailedWrong(int orderId, Exception error);

    // CORRECT
    [NeedlrLoggerMessage(Level = LogLevel.Warning, Message = "Failed {OrderId}")]
    public partial void LogFailed(int orderId, Exception error);
}
```

## See Also

- [Cancellation-Aware Logging](../cancellation-aware-logging.md)
