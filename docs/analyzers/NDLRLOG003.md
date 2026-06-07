# NDLRLOG003: Method must not be generic

## Cause

A method marked with `[NeedlrLoggerMessage]` declares type parameters.

## Rule Description

The generated body builds a cached `LoggerMessage.Define` delegate whose concrete parameter types must
be known at generation time. Generic logging methods are not supported, matching the built-in
`[LoggerMessage]` generator.

## How to Fix

```csharp
public partial class Cache
{
    private readonly ILogger _logger;

    // WRONG: generic logging method
    [NeedlrLoggerMessage(Level = LogLevel.Information, Message = "Stored {Key}")]
    public partial void LogStoredWrong<TKey>(TKey key);

    // CORRECT: use concrete parameter types
    [NeedlrLoggerMessage(Level = LogLevel.Information, Message = "Stored {Key}")]
    public partial void LogStored(string key);
}
```

## See Also

- [Cancellation-Aware Logging](../cancellation-aware-logging.md)
