---
applyTo: "**/*.cs"
---

# Structured Logging Rules

## Source-Generated Logging (required for new code)

Never use `_logger.LogInformation("message {Param}", param)` directly in production code. This allocates strings and boxes value types on every call, even when the log level is disabled.

Use `[LoggerMessage]` source-generated methods instead:

```csharp
// WRONG — allocates on every call
_logger.LogInformation("Processing content {ContentId} on {Platform}", contentId, platform);

// CORRECT — zero allocation when level is disabled, compile-time generated
_log.ProcessingContent(contentId, platform);
```

## Dedicated Logger Classes

When a feature has **4 or more** log methods, extract them into a dedicated logger class. When 3 or fewer, inline `[LoggerMessage]` on the class itself is acceptable.

### Pattern

```csharp
internal sealed partial class PostingLog(ILogger logger)
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Post job started for {ContentId}")]
    public partial void JobStarted(string contentId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Post published to {Platform}")]
    public partial void Published(string platform);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Post failed on {Platform}: {Reason}")]
    public partial void PostFailed(string platform, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rate limited by {Platform}, retrying in {DelayMs}ms")]
    public partial void RateLimited(string platform, long delayMs);
}
```

### Consuming the logger

```csharp
internal sealed class PostJobUnitOfWork(
    ILogger<PostJobUnitOfWork> _logger,
    IPostRepository _repo)
{
    private readonly PostingLog _log = new(_logger);

    public async Task ExecuteAsync(ContentId contentId, ...)
    {
        _log.JobStarted(contentId.ToString());
        // ...
        _log.Published("Twitter");
    }
}
```

### Naming and location

- Name: `{Feature}Log` (e.g., `PostingLog`, `TopicStreamLog`, `AuthLog`)
- Lives in the same namespace and project as the feature it serves
- One logger class per logical feature area — not per consuming class

### Inline pattern (3 or fewer log methods)

When a class only has a few log calls, define them directly on the class:

```csharp
internal sealed partial class MyService(ILogger<MyService> _logger)
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Operation completed for {ItemId}")]
    private partial void LogOperationCompleted(string itemId);

    public void DoWork(string itemId)
    {
        // ...
        LogOperationCompleted(itemId);
    }
}
```

## Context Enrichment via BeginScope

At entry points (handlers, consumers, jobs, UnitOfWork methods), push key identifiers onto the log scope so downstream code doesn't need to pass them into every log call:

```csharp
public async Task ExecuteAsync(ContentId contentId, SocialAccountId accountId, ...)
{
    using (_logger.BeginScope(new Dictionary<string, object>
    {
        ["ContentId"] = contentId.ToString(),
        ["SocialAccountId"] = accountId.ToString(),
    }))
    {
        // Everything logged inside this block automatically includes
        // ContentId and SocialAccountId as structured properties.
        // Log methods don't need these as parameters.
        _log.JobStarted();
        await _repo.SaveAsync(...);
        _log.Published("Twitter");
    }
}
```

Serilog automatically includes scope properties in every log entry within the block. These appear as structured fields in Loki/Grafana, queryable via `{ContentId="123"}`.

**Where to use BeginScope:**
- Carter module handlers (push request identifiers)
- Consumer `Consume()` methods (push message identifiers)
- Job `ExecuteWithProgressAsync` methods (push job parameters)
- UnitOfWork entry methods (push business entity identifiers)

**Do not** nest BeginScope calls deeply — one scope at the entry point is sufficient. Downstream services and repositories inherit the scope automatically.

## Log Levels

| Level | Use when |
|-------|----------|
| `LogDebug` | Diagnostic detail: query results, cache hits, internal state |
| `LogInformation` | Business events: job started, post published, content created |
| `LogWarning` | Transient/expected failures: rate limits, record not found, timeout, conflict |
| `LogError` | Systematic failures indicating the service is broken: config missing, dependency down |
| `LogCritical` | Service cannot continue: startup failure, data corruption |

**`LogWarning` not `LogError`** for runtime failures that can happen for any transient reason. `LogError` means something is fundamentally wrong with the service, not that an external API returned a 429.

## Testing

For `ILogger<T>` dependencies that are not being verified, use `NullLogger<T>.Instance` — **never** `new Mock<ILogger<T>>()`.

```csharp
var service = new MyService(NullLogger<MyService>.Instance, ...);
```

## Migration from existing code

Existing `_logger.LogXxx("...", params)` calls do not need to be rewritten immediately. When touching a file for other reasons, migrate its logging to the source-generated pattern. New code must use the source-generated pattern from the start.
