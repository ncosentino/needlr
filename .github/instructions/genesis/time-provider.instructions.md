---
applyTo: "**/*.cs"
---

# Time Provider Rules

## NEVER use DateTime.Now, DateTime.UtcNow, DateTimeOffset.Now, or DateTimeOffset.UtcNow

Direct access to system time is **FORBIDDEN** in production code. It makes code untestable because time cannot be controlled in tests.

## Use TimeProvider instead

Inject `TimeProvider` (built into .NET 8+) and call `_timeProvider.GetUtcNow()`:

```csharp
internal sealed class MyUnitOfWork(
    ILogger<MyUnitOfWork> _logger,
    TimeProvider _timeProvider,
    MyRepository _repository) : IMyUnitOfWork
{
    public async Task<TriedEx<Thing>> TryCreateAsync(
        CreateThingInput input,
        string ownerUserId,
        CancellationToken cancellationToken) => await
    Try.GetAsync<Thing>(_logger, async () =>
    {
        var now = _timeProvider.GetUtcNow().DateTime;
        // ...
    });
}
```

## Return types

`GetUtcNow()` returns `DateTimeOffset`. Use:
- `_timeProvider.GetUtcNow()` when you need `DateTimeOffset`
- `_timeProvider.GetUtcNow().DateTime` when you need `DateTime`

## Registration

Register `TimeProvider.System` as a singleton in your DI setup:

```csharp
services.AddSingleton(TimeProvider.System);
```

## Test code

Test code is exempt from this rule — use `FakeTimeProvider` from `Microsoft.Extensions.Time.Testing` to control time in tests:

```csharp
var fakeTime = new FakeTimeProvider();
fakeTime.SetUtcNow(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
// inject fakeTime wherever TimeProvider is needed
```
