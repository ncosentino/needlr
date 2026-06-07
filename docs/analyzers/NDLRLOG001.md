# NDLRLOG001: Method must be partial

## Cause

A method marked with `[NeedlrLoggerMessage]` is not declared `partial`.

## Rule Description

The source generator supplies the body of a `[NeedlrLoggerMessage]` method. That is only possible when
the method is `partial` and has no implementation of its own. A non-partial method (or a partial method
you have already implemented) leaves nothing for the generator to fill in.

## How to Fix

```csharp
public partial class OrderService
{
    private readonly ILogger _logger;

    // WRONG: not partial, so the generator cannot supply the body
    [NeedlrLoggerMessage(Level = LogLevel.Warning, Message = "Failed {OrderId}")]
    public void LogFailedWrong(int orderId, Exception error) { }

    // CORRECT
    [NeedlrLoggerMessage(Level = LogLevel.Warning, Message = "Failed {OrderId}")]
    public partial void LogFailed(int orderId, Exception error);
}
```

## See Also

- [Cancellation-Aware Logging](../cancellation-aware-logging.md)
