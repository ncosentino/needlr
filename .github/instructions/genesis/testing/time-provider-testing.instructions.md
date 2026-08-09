---
applyTo: "**/*.Tests/**/*.cs,**/*.Tests/*.cs"
---

# Time-dependent tests

- Use `FakeTimeProvider` from package
  `Microsoft.Extensions.TimeProvider.Testing` and namespace
  `Microsoft.Extensions.Time.Testing`.
- Reference that package from test projects only.
- Seed the clock to an explicit fixed `DateTimeOffset`; never use its current-time
  default.
- Production code under test uses `TimeProvider` APIs. Do not compensate for
  `DateTime.UtcNow`, `Stopwatch`, or unbound timers by sleeping in tests.

## Advance after registration

`Advance` fires timers already registered at or before the new instant. When the system
arms a timer asynchronously:

1. start the operation;
2. wait for the expected registration;
3. advance once;
4. await the operation.

Use `RegistrationObservingTimeProvider.WaitForArmedTimersAsync` when the registration
count is known. Use bounded `AdvanceUntilAsync` only when the count cannot be known.

`AutoAdvanceAmount` helps code that repeatedly reads time; it does not solve a timer
registration race.

## Timer integrity

- Never override `FakeTimeProvider.CreateTimer` to complete immediately (NLF0028).
- Observation wrappers call `base.CreateTimer` and forward `Change`, `Dispose`, and
  `DisposeAsync`.
- `Task.Delay(delay, provider, token)`, provider-backed
  `CancellationTokenSource`, and provider timers remain controlled by the fake clock.

## DI override

When the subject is resolved from the generated fixture, replace the production
registration rather than constructing the subject manually:

```csharp
var fakeTime = new FakeTimeProvider(
    new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

var fixture = new TestFixtureBuilder()
    .UsingDependency((TimeProvider)fakeTime)
    .Build();
```

The `TimeProvider` upcast is required because registration uses the argument's static
type. A generic `UsingDependency<TimeProvider>(fakeTime)` overload is equivalent.

API tests apply the same override in `OnModifyBuilder` before the application is
created.
