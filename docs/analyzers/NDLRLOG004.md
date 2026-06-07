# NDLRLOG004: Containing type must be partial

## Cause

A method marked with `[NeedlrLoggerMessage]` lives in a type that is not declared `partial`.

## Rule Description

The generator adds the method's implementation to the containing type via a `partial` declaration. Every
type in the nesting chain that contains the logging method must therefore be `partial`. The C# compiler
also requires this for partial methods (CS0751), but this diagnostic gives feature-specific guidance —
especially useful alongside [NDLRLOG001](NDLRLOG001.md).

## How to Fix

```csharp
// WRONG: the containing type is not partial
public class OrderService
{
    private readonly ILogger _logger;

    [NeedlrLoggerMessage(Level = LogLevel.Warning, Message = "Failed {OrderId}")]
    public partial void LogFailed(int orderId, Exception error);
}

// CORRECT
public partial class OrderServiceFixed
{
    private readonly ILogger _logger;

    [NeedlrLoggerMessage(Level = LogLevel.Warning, Message = "Failed {OrderId}")]
    public partial void LogFailed(int orderId, Exception error);
}
```

## See Also

- [Cancellation-Aware Logging](../cancellation-aware-logging.md)
