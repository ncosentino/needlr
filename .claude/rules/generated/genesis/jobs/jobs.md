---
# AUTO-GENERATED from .github/instructions/genesis/jobs/jobs.instructions.md — do not edit
paths:
  - "**/*Job.cs"
---
# Quartz Job Rules

## Base class

All jobs MUST inherit from `BaseProgressJob` or `BaseSelfProgressReportingJob`. Never implement raw `IJob` directly:

```csharp
internal sealed class MyFeatureJob(
    IJobProgressReporter _jobProgressReporter,
    ILogger<MyFeatureJob> _logger,
    MyFeatureService _service,
    TimeProvider _timeProvider)
    : BaseProgressJob(_jobProgressReporter, _logger)
{
    protected override async Task<TriedEx<object>> ExecuteWithProgressAsync(
        IJobExecutionContext context,
        JobExecutionId executionId) => await
    Try.GetAsync<object>(_logger, async () =>
    {
        // implementation
    });
}
```

## Job data map parameters

Extract parameters from `context.MergedJobDataMap` using typed parsing with `CultureInfo.InvariantCulture`. Define keys as `private const string` fields:

```csharp
private const string CUSTOMER_ID_KEY = "CustomerId";

// In ExecuteWithProgressAsync:
CustomerId customerId = new(long.Parse(
    (string)context.MergedJobDataMap.Get(CUSTOMER_ID_KEY),
    CultureInfo.InvariantCulture));
```

## Thin orchestrators

Jobs should be thin orchestrators — delegate business logic to injected services or UnitOfWork classes. Do not put significant logic directly in `ExecuteWithProgressAsync`.

## Cancellation and indivisible side effects

- Thread `context.CancellationToken` through ordinary job work.
- Prefer idempotent external operations or an outbox/durable state transition when a job combines an
  external side effect with local bookkeeping.
- If no stronger atomic mechanism exists and cancellation between a completed side effect and its
  durable record would make retries unsafe, defer cancellation only across that smallest bookkeeping
  region, then observe the token immediately.

## Return value

`ExecuteWithProgressAsync` returns `TriedEx<object>`. The `object` value is used for progress reporting but is not otherwise consumed by callers. Returning a descriptive summary string is acceptable.

## Registration

Schedule and register jobs via `RegisterScheduledJobOptions` in the appropriate plugin's `Configure()`. Do not manually call `services.Add*` for job types — Needlr auto-discovers them as singletons.
