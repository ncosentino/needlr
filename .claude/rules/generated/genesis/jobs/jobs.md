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
    ITimeProvider _timeProvider)
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
private const string SOCIAL_ACCOUNT_ID_KEY = "SocialAccountId";

// In ExecuteWithProgressAsync:
SocialAccountId socialAccountId = new(long.Parse(
    (string)context.MergedJobDataMap.Get(SOCIAL_ACCOUNT_ID_KEY),
    CultureInfo.InvariantCulture));
```

## Thin orchestrators

Jobs should be thin orchestrators — delegate business logic to injected services or UnitOfWork classes. Do not put significant logic directly in `ExecuteWithProgressAsync`.

## Return value

`ExecuteWithProgressAsync` returns `TriedEx<object>`. The `object` value is used for progress reporting but is not otherwise consumed by callers. Returning a descriptive summary string is acceptable.

## Registration

Schedule and register jobs via `RegisterScheduledJobOptions` in the appropriate plugin's `Configure()`. Do not manually call `services.Add*` for job types — Needlr auto-discovers them as singletons.
