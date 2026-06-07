# NDLRLOG006: More than six non-exception parameters

## Cause

A method marked with `[NeedlrLoggerMessage]` has more than six message parameters (parameters other than
the logger and the exception).

## Rule Description

The allocation-free fast path is built on `LoggerMessage.Define`, which supports at most six message
parameters. When a method exceeds that limit, the generator still produces a correct implementation, but
it falls back to a slower `LoggerExtensions.Log` call that allocates and boxes its arguments. This
diagnostic is informational — the code works, but you lose the performance benefit.

## How to Fix

Reduce the number of parameters to six or fewer, for example by grouping related values into a single
structured argument.

```csharp
// INFO: seven message parameters fall back to the slower path
[NeedlrLoggerMessage(Level = LogLevel.Information, Message = "{A}{B}{C}{D}{E}{F}{G}")]
public partial void LogManyWrong(int a, int b, int c, int d, int e, int f, int g);

// CORRECT: six or fewer keeps the allocation-free fast path
[NeedlrLoggerMessage(Level = LogLevel.Information, Message = "Processed {Batch}")]
public partial void LogBatch(BatchSummary batch);
```

## When to Suppress

Suppress this diagnostic if the extra parameters are intentional and the small allocation cost is
acceptable for the call site.

## See Also

- [Cancellation-Aware Logging](../cancellation-aware-logging.md)
