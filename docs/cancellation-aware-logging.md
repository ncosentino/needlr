# Cancellation-Aware Logging

`NexusLabs.Needlr.Logging` adds the `[NeedlrLoggerMessage]` attribute: source-generated logging
methods with the same ergonomics and performance as .NET's built-in `[LoggerMessage]`, plus one extra
behavior — **logs whose exception argument is a cancellation are skipped by default**.

Cancellation (`OperationCanceledException` / `TaskCanceledException`) is normal control flow in
production code. Logging it as a warning or error is usually noise. `[NeedlrLoggerMessage]` removes
that noise without you writing a guard at every call site, while remaining a near drop-in replacement
for `[LoggerMessage]`.

!!! info "How it relates to `[LoggerMessage]`"
    Roslyn source generators cannot see each other's output, so Needlr cannot extend Microsoft's
    generator directly. Instead, the generated body reuses the public `LoggerMessage.Define` runtime
    primitive — the exact fast path Microsoft's generator targets — and wraps it with the cancellation
    guard. There is no performance penalty for the common case.

## Installation

```xml
<PackageReference Include="NexusLabs.Needlr.Logging" Version="..." />
```

The package brings the `[NeedlrLoggerMessage]` attribute, the generator, the analyzer, and the
`NeedlrCancellationLogging` policy class.

## Quick Start

```csharp
using Microsoft.Extensions.Logging;
using NexusLabs.Needlr.Logging;

public partial class OrderService
{
    private readonly ILogger<OrderService> _logger;

    public OrderService(ILogger<OrderService> logger) => _logger = logger;

    // If 'error' is an OperationCanceledException, nothing is logged (by default).
    [NeedlrLoggerMessage(EventId = 42, Level = LogLevel.Warning, Message = "Order {OrderId} failed")]
    public partial void LogOrderFailed(int orderId, Exception error);
}
```

```csharp
try
{
    await ProcessAsync(orderId, cancellationToken);
}
catch (Exception ex)
{
    // Real failures are logged at Warning; cancellations are silently skipped.
    service.LogOrderFailed(orderId, ex);
    throw;
}
```

## Migrating from `[LoggerMessage]`

The attribute surface mirrors `Microsoft.Extensions.Logging.LoggerMessageAttribute`, so migration is
usually just a namespace and attribute-name change:

```diff
- using Microsoft.Extensions.Logging;
+ using Microsoft.Extensions.Logging;
+ using NexusLabs.Needlr.Logging;

- [LoggerMessage(EventId = 42, Level = LogLevel.Warning, Message = "Order {OrderId} failed")]
+ [NeedlrLoggerMessage(EventId = 42, Level = LogLevel.Warning, Message = "Order {OrderId} failed")]
  public partial void LogOrderFailed(int orderId, Exception error);
```

## How the logger is resolved

Like `[LoggerMessage]`:

- **Instance methods** use an `ILogger` (or `ILogger<T>`) **field or property** on the containing type.
- **Static methods** take an `ILogger` **parameter**.

```csharp
public static partial class Log
{
    [NeedlrLoggerMessage(Level = LogLevel.Error, Message = "Boom {Code}")]
    public static partial void Boom(ILogger logger, int code, Exception error);
}
```

## The exception parameter

The **first parameter assignable to `System.Exception`** is treated as the exception. It is passed to
the underlying log call's exception slot (not bound to a message placeholder) and is the value the
cancellation guard inspects. A method with no exception parameter behaves exactly like a plain
`[LoggerMessage]` method — no guard is generated.

## Controlling the cancellation behavior

By default, a cancellation exception causes the log to be **skipped entirely**. You can change this
globally through the static `NeedlrCancellationLogging` policy — typically once, at startup.

```csharp
using NexusLabs.Needlr.Logging;

// Turn the feature off (log cancellations normally):
NeedlrCancellationLogging.Behavior = CancellationLoggingBehavior.Log;

// Or demote cancellations to a quieter level instead of skipping them:
NeedlrCancellationLogging.Behavior = CancellationLoggingBehavior.Demote;
NeedlrCancellationLogging.DemotedLevel = LogLevel.Debug;
```

| Behavior | Effect |
|----------|--------|
| `Skip` (default) | The log entry is suppressed entirely. |
| `Log` | The entry is logged at its declared level, as if the exception were not a cancellation. |
| `Demote` | The entry is logged at `DemotedLevel` instead of its declared level. |

### Customizing what counts as a cancellation

```csharp
// Treat your own cooperative-cancellation exception as a cancellation too.
NeedlrCancellationLogging.IsCancellationPredicate =
    ex => ex is OperationCanceledException or MyCooperativeShutdownException;
```

### Environment-variable defaults

The initial `Behavior` and `DemotedLevel` are read **once**, lazily, from environment variables and
then cached for the lifetime of the process. This lets you flip behavior per environment without code
changes. Explicitly assigning a property overrides the environment-derived default.

| Variable | Values | Default |
|----------|--------|---------|
| `__NEEDLR_CANCELLATION_LOGGING_BEHAVIOR` | `skip`, `log`, `demote` (case-insensitive) | `skip` |
| `__NEEDLR_CANCELLATION_LOGGING_DEMOTED_LEVEL` | any `LogLevel` name | `Debug` |

## Parameter limit

`LoggerMessage.Define` supports at most **six** message parameters (excluding the logger and the
exception). Methods with more parameters still work, but fall back to a slower logging call and the
analyzer reports [NDLRLOG006](analyzers/NDLRLOG006.md). Keep the parameter count at six or fewer to
stay on the allocation-free fast path.

## Attribute Reference

`[NeedlrLoggerMessage]` (`NexusLabs.Needlr.Logging`)

| Member | Type | Description |
|--------|------|-------------|
| `EventId` | `int` | The numeric event id. Defaults to `0`. |
| `EventName` | `string?` | The event name. Defaults to the method name. |
| `Level` | `LogLevel` | The level at which the message is logged. Defaults to `Information`. |
| `Message` | `string` | The structured message template (e.g. `"Order {OrderId} failed"`). |
| `SkipEnabledCheck` | `bool` | Omit the `IsEnabled` guard. Defaults to `false`. |

Constructors mirror `LoggerMessageAttribute`: parameterless, `(string message)`,
`(LogLevel level, string message)`, and `(int eventId, LogLevel level, string message)`.

## Analyzers

| Rule | Severity | Description |
|------|----------|-------------|
| [NDLRLOG001](analyzers/NDLRLOG001.md) | Error | Method must be partial |
| [NDLRLOG002](analyzers/NDLRLOG002.md) | Error | Method must return void |
| [NDLRLOG003](analyzers/NDLRLOG003.md) | Error | Method must not be generic |
| [NDLRLOG004](analyzers/NDLRLOG004.md) | Error | Containing type must be partial |
| [NDLRLOG005](analyzers/NDLRLOG005.md) | Error | No accessible `ILogger` |
| [NDLRLOG006](analyzers/NDLRLOG006.md) | Info | More than six non-exception parameters |
